# SmartFiles

A Windows assistant for organizing new downloads and received files. Review category suggestions and confirm each move. Existing files stay in place.

## Download

[Windows x64 release and SHA-256 checksum](https://github.com/lcz1444532800-byte/SmartFiles/releases/latest).

Extract to `D:\` and launch `D:\SmartFilesApp\Release\SmartFiles.exe`. The portable package includes .NET and the BGE/TinyCLIP models. OCR has a separate local dependency below. Exit SmartFiles before updating an existing installation. Keep your `Data` directory and organized files.

## Background operation

- **Watcher** waits for filesystem events. It is the only long-running process, without WPF, SQLite, OCR, ONNX, or network dependencies.
- **Worker** starts on demand for metadata, models, and API suggestions. It exits after 45 idle seconds by default, configurable from 30 to 600 seconds. Model subprocesses exit with it.
- **SmartFiles.exe** is the WPF interface. Closing it exits the UI by default. Notifications can reopen a specific task.

Startup launches only `Release\Watcher\SmartFiles.Watcher.exe --startup`, with a 20-second delay. Tray residency is optional and off by default. Pause keeps the startup setting; **Exit SmartFiles completely** stops every app process.

## Build and run

Requirements: Windows 11 x64, .NET 8 SDK, Git, and a writable D: drive. This version uses `D:\SmartFilesApp` as its runtime home and `D:\SmartFiles` as its default category root; it is not cross-platform.

Clone into an empty `D:\SmartFilesApp`, then run:

```powershell
.\build.ps1 -Publish
.\Release\SmartFiles.exe
```

For an existing installation, build in a separate checkout and review the output before updating. Never run `git clean` against private installation data. Preserve the `Watcher`, `Worker`, and `AIWorker` subdirectories; the trimmed Watcher runtime must not overwrite the UI runtime.

Review **Sources**, select folders, then enable monitoring. Startup is optional. Only events after monitoring begins are admitted; existing user files are not scanned. D: unavailability never falls back to C:. Confirmed new C: downloads can use a verified cross-drive move; existing files remain excluded.

## AI providers and models

DeepSeek and Gemini are the main presets; other Chat Completions-compatible APIs are supported. Enter your own key in Settings. Keys use Windows DPAPI and are isolated by API base URL. Provider model discovery determines actual availability.

Local text routing uses BGE-small-zh ONNX INT8. Local vision uses MIT-licensed TinyCLIP. Pinned revisions and weight checksums are in `Models/*/manifest.json`. Obtain the matching ONNX and tokenizer assets from those upstream revisions. Models are not automatically downloaded.

OCR currently reads PP-OCRv4 resources from an existing `D:\DeepPickAI\models` installation. Without them, disable the relevant local AI option or use fallback classification. MobileCLIP weights are excluded because their separate research license does not permit this product use. See [design research](docs/research-notes.md).

## Privacy and storage

Version 1 has **no complete-file upload feature**. AI receives only necessary filenames, metadata, limited document/OCR text, and local candidates. A compressed in-memory thumbnail, at most 768 pixels on its long edge, is attached only when necessary. Sufficient OCR suppresses additional image uploads. Real paths are redacted. AI-selected category IDs and names are validated locally; responses cannot execute arbitrary operations.

Requests, raw responses, full extracted text, keys, and permanent image copies are not retained. Logs are capped at 50 MiB; cache and model cache at 100 MiB each. History keeps the latest 10,000 terminal records or 180 days, whichever limit is reached first. Active recovery data is preserved. Undo uses identity-checked moves, not backup copies. Settings includes disk usage and safe cache cleanup.

## English interface and verification

The interface, notifications, dialogs, built-in messages, and new default categories are English. Existing built-in categories receive English display aliases without renaming physical folders or changing their IDs. User filenames, custom labels, keywords, settings, keys, and history are preserved. Multilingual file recognition remains supported.

```powershell
.\run-tests.ps1
```

Tests use synthetic data under `D:\SmartFilesApp\TestData`, with results under `D:\SmartFilesApp\DevArtifacts`. The English release adds legacy compatibility coverage. Earlier Watcher idle measurements averaged 26.88 MiB over 10 minutes; see [validation](docs/validation.md) and [architecture](docs/architecture.md).

Builds are unsigned. Smart App Control may block them; a GitHub release is not trusted signing or Microsoft approval. SmartFiles never disables system protection. Actual reboot, sudden power/drive loss, physical cross-drive operation, and manual notification clicks remain unverified.

## Licensing

No open-source license has been assigned to the application code. Public visibility does not grant redistribution or commercial rights. Third-party components and models retain their licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and `licenses/`.
