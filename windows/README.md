# Clipori for Windows · WPF MVP (unofficial)

> **Status: first implementation; Windows compilation and behavior have NOT been verified in the authoring environment.**
> Original project: [Panzer-Jack/Clipori](https://github.com/Panzer-Jack/Clipori), Apache-2.0.

An independent Windows 10/11 implementation of Clipori's floating horizontal clipboard shelf, inspired by the upstream Mac SwiftUI app. Windows-specific code lives entirely in `windows/Clipori.Windows/`, leaving upstream SwiftUI and Xcode sources untouched.

## Implemented in phase 1

- Native Windows clipboard change notifications (`AddClipboardFormatListener`); saves text, URLs, likely code, and copied images.
- Horizontal pastel-lavender shelf, card hover lift, text/image previews, card context actions.
- **Ctrl+Alt+V** global shortcut; tray shortcut fallback on conflict.
- One click replaces clipboard; double click hides shelf, restores target window, attempts Ctrl+V through `SendInput`.
- Offline keyword search, type filters, pin/unpin, delete, new notes, pause recording.
- SQLite database and PNG cache in `%LOCALAPPDATA%\CliporiWindows`; 30-day retention for unpinned entries.
- Single-instance mutex, maximum 1000 on-screen rows, basic sensitive-app process exclusions.
- Windows GitHub Actions build and publish workflow.

## Installation/build

**Intended for Windows 10 and Windows 11 x64. Requires .NET 10 SDK for building.** Actual Windows compilation and functional testing are pending.

```powershell
# From PowerShell in the windows directory:
.\build-windows.ps1
# Then run:
.\publish\Clipori-Windows-win-x64\Clipori.Windows.exe
```

A successful GitHub Actions build outputs the self-contained Windows x64 app as the `Clipori-Windows-win-x64` workflow artifact.

## Usage

- Ctrl+Alt+V: show/hide shelf
- Left click a card: copy; shelf stays visible
- Double click / Enter: copy and attempt to paste into previously active app
- Ctrl+1..9: paste corresponding visible item
- Ctrl+F: search
- Ctrl+T: pause or resume capture
- Ctrl+N: new text note
- Esc: hide; double-click tray icon to reopen
- Right click a card: preview, pin/unpin, delete

## Explicit phase-1 limitations

- This is a **source-code MVP**, not a signed installer or a claim of completed 1:1 parity.
- AI search/classification, custom categories and drag and drop, existing entry editing, floating sticky notes, configurable shortcuts and application-exclusion settings, undo and sync remain planned.
- Windows may block simulated paste into elevated or nonstandard applications; manually press Ctrl+V if necessary.
- Excluded process capture detection is approximate, not a complete password/secret protection mechanism. Pause capture before handling sensitive data.
- Clipboard history is stored locally **without encryption**; do not copy passwords or confidential data until stronger protection ships.
- Windows APIs and runtime behavior have not yet been tested here. A green CI build validates compilation only.

## Licensing

This is an unofficial Windows port, not endorsed by the original Clipori author. The root Apache-2.0 `LICENSE` in this Fork applies; see [NOTICE.md](NOTICE.md) for attribution and modifications.
