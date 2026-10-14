# Clipori for Windows v0.2.0-preview.1

**Unofficial WPF port** of Clipori, originally by Panzer-Jack, under Apache-2.0.

This is an **unsigned pre-release**, not a stable Windows build; user-device validation is still necessary.

## Fixed from v0.1
- Proper Clipori violet-cat system tray icon.
- Single-click direct paste (configurable) and Ctrl+click copy-only. Prior active window and focused input are remembered and restored when Windows permits. Secure/elevated targets may require manual Ctrl+V.
- Hide shelf automatically when user clicks elsewhere.
- Windows Explorer file/folder clipboard format preserved for actual file paste.
- Improved Python and C# code-content detection (regression-tested).
- Optional Windows Debug build plus rotating diagnostics and sanitized crash/error logs.

## New P0 / P1 capabilities
- DPAPI-protected local history, PNGs, notes and API secrets, with migration from prior plaintext storage (backup first).
- Configurable exclusion list, retention, startup-at-login and click behavior.
- Editable items, custom category tabs, color selection, pinboard retention, Ctrl multi-select and drag-to-category.
- Optional OpenAI/Anthropic-compatible AI recall, AI categorization off by default.
- Windows CI compiles and runs code-detection and DPAPI regression checks.

## Known gaps
- Windows 10/11 app-specific paste and GUI acceptance tests pending. Auto paste across elevated app boundaries may fail.
- Full pixel-perfect animation, true independent floating sticky-note windows, complete undo, sophisticated category reordering, and sync are not implemented.
- File paths are references (files are not archived). No installer, code signing or auto update yet.
- Clipboard content privacy depends on exclusion rules; do not store passwords or confidential materials during testing.

Data: `%LOCALAPPDATA%\CliporiWindows`. Diagnostics: `%LOCALAPPDATA%\CliporiWindows\logs`.
