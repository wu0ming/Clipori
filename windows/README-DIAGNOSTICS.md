# Windows diagnostic mode & reproducibility

The Windows app keeps a **rotating local log** in `%LOCALAPPDATA%\CliporiWindows\logs\clipori.log`.
The settings window and tray menu include **打开诊断日志**.

Logs include startup, Win32 registrations, clipboard content **types**, save/copy/paste state, target restoration results, classification, and sanitized exceptions. The app intentionally does **not** log clipboard text, images, full file paths, passwords or API keys.

Windows CI produces two separate artifacts:
* `Clipori-Windows-win-x64`: self-contained Release build
* `Clipori-Windows-Debug-win-x64`: self-contained Debug build with symbols for reproducing faults

To generate a local diagnostic build:

```powershell
dotnet publish windows/Clipori.Windows/Clipori.Windows.csproj -c Debug -r win-x64 --self-contained true -o .\build\Clipori-Debug
.\build\Clipori-Debug\Clipori.Windows.exe --debug
```

When reporting a bug: send the Windows version, app version, a short reproduction, whether content was text/image/files, and the **sanitized** log. Inspect the log before sharing; OS errors may still include machine-specific information.

**Security notes:** DPAPI-protected content can be decrypted only under the same Windows user profile. Back up the original `%LOCALAPPDATA%\CliporiWindows` directory before updating from v0.1.x; the first v0.2.x launch migrates legacy plaintext. Do not copy credentials or regulated data until application exclusions and privacy have been validated on your PC. GitHub Actions green does not imply Windows 10 workstation QA passed.
