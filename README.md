# Murmur

**Local voice typing for Windows. Your voice never leaves your PC.**

Murmur is a free, open-source dictation app for Windows. Hold a global hotkey, speak, and
your words are typed into whatever app has focus — email, chat, code editor, browser,
anything with a text field. All speech-to-text runs **locally** using Whisper. No cloud, no
account, no per-use cost.

It's an open-source alternative to Wispr Flow, with two deliberate advantages: it's
**private** (fully local processing) and **free**.

## How it works

```
Hold hotkey  → record mic (WASAPI)
Release      → transcribe locally with Whisper → paste into the focused app
```

The default injection strategy snapshots your clipboard, pastes the transcribed text, then
restores your original clipboard a moment later — so dictating never clobbers what you had
copied.

## Status

- **Milestone 0 — Skeleton:** ✅ solution, projects, interfaces, settings store, tray app.
- **Milestone 1 — Core loop:** ✅ global push-to-talk hotkey → mic capture → local Whisper →
  clipboard paste into the focused app, with clipboard restore and tray feedback.
- Milestone 2+ (settings window, model download progress UI, first-run wizard, additional
  injection strategies, VAD, etc.) are on the roadmap in `CLAUDE.md`.

## Requirements

- Windows 10/11, x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build from source

## Build & run

```bash
dotnet restore
dotnet build
dotnet run --project src/Murmur.App
dotnet test
```

On first launch Murmur downloads the `ggml-base` Whisper model (~150 MB) to
`%AppData%\Murmur\models`. This is the only network request Murmur makes; the download
happens once and the model stays on your PC. Settings live in `%AppData%\Murmur\settings.json`.

## Usage

1. Run the app — it lives in the system tray (no main window).
2. Put focus in any text field.
3. **Hold Right Ctrl** (the default hotkey), speak, then release.
4. Your words are typed into the focused field.

The tray icon shows the current state (ready / listening / transcribing) and surfaces
notifications for the model download and any errors.

### Settings (`%AppData%\Murmur\settings.json`)

| Setting | Default | Notes |
| --- | --- | --- |
| `HotkeyVirtualKey` | `163` (Right Ctrl) | Windows virtual-key code |
| `HotkeyMode` | `PushToTalk` | or `Toggle` |
| `ModelName` | `ggml-base` | any whisper.cpp GGUF model name |
| `Language` | `auto` | e.g. `en`, or `auto` to detect |
| `ClipboardRestoreDelayMs` | `150` | delay before restoring your clipboard |
| `PostKeyUpDelayMs` | `50` | settle time after releasing the hotkey |

The file is safe to hand-edit; unknown/future settings are preserved across saves.

## Known limitation

A non-elevated process cannot send input to windows running **as administrator** (Windows
UIPI). To dictate into elevated apps, run Murmur as administrator too. A built-in
"run as admin" option is planned for a later milestone.

## License

MIT — see [LICENSE](LICENSE).
