# Clipori for Windows v0.2.0-preview.2 (unofficial)

**Build:** C# / WPF / .NET 10 · Windows 10/11 x64 · GitHub Actions
**Status:** CI compiler, classifier/DPAPI regression tests, and both publish configurations verified. **Windows desktop end-to-end testing is still required.**

An unofficial Windows port inspired by [Panzer-Jack/Clipori](https://github.com/Panzer-Jack/Clipori), licensed under Apache-2.0. The original macOS Swift sources are preserved on this Fork.

## Install

Download the **pre-release** from [Releases](https://github.com/wu0ming/Clipori/releases) and unzip `Clipori-Windows-v0.2.0-preview.2-win-x64.zip`. Run `Clipori.Windows.exe`.

If the release has not appeared, open the latest green [Actions](https://github.com/wu0ming/Clipori/actions) build and download `Clipori-Windows-win-x64`. A separate `Clipori-Windows-Debug-win-x64` build with symbols is provided for diagnosing failures.

## Features
- Local clipboard capture: text, URLs, recognized code (including Python), images and Windows Explorer file/folder selections (`CF_HDROP`).
- **Single click:** attempts direct paste into the previously focused input (new default). **Ctrl-click:** copy without auto-pasting. **Double click:** paste. Change preference in Settings.
- Remembers the previous foreground/focused control. Automatic hide when clicking outside; tray controls to reopen or inspect logs.
- Keyword search, horizontal card shelf, hover animation, source-app labels, quick paste, pin, delete, add/edit notes and titles, preview.
- Custom categories, multi-selection, drag cards onto category tabs, color changes and Pinboards with retention exemption.
- AI search via user-configured OpenAI-compatible or Anthropic-compatible endpoint. Optional AI category assignment, **off by default**. AI search sends selected candidate text excerpts to the service; never sends image pixels.
- Settings for capture, exclusion list, retention, optional auto-start, AI endpoint/model/API key, and diagnostics.
- Local SQLite persistence; text, titles, notes and pictures are encrypted using per-Windows-user DPAPI; stored API keys are also DPAPI protected. Legacy plain-text records are migrated on startup.
- Rotating activity/error logs in `%LOCALAPPDATA%\CliporiWindows\logs`. Logs do not intentionally store clipboard bodies.

## Important precautions
1. **Back up** `%LOCALAPPDATA%\CliporiWindows` before updating from v0.1.x. Encrypted history is tied to your Windows account. Downgrading to v0.1.x may no longer read migrated history.
2. Clipboard managers cannot recognize every secret or password field. Pause capture or exclude confidential applications. Database encryption does not prevent exposure to programs running under your own Windows user.
3. Windows may prevent simulated paste into elevated applications, protected inputs or incompatible apps. The copied content remains on the clipboard; paste manually using Ctrl+V.
4. File history stores **references to file paths**, not file data. If a file is removed or relocated, its historical record cannot paste it.
5. The port is in **pre-release**; it is not signed, independently audited, or pixel-perfect relative to the macOS original. UI, multi-monitor, Win10 focus behavior and clipboard reliability require on-device tests.

## Keyboard & mouse
- Ctrl+Alt+V show/hide | Esc hide | Ctrl+F focus search | Ctrl+T pause/resume
- Ctrl+N new note | Ctrl+1..9 quick paste | Left click card paste | Ctrl+click copy/select
- Ctrl+drag selected card(s) onto a category tab to assign multiple memories
- Right click card for preview, edit, categorize, pin and delete
- Tray icon to open main window, Settings, local log folder, pause, exit

## Development
```powershell
dotnet restore windows/Clipori.Windows/Clipori.Windows.csproj
dotnet build windows/Clipori.Windows/Clipori.Windows.csproj -c Debug
dotnet run --project windows/Clipori.Windows.Tests/Clipori.Windows.Tests.csproj -c Release
```

See `README-DIAGNOSTICS.md` and `WINDOWS-SMOKE-TEST.md`.
