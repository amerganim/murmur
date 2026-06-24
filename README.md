# Murmur

**Local voice typing for Windows. Your voice never leaves your PC.**

Murmur is a free, open-source dictation app for Windows. Hold a global hotkey, speak, and
your words are typed into whatever app has focus — email, chat, code editor, browser,
anything with a text field. All speech-to-text runs **locally** with Whisper. No cloud, no
account, no per-use cost, no telemetry.

It's an open-source alternative to Wispr Flow, with two deliberate advantages: it's
**private** (fully local) and **free**.

```
Hold hotkey  → record mic (WASAPI)
Release      → trim silence → transcribe locally with Whisper → insert into the focused app
```

---

## What makes Murmur different

Plenty of "Whisper + type it out" scripts exist. Murmur focuses on the things that make
dictation actually pleasant to use every day:

- **Reliable text insertion (the hard part).** Murmur uses a layered fallback chain —
  **clipboard paste → UI Automation → per-character Unicode SendInput** — and picks the
  right one for the focused app. The clipboard strategy **snapshots and restores your
  clipboard** (on a delay, so the paste completes first) instead of clobbering whatever you
  had copied — the #1 bug in simpler tools. It uses **Ctrl+Shift+V automatically in
  terminals**, and the UI-Automation path **never overwrites existing text**.
- **Command Mode with a local LLM.** Select text, hold a key, say *“make this more formal”*
  or *“fix the grammar”*, and a local **Ollama** model rewrites it in place — a Wispr-Flow
  style feature that stays 100% on your machine. (Optional, off by default.)
- **Accuracy aids beyond raw Whisper:** silence trimming (VAD), automatic audio
  normalization to cut hallucinations on quiet mics, a **custom vocabulary** that biases
  Whisper toward your names/jargon, **voice shortcuts** (say a phrase → expand to canned
  text), and **per-app formatting** (e.g. no trailing period in terminals).
- **Polished, no-config-needed UX:** first-run wizard (mic → model → hotkey → live test),
  settings that apply instantly without restarting, a recording overlay, model
  auto-download with a progress bar, single-instance protection, and tray notifications
  that never leave you guessing.
- **Run-as-administrator mode** so you can dictate into elevated windows (works around
  Windows UIPI), with a clean hand-off.
- **Genuinely local & free:** the only network request is the one-time model download.

---

## Features

**Dictation**
- Global push-to-talk **or** toggle hotkey (low-level keyboard hook, configurable key).
- Local Whisper transcription (Whisper.net / whisper.cpp), model kept warm for fast repeats.
- Microphone selection (WASAPI); capture is reused across takes so recording starts instantly.
- Silence trimming + audio normalization before transcription.
- 20+ selectable languages, or automatic language detection.

**Text insertion**
- Fallback chain: Clipboard paste → UI Automation → Unicode SendInput.
- Clipboard-safe (snapshots & restores your clipboard; preserves unicode/emoji).
- Terminal-aware paste chord (Ctrl+Shift+V for Windows Terminal, cmd, PowerShell, pwsh, …).
- Per-app formatting (strip trailing punctuation in terminals).

**Productivity**
- **Voice shortcuts / snippets:** say a trigger phrase, type a saved expansion.
- **Custom vocabulary:** feed names/jargon to Whisper so they’re spelled correctly.
- **Command Mode:** voice-edit the selected text with a local Ollama model.

**App & UX**
- System-tray app with state icons (ready / listening / transcribing) + balloon notifications.
- First-run setup wizard with a live dictation test.
- Settings window — all changes apply live, no restart.
- Floating recording overlay (click-through, never steals focus).
- Auto-start with Windows (optional).
- Run as administrator (optional) for elevated windows.
- Model auto-download with progress; single-instance guard.

**Privacy**
- 100% local processing, no account, **no telemetry**. Only network call: one-time model download.

---

## Requirements

- Windows 10 / 11, x64
- A microphone
- ~150 MB free disk for the default model (more for larger models)
- For **Command Mode** only: [Ollama](https://ollama.com) installed and running

---

## Install

### Option A — download a release (recommended)
1. Go to the [Releases](https://github.com/amerganim/murmur/releases) page.
2. Download `Murmur-<version>-win-x64.zip`, extract it, and run **`Murmur.App.exe`**.
   It’s a single self-contained file — no .NET install needed.
3. Windows SmartScreen may warn because the build isn’t code-signed → **More info → Run
   anyway**. (It’s open source; you can read or build it yourself.)

On first launch, the setup wizard helps you pick a mic, model, and hotkey, and downloads the
Whisper model (~150 MB) to `%AppData%\Murmur\models`.

### Option B — build from source
```bash
dotnet restore
dotnet build
dotnet run --project src/Murmur.App
dotnet test
```

---

## Usage

1. Murmur lives in the **system tray** (no main window).
2. Put your cursor in any text field.
3. **Hold Right Ctrl** (default), speak, then release. Your words are typed in.
4. Right-click the tray icon for **Settings**, **Voice shortcuts**, **Restart as
   administrator**, and **Exit**.

### Command Mode (optional)
1. Install Ollama and pull a model: `ollama pull llama3.2`
2. Tray → **Settings** → enable **Command Mode**, pick a command hotkey (default Right Alt).
3. Select some text in any app, **hold the command hotkey**, say an instruction
   (“summarize this”, “translate to French”, “make it polite”), release. The selection is
   replaced with the rewrite. If Ollama isn’t running, Murmur tells you instead of failing.

---

## Models & performance

Bigger models are more accurate (especially for non-English) but slower, and speed depends
heavily on your hardware. Measured on a **CPU-only laptop (Intel i5-1235U, no GPU)** for a
~3.4 s clip:

| Model | Size | Transcribe time (CPU) | Good for |
| --- | --- | --- | --- |
| Tiny | ~75 MB | very fast | quick notes, English |
| **Base** (default) | ~145 MB | ~2.5 s | everyday English |
| Small | ~465 MB | ~10 s | better accuracy, many languages |
| Medium | ~1.5 GB | ~35 s | high accuracy; **needs a GPU to be usable** |
| Turbo / Large v3 | 0.5–3 GB | 60 s+ | best accuracy; **GPU strongly recommended** |

Change the model in **Settings → Model**. The takeaway: **on a CPU, Base/Small are the
practical choices**; Medium and larger are only comfortable with a GPU (see below).

---

## Testing on a high-spec / GPU PC (help wanted 🙏)

The published binary uses the **CPU** Whisper runtime, which is why large models are slow on
modest laptops. If you have a capable PC — especially an **NVIDIA GPU** — you can unlock fast,
high-accuracy transcription (great for Bengali and other lower-resource languages) and help
us validate it:

**Build with GPU acceleration:**
1. Edit [`src/Murmur.Core/Murmur.Core.csproj`](src/Murmur.Core/Murmur.Core.csproj) and
   replace the CPU runtime package with a GPU one:
   - NVIDIA: `Whisper.net.Runtime.Cuda`
   - Cross-vendor (AMD/Intel/NVIDIA): `Whisper.net.Runtime.Vulkan`

   ```xml
   <!-- replace: <PackageReference Include="Whisper.net.Runtime" Version="1.9.1" /> -->
   <PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.9.1" />
   ```
2. `dotnet run --project src/Murmur.App`
3. **Settings → Model → Medium / Turbo / Large v3**, set your language, dictate.

**What’s especially useful to report** (please open a [GitHub issue](https://github.com/amerganim/murmur/issues)):
- Transcription speed per model on your GPU (a sentence or two).
- Accuracy for **non-English / complex-script languages** (Bengali, Tamil, Arabic, Hindi…),
  ideally comparing Small vs Medium vs Large.
- Whether insertion works across your apps (browsers, Electron apps, terminals, IDEs).
- Command Mode quality with different Ollama models.

To capture exactly what the mic fed Whisper, set `"SaveDiagnosticRecording": true` in
`settings.json`; the last take is written to `%AppData%\Murmur\last-recording.wav`.

---

## Settings (`%AppData%\Murmur\settings.json`)

All settings are editable in the UI; the file is also safe to hand-edit (unknown/future keys
are preserved).

| Setting | Default | Notes |
| --- | --- | --- |
| `HotkeyVirtualKey` | `163` (Right Ctrl) | dictation hotkey (Win32 virtual-key code) |
| `HotkeyMode` | `PushToTalk` | or `Toggle` |
| `ModelName` | `ggml-base` | any whisper.cpp GGUF model name |
| `Language` | `auto` | e.g. `en`, `bn`, or `auto` |
| `TrimSilence` | `true` | trim leading/trailing silence before transcribing |
| `CustomVocabulary` | `""` | names/jargon to bias Whisper |
| `Snippets` | `[]` | voice shortcuts (`Trigger` → `Expansion`) |
| `TerminalStripTrailingPunctuation` | `true` | drop trailing `.`/`!`/`?` in terminals |
| `CommandModeEnabled` | `false` | enable local-LLM Command Mode |
| `CommandModeHotkeyVirtualKey` | `165` (Right Alt) | Command Mode hotkey |
| `OllamaEndpoint` / `OllamaModel` | `http://localhost:11434` / `llama3.2` | Command Mode backend |
| `StartWithWindows` | `false` | auto-start at sign-in |
| `ClipboardRestoreDelayMs` | `150` | delay before restoring your clipboard |
| `PostKeyUpDelayMs` | `50` | settle time after releasing the hotkey |

---

## Distribution

Murmur is distributed as a **portable single-file `.exe`** via GitHub Releases — no installer,
no admin needed to run it. Releases are produced automatically by GitHub Actions:

- **Cut a release:** push a version tag and the
  [`Release` workflow](.github/workflows/release.yml) builds a self-contained, single-file
  `win-x64` binary, zips it, and publishes a GitHub Release:
  ```bash
  git tag v0.1.0
  git push origin v0.1.0
  ```
- **CI:** every push/PR builds and runs the tests ([`CI` workflow](.github/workflows/ci.yml)).

**Roadmap for wider distribution:**
- **Code signing** to remove the SmartScreen warning (needs a code-signing certificate).
- **winget** and **Scoop** manifests for `winget install` / `scoop install`.
- An optional **MSIX / Inno Setup installer** with Start-menu and auto-update.

(Contributions for any of these are welcome.)

---

## Privacy

By default Murmur makes exactly **one** network request: downloading the Whisper model on
first run. Audio is processed locally and never leaves your machine. There is no telemetry
and no account. Command Mode talks only to your **local** Ollama server.

---

## Project layout

```
src/
  Murmur.Core/    # engine: audio, STT, injectors, settings, models, snippets, Ollama client
  Murmur.Hotkey/  # low-level global keyboard hook
  Murmur.App/     # WPF tray app, windows, wiring
tests/
  Murmur.Core.Tests/
```

## Known limitations
- Large models are slow without a GPU (see above).
- Lower-resource languages (e.g. Bengali) need a large model — i.e. a GPU — for good results.
- The release binary is not yet code-signed (SmartScreen warning on first run).

## License
MIT — see [LICENSE](LICENSE).
