# Clipori for Windows v0.2.0-preview.1

**UNOFFICIAL community WPF port** of Clipori; original project by Panzer-Jack, Apache-2.0.

This is a **pre-release**, not a stable, officially supported or signed Windows release.

## What's new
- Single-click paste to the previously focused application; Ctrl+click copies only. Double-click continues to paste.
- Restores previous foreground window and focused input element where Windows allows it; fallback clipboard copy on permission restrictions.
- Automatically hides the shelf after clicking elsewhere.
- Real Clipori-themed tray icon, tray settings and log folder access.
- Windows Explorer files/folders clipboard history using CF_HDROP and file-list paste.
- Better rule-based Python/C#/code recognition.
- Local rotating logs, debug artifact and regression tests. Log events intentionally exclude clipboard bodies.
- DPAPI encryption for text, titles, notes, images and API credentials; legacy plaintext records are migrated on first launch.
- Basic custom categories, Ctrl multi-select, drag selected cards onto a category, editing notes and search.
- Opt-in OpenAI/Anthropic-compatible search and optional AI classification (disabled by default).
- Settings: app exclusions, retention, launch at login, clipboard behavior and AI endpoint.

## Limitations
- **Windows 10/11 hands-on QA still required:** GUI and clipboard focus vary by application; administrator windows block unelevated SendInput.
- File references are not duplicated or archived. Pasting fails if source files are moved or deleted.
- UI fidelity, full Pinboards, visual color management, rich sticky notes, full keyboard navigation, cross-device sync and update installer are not complete.
- Backup `%LOCALAPPDATA%\CliporiWindows` before upgrading: DPAPI migration may prevent downgraded clients from reading data.
- Audit log files before sharing; never send secrets to AI providers you do not trust.

See `windows/README-DIAGNOSTICS.md` for debug instructions.
