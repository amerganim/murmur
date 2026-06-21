# Murmur

**Local voice typing for Windows. Your voice never leaves your PC.**

Murmur is a free, open-source dictation app for Windows. Hold a global hotkey, speak, and your words are typed into whatever app has focus — email, chat, code editor, browser, anything with a text field. All speech-to-text runs **locally** using Whisper. No cloud, no account, no per-use cost.

This is an open-source alternative to Wispr Flow, with two deliberate advantages: it is **private** (fully local processing) and **free**.

---

## North-star principles (do not compromise these)

1. **Accuracy of insertion first.** The text that lands in the user's app must be exactly what was transcribed, in the right place, without corrupting their clipboard or dropping characters. This is the feature. Treat the injection layer as the most important code in the repo.
2. **Local-first.** Default path uses an on-device Whisper model. No network calls required for core functionality.
3. **Lightweight.** Low idle RAM, fast cold start, small installer. We are explicitly fixing the things users complain about in Wispr (heavy RAM, 8–10s startup).
4. **Privacy.** No telemetry by default. No audio leaves the machine in the default configuration.

---

## Tech stack (pinned — do not substitute without asking)

- **.NET 8** (LTS), C#
- **WPF** for all UI (settings window, recording overlay, first-run wizard)
- **Whisper.net** + **Whisper.net.Runtime** — local STT via whisper.cpp, GGUF models
- **NAudio** — microphone capture (WASAPI)
- **Hardcodet.NotifyIcon.Wpf** — system tray icon
- **FlaUI.Core** + **FlaUI.UIA3** — UI Automation, used by one injection strategy
- **System.Text.Json** — settings persistence (built in)
- **xUnit** — tests
- Native Win32 via P/Invoke for the keyboard hook and `SendInput`

Settings live as JSON in `%AppData%\Murmur\settings.json`.

---

## Repository structure

```
/src
  Murmur.Core/        # No UI. Audio capture, STT pipeline, ITextInjector + strategies, settings, models
  Murmur.Hotkey/      # Low-level keyboard hook service (WH_KEYBOARD_LL)
  Murmur.App/         # WPF: tray, settings window, recording overlay, first-run wizard, app wiring
/tests
  Murmur.Core.Tests/  # Unit tests, especially for the injector strategies and settings
Murmur.sln
README.md
CLAUDE.md
```

**Hard rule:** `Murmur.Core` and `Murmur.Hotkey` must have **no WPF / UI dependencies**. Keep the engine headless and testable. The App project wires everything together.

---

## Architecture: the core loop

```
Hotkey down  → start mic capture (NAudio, WASAPI)
Hotkey up    → stop capture → run VAD to trim silence → transcribe (Whisper.net)
             → (optional) clean text → inject into foreground app
```

### Text injection — the layered fallback chain

Implement an `ITextInjector` interface with multiple strategies tried in order. Select/skip strategies based on the **foreground process** (`GetForegroundWindow` → process name).

1. **ClipboardPasteInjector (default).** Snapshot current clipboard → set clipboard to the text → send paste chord → restore clipboard after a short, configurable delay.
   - **App-aware paste chord:** terminals (Windows Terminal, `WindowsTerminal`, `cmd`, `powershell`, VS Code integrated terminal, PuTTY) need `Ctrl+Shift+V`, not `Ctrl+V`. Detect by process name and switch.
   - **Restore timing is critical:** do NOT restore the clipboard synchronously right after sending Ctrl+V — the paste hasn't completed yet and you'll corrupt the user's clipboard. Restore on a short delay (default ~150ms, configurable). This is the #1 bug in tools like this.
   - **Preserve unicode/emoji.** Snapshot text format at minimum; design the snapshot so other formats (image/files) can be preserved later.
2. **UiaInjector.** Use UI Automation `ValuePattern` / `TextPattern` to write directly into the focused control when supported — cleanest possible insertion, no clipboard or keystrokes. Many rich editors (browsers, Electron) don't support it, so it can't be the only method.
3. **SendInputUnicodeInjector (last resort).** Synthesize each char with `KEYEVENTF_UNICODE`. Works where paste fails; slower for long text; some raw-input apps ignore it.

The chain tries strategies until one reports success. Order and per-app overrides should be configurable.

### Keyboard hook (push-to-talk)

Use a **low-level keyboard hook** (`SetWindowsHookEx` with `WH_KEYBOARD_LL`) in `Murmur.Hotkey`, NOT `RegisterHotKey`. We need both **key-down and key-up** to support hold-to-talk. Support two modes:
- **Push-to-talk:** hold hotkey, speak, release.
- **Toggle:** press once to start, press again to stop.

Add a small configurable delay + focus check between hotkey-release and paste so the key-up event doesn't collide with injected input.

---

## Critical gotchas (design for these now, not later)

- **UIPI / elevation:** a non-elevated process cannot send input to an admin-elevated window. Provide an optional "Run as administrator" mode and document the limitation clearly in the README.
- **Keep the Whisper model warm:** lazy-load it once, then keep it in memory so the first transcription isn't slow. Don't reload per use.
- **Custom dictionary → Whisper `initial_prompt`:** bias transcription toward the user's names/jargon by feeding their dictionary into the prompt.
- **VAD before transcription:** trim silence — faster and more accurate.
- **Model download, not bundle:** don't ship a 1GB model in the installer. Download the GGUF on first run with a progress bar.

---

## Coding conventions

- Nullable reference types **on**. Treat warnings as errors in CI.
- `async`/`await` for all I/O and audio/STT work; never block the UI thread.
- Dependency injection for services (`IAudioCapture`, `ISpeechToText`, `ITextInjector`, `ISettingsStore`) so they can be mocked in tests.
- One class per file. XML doc comments on public interfaces.
- Strategies (`ITextInjector` implementations) must be independently unit-testable.

---

## Build & run

```bash
dotnet restore
dotnet build
dotnet run --project src/Murmur.App
dotnet test
```

Target a single-file, self-contained publish for releases later:
```bash
dotnet publish src/Murmur.App -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

---

## Roadmap (work milestone by milestone, commit per milestone)

### Milestone 0 — Skeleton
Create the solution, three projects, DI wiring, settings load/save, empty interfaces.
**Done when:** `dotnet build` and `dotnet test` pass; app launches to a tray icon.

### Milestone 1 — MVP core loop (the whole point)
Global push-to-talk hotkey → record mic → local Whisper transcribe → ClipboardPasteInjector into focused field.
**Done when:** holding the hotkey and speaking into Notepad, Chrome, and VS Code reliably types the spoken text, and the user's existing clipboard is restored intact afterward.

### Milestone 2 — Daily-usable
Settings window (hotkey, mic device, model size, language, push-to-talk vs toggle), tray menu, auto-start option, recording overlay indicator, app-aware paste chord for terminals, model auto-download with progress, first-run wizard (mic → model → hotkey → test).
**Done when:** a new user can install, complete the wizard, and dictate without touching config files.

### Milestone 3 — Accuracy & robustness
Add UiaInjector and SendInputUnicodeInjector to the fallback chain, VAD, custom dictionary → initial_prompt, unit tests for all injector strategies, optional "run as admin" mode.
**Done when:** injection works across a broad app set including terminals and elevated windows (in admin mode), and the injector strategies have test coverage.

### Milestone 4 — Power features
Command Mode (highlight text + voice command → local LLM via Ollama rewrites it), snippets/voice shortcuts, per-app formatting, streaming partial transcription for lower perceived latency.

---

## Start here (first task for Claude Code)

Build **Milestone 0**, then **Milestone 1**. Specifically:

1. Scaffold the solution and the three projects with the structure above.
2. Define interfaces in `Murmur.Core`: `IAudioCapture`, `ISpeechToText`, `ITextInjector`, `ISettingsStore`, plus a `Settings` model.
3. Implement `ClipboardPasteInjector` first — with correct delayed clipboard restore — and write unit tests for it.
4. Implement the `WH_KEYBOARD_LL` keyboard-hook service in `Murmur.Hotkey` with push-to-talk (key-down/up).
5. Implement NAudio mic capture and Whisper.net transcription (default model: `ggml-base`).
6. Wire the loop in `Murmur.App` with a tray icon, and verify the Milestone 1 "done when" by typing into Notepad.

Work one milestone at a time. After each, ensure build + tests pass and summarize what changed. Ask before adding any dependency not pinned above, and before any code path that sends data over the network.

---

## Out of scope (for now)

Cloud STT, mobile, macOS/Linux, accounts/auth, telemetry, multi-speaker, audio-file transcription. Windows-only, local-only, free. Keep it focused.
