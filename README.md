# Windows Clipboard Workspace

**Experimental Windows/WPF community port · working title (not a product brand)**

> **Unofficial independent fork.** This Windows work is inspired by [Clipori by Panzer-Jack](https://github.com/Panzer-Jack/Clipori) and is **not** an official Windows release or endorsed by the upstream author. The final product name and icon have not been chosen. The current development executable is still named `Clipori.Windows.exe` until rebranding is complete.

[简体中文](README.zh-CN.md) · [Windows development guide](windows/README.md) · [Smoke-test checklist](windows/WINDOWS-SMOKE-TEST.md) · [Upstream macOS project](https://github.com/Panzer-Jack/Clipori)

## Why this fork?

The original Clipori project combines a fast clipboard shelf with AI-assisted search and optional automatic classification. Its idea of turning fleeting copied material into retrievable memory inspired this Windows 10/11 port.

The longer-term goal is to explore a **privacy-conscious AI context workspace**: keep original clipboard records in structured local storage, offer explicit and selective Markdown/JSON exports for AI context, and eventually support user-controlled summarization, categorization, deduplication, and linked notes. **These are proposals, not shipped Windows features.**

## Windows status

**Pre-release v0.2.0-preview.1 is published; Windows desktop end-to-end validation and code signing remain outstanding.** The repository contains code for the following, but a successful build does not prove all interactions work:

| Area | Current status |
| --- | --- |
| Windows 10/11 x64, C#/.NET 10 WPF | Initial implementation |
| Clipboard text, links, images; heuristic code detection | Initial implementation; file paste and code detection need work |
| Local SQLite history and PNG cache | Initial implementation |
| Floating shelf, global shortcut, basic search, pins, notes | Initial implementation; UX and tray behavior need validation |
| Copy from a card | Implemented in code; user-side testing has shown copying works |
| Direct paste back into the previously focused app | **Known issue: focus restoration / simulated paste can fail** |
| AI search and optional automatic classification | Initial implementation; provider and privacy behavior still need on-device verification |
| Star knowledge archive, Markdown export, cloud sync, automated knowledge organization | **Planned; not yet implemented** |

### Immediate priorities (P0/P1)

- **P0 – reliability:** reliable focus-safe paste, Windows diagnostics/error logs, correct tray icon, hide shelf on outside click, and repeatable Windows smoke tests.
- **P1 – clipboard fidelity:** support file paste and improve code recognition; continue parity and usability work without assuming these features are finished.
- **M2 Star:** an independently persisted knowledge snapshot, selectable vault path, portable Markdown notes and an optional generated index.
- **M3+ integrations:** optional Notion/Obsidian sync, browser clipping, permissioned AI context retrieval and AI organization.

See the [Pin/Star product roadmap](docs/PRODUCT-STAR-ROADMAP.md) and [machine-readable status](project-site/roadmap.json). Keep local capture and AI network transmission separate. Clipboard history can include secrets; never silently send the entire database to an AI provider.

## Build from source (Windows)

Requires Windows 10/11 x64 and the .NET 10 SDK to build. From the repository root:

```powershell
cd windows
.\build-windows.ps1
.\publish\Clipori-Windows-win-x64\Clipori.Windows.exe
```

The binary name is temporary. See the [Windows guide](windows/README.md) and [manual test checklist](windows/WINDOWS-SMOKE-TEST.md). There is **no claim of an official or fully tested Windows installer**.

## Credits, modifications and license

- **Original project:** [Panzer-Jack/Clipori](https://github.com/Panzer-Jack/Clipori) (macOS / SwiftUI); thank you to its author for the idea and the open-source code.
- **Windows work:** an independent WPF/.NET implementation added under [`windows/`](windows/), with an independent development roadmap.
- **License:** the upstream repository uses [Apache License 2.0](LICENSE). Preserve the required license and attribution notices when redistributing applicable code or binaries. See [Windows attribution and change notes](windows/NOTICE.md); separately verify third-party licenses before releasing.
- **Branding:** upstream names and marks are mentioned **only to explain provenance**. The fork is evaluating its own product name and icon. The Apache-2.0 license does not itself grant trademark rights or official endorsement.

For the upstream macOS product, its README, screenshots, and releases, visit [the original Clipori repository](https://github.com/Panzer-Jack/Clipori). This branch's home page intentionally does not use the upstream application's cat icon as the Windows brand.

_README adapted for the Windows fork on 2026-10-14. Upstream source files and their existing notices remain attributed to their respective authors._