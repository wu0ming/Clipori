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

## v0.3.0-preview.1 checks
- [ ] From a text editor summon Clipori; single-click a card and ensure it copies and opens details **once**, without inadvertently pasting.
- [ ] Double-click a card quickly: no details modal opens, original caret receives pasted text.
- [ ] Enable single-click paste in Settings and verify behavior switches; disable again.
- [ ] Upgrade existing v0.2 settings with SingleClickPaste=true: verify v0.3 defaults to double-click paste.
- [ ] Change global hotkey and confirm old one releases, new one summons; try an occupied combo and confirm rollback.
- [ ] Customize Search, Note, Pause shortcuts, exit/restart, confirm they persist.
- [ ] Search "不清楚" with a stored item containing "不是很清楚"; confirm fuzzy hit and visible highlight.
- [ ] Search a mistyped English word and check ranking; rapid typing should not freeze the shelf.
- [ ] Test 1000 history items including images: scroll should remain smooth; search should only render up to 70 matches.
- [ ] Zoom a large image using +, -, 100%, fit and Ctrl+wheel; inspect clarity and memory.
- [ ] Copy/paste from Notepad and Windows Explorer; logs should no longer report SendInput error 87.
