# Clipori for Windows v0.3.0-preview.1

**Unofficial C# WPF port** of Clipori, originally by Panzer-Jack (Apache-2.0).
This is an **unsigned pre-release** with green Windows CI, but desktop application acceptance testing remains necessary.

## Main updates
- **Paste bug fix:** The uploaded diagnostic log confirmed `SendInput` failing with Windows error **87**. Fixed native Windows x64 `INPUT` ABI layout to 40 bytes, with 8-byte union offset, plus regression checks.
- Default to **single-click copy + detail viewer**, **double-click paste**. Earlier v0.2 settings automatically migrate to this default. Set single-click paste back on if desired.
- Detail window includes full text, notes, category, timestamp, source, and a zoomable image preview (25–600%, Ctrl+mouse wheel).
- Configurable global summon shortcut (Ctrl/Alt/Shift + A–Z/F1–F12) and configurable local search/new-note/pause commands, with duplicate/conflict detection and automatic rollback when global registration fails.
- Faster local search: cached normalized per-item index, 190 ms debounce, UI virtualized for horizontal card list, hard limit of 70 displayed matches, and fuzzy query ranking.
- Search handles exact substrings and typo tolerance (bounded edit distance) as well as Chinese skipped-character patterns, e.g. `不清楚` matching `不是很清楚`.
- Search highlights matched characters in card titles and excerpts.

## Limitations and privacy
- Fuzzy **text search** is not semantic/vector AI search. It does not translate or infer conceptual synonym meanings.
- Search checks a bounded part of long content to keep latency predictable. Search ranking and highlighted text may differ for unusual Unicode normalization and typo patterns.
- Image zoom is native WPF image rendering; very large images may consume significant memory. File path history is not an archive.
- Windows elevated/protected applications may reject synthetic paste. `SendInput` success proves injected key events, not completed target text insertion.
- Logs: `%LOCALAPPDATA%\\CliporiWindows\\logs`. Back up the `%LOCALAPPDATA%\\CliporiWindows` directory before updating.

## Testing
Windows x64 CI compiles WPF, tests native `INPUT` layout, classifier, fuzzy search, shortcut parsing, and DPAPI encryption. On-device mouse double-click, image zoom, multi-monitor/DPI and application-level paste must be tested by users.
