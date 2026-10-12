# Attribution and changes

This is an **unofficial Windows port / independent WPF implementation** inspired by the open-source project [Clipori](https://github.com/Panzer-Jack/Clipori) by Panzer-Jack.

The upstream project is licensed under Apache License, Version 2.0. Refer to the upstream repository's root `LICENSE` for its terms. The Windows code under `windows/Clipori.Windows/` is a new platform-specific implementation, not a compiled version of the upstream Swift source. The visual patterns and interaction design were studied from upstream `Sources/CliporiMac/ShelfView.swift` and README.

Changes compared to the macOS project: a C#/.NET WPF shelf and visual theme, Win32 clipboard notifications and global hotkey, Windows direct-paste integration, local SQLite storage, Windows notifications, and a native build workflow. The `windows/` folder is an add-on directory intended for a Fork; it does **not** contain a copy of all upstream sources.

Clipori names and marks are used for descriptive attribution; this is not an official build and is not endorsed by the upstream author.
