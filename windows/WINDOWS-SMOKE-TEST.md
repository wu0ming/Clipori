# Windows 10/11 smoke-test checklist (manual, not yet executed)

- [ ] Build on `windows-latest` with .NET 10 SDK; no XAML or C# compiler errors.
- [ ] Launch program, confirm tray icon appears and no initial unwanted clipboard capture.
- [ ] Press Ctrl+Alt+V: floating shelf opens at screen bottom; press again to hide.
- [ ] Copy Chinese text from Notepad; shelf displays one new card with correct characters.
- [ ] Copy a URL and C# code; confirm type filter and keyword search.
- [ ] Copy an image in Paint or screenshot-to-clipboard (Win+Shift+S); verify PNG preview.
- [ ] Click a prior card, then use Ctrl+V in Notepad; history doesn't duplicate the click.
- [ ] Double-click a prior card: shelf hides, target regains focus, Ctrl+V pastes correctly.
- [ ] Try elevated target app; verify fallback notification instead of silently assuming paste.
- [ ] Pin, unpin, delete entries; exit from tray, restart, verify SQLite persistence.
- [ ] Pause capture; copy new content; confirm it isn't stored; resume and retry.
- [ ] Right click card, preview it; create a manual note.
- [ ] Open `%LOCALAPPDATA%\CliporiWindows` and confirm expected files.
- [ ] Try two instances; second must not register duplicate listeners or hotkeys.
- [ ] Check clipboard errors from locked clipboard in Office, browser and RDP sessions.
- [ ] Validate no unintended capture from known excluded credential apps (best effort only).
