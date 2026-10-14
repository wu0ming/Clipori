# Windows 10/11 hands-on verification (not yet executed)

- [ ] Backup `%LOCALAPPDATA%\CliporiWindows` from v0.1 before upgrade; verify legacy SQLite history and images migrate.
- [ ] Confirm `Clipori.Windows.exe` starts and violet-cat tray icon appears.
- [ ] Run Debug edition; verify `logs/clipori.log` records operations but no clipboard text/secrets.
- [ ] Open Notepad at a caret; summon shelf, click an older text card once: actual text appears at prior caret.
- [ ] Ctrl+click a card: only clipboard changes, not target input.
- [ ] Double-click old item: previous app accepts paste.
- [ ] Click empty desktop: shelf hides. Try right-click menu and Settings: popup/modal remains usable.
- [ ] Copy a file from Explorer; switch to a different folder, paste previous file history card: file appears.
- [ ] Copy several Explorer files; verify list and paste multiple; move original away then check failure fallback.
- [ ] Copy sample Python (`for i in range(3):`, `append`, `sort()`), verify code classification.
- [ ] Copy PNG/screenshot, restart, copy image from card, verify dimensions and transparency.
- [ ] Create colored categories and Pinboard; Ctrl multi-select then drag to category; check persistence.
- [ ] Pin, delete, edit note/title; restart, verify SQLite history.
- [ ] Configure exclusions, test ignored process; pause/resume capture and auto-start.
- [ ] Configure AI using a trusted endpoint and non-sensitive test records; verify AI search, optional classification.
- [ ] Test Office, Edge, VS Code, cmd, RDP, and elevated windows (fallback expected).
- [ ] Test 100% / 150% / 200% DPI, multiple monitors, Windows 10 and Windows 11.
- [ ] Confirm log rotation and crash diagnostics without clipboard contents.
