# Windows paste and UI parity research (2026-10-14)

## Confirmed production issue from user diagnostics
`System.EntryPointNotFoundException: NativeMethods.GetCurrentThreadId()`.
Cause: the code declared `GetCurrentThreadId` under `user32.dll`. The export is in **kernel32.dll**.
This blocked all text, link, and file pastes before `SendInput` could run.

## Reference projects studied

- [Ditto](https://github.com/sabrogden/Ditto) documents pasting selected clipboard content directly into the **previously active window**. We reused that behavioral expectation, not project source code.
- [CopyQ Windows integration](https://github.com/hluk/CopyQ/blob/master/src/platform/win/winplatformwindow.cpp) uses foreground activation, optional `AttachThreadInput`, a wait for modifier keys to release, and `SendInput` for a virtual-key paste shortcut. The current implementation applies these **documented Win32 APIs independently**, with explicit failure paths. No C++ code was copied from these GPL projects.
- [Microsoft Win32: GetCurrentThreadId](https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentthreadid) is provided by **Kernel32.dll**.

## Implementation invariants

1. Shelf summons remember previous top-level window and its focused child.
2. Selecting an item updates clipboard, hides shelf, and waits briefly until Ctrl/Alt/Shift/Windows modifiers are released.
3. Activate previous window; if needed and allowed, temporarily attach to a GUI input thread; always detach.
4. Confirm foreground is the intended window before injecting Ctrl+V.
5. A successful SendInput means **keystrokes were injected**, not that the target app inserted text. No false 'pasted' success message.
6. On failure, keep clipboard content and notify about manual Ctrl+V; record only event names and Win32 result.
7. Never inject into an invalid/replaced window handle.

CI now checks that `kernel32.dll` exports `GetCurrentThreadId` and that P/Invoke resolves successfully, making the prior entry-point failure reproducible in a test.

## UI implementation comparison

macOS `Sources/CliporiMac/ShelfView.swift` uses a 237×233 memory card, layered translucent glass, ~16pt headings, colored category chips, ordinal indices, subtle shadows and a spring-like hover lift.
Windows WPF now approximates this with native XAML layered brushes, 237×233 cards, category badges, indices, hover elevation, smooth hidden-scroll layout, arrow navigation and a custom vector mascot. It is still a WPF **glass approximation**; true macOS vibrancy and identical spring curves are not supplied by WPF.

Remaining UI parity: OS backdrop (consider Win11 Mica separately), full keyboard card selection, actual Pinboard visual organization, preview/edit shortcuts, accessible color contrast at high-DPI and borderless multi-monitor placements.
