# Windows Clipboard Workspace — Pin / Star roadmap

> Working project name only. Unofficial Windows port inspired by [Clipori](https://github.com/Panzer-Jack/Clipori). No endorsement or trademark rights implied. Updated 2026-10-14.

## Product decision

- **Pin**: a non-destructive property of clipboard history. It keeps a useful clip in the clipboard UI and exempts it from ordinary retention rules. It does not automatically export or sync content.
- **Star**: an explicit, independent durable-knowledge archive action. One clip can be both pinned and starred. A successful Star must commit a durable local snapshot, irrespective of remote sync success.
- Unstarring removes the Star selection; by default it **does not delete** exported files or remote notes. Destructive deletion must be a separate, explicit operation.

## Proposed pipeline

```text
Windows Clipboard / Browser extension
      -> type detection / source / privacy checks
      -> local SQLite (existing; DPAPI protection)
      -> independently:
          Pin -> retention exemption + quick paste (existing)
          Star -> archive job + snapshot (planned)
               -> user-selected Knowledge Vault folder
               -> one Markdown note per Star + optional index/daily aggregation
               -> outbox -> optional destination adapters:
                    FileSystem / Obsidian Vault / Notion API
               -> selective AI context adapter (opt-in only)
```

This is a planned target architecture, not a claim of implementation.

## Star data contract (proposal)

- `starred_clips`: `star_id`, `clip_id`, `snapshot_hash`, `created_utc`, `starred_utc`, `source_app`, `source_url`, `title`, `kind`, `tags_json`, `archive_path`, `archive_state`, `privacy_level`.
- `sync_outbox`: `job_id`, `star_id`, `destination_id`, `operation`, `attempt_count`, `next_retry_utc`, `last_error`, `state`.
- `sync_mappings`: `star_id`, `destination_id`, `external_item_id`, `content_version`, `last_synced_utc`.
- Stable IDs and content hashes allow retries without duplicate Notion pages. Different sync destinations must not block local archiving.

## Markdown behavior (proposed)

- Default: one Markdown file per Star with YAML frontmatter including `id`, `source`, `timestamps`, `kind`, `tags`, plus original content and user notes.
- User chooses any writable local Knowledge Vault folder. Support an Obsidian vault directory without requiring Obsidian.
- Optionally build a generated `index.md`, one daily Markdown digest, or a combined export. Do **not** rely exclusively on appending to a single giant mutable Markdown file.
- Atomic temporary file + rename, filesystem-name normalization, path traversal protection, network folder failure and conflict handling, backup/import, and readable error feedback.
- For images and files: store assets under `assets/` with links; do not confuse Windows clipboard file-path references with actual copied file content.

## Privacy first

Windows history currently encrypts text and other record fields using DPAPI. Once exported to a Markdown file, the data may be **plaintext**. Show clear disclosure before first export, suggest a protected local folder, allow excluding apps/sensitive content, and never auto-send all clipboard history to external AI or Notion.

AI context access should operate on a user-approved selection (Star subset or explicit query), preserve source citations, bound context size and tokens, and treat retrieved text as untrusted input.

## Integrations — decision record

| Candidate | Where it fits | Licensing note |
|---|---|---|
| [Obsidian Web Clipper](https://github.com/obsidianmd/obsidian-clipper) | Markdown vault / templates / AI extraction inspiration | MIT for code; trademarks and marketing assets excluded |
| [MarkDownload](https://github.com/deathau/markdownload) | HTML/Readability -> Markdown clipping patterns | Apache-2.0; verify redistributed notices |
| [AtomicClipper](https://github.com/gauthierae/AtomicClipper) | Region selection and markdown snippets | MIT |
| [Karakeep](https://github.com/karakeep-app/karakeep) | AI tagging, archive architecture, API integrations | AGPL-3.0; do not copy into closed-source components without license review |
| [SingleFile](https://github.com/gildas-lormeau/SingleFile) | Full HTML archive reference | AGPL-3.0 |
| [Linkwarden](https://github.com/linkwarden/linkwarden) | Collaborative archive/reference integration | AGPL-3.0 |

Recommendation: ship an independent, thin WPF Star export engine first; treat browser capture as a separate browser extension/local import path; integrate existing services over APIs where practical.

## Roadmap progress data

The status source for the product site is [project-site/roadmap.json](../project-site/roadmap.json). Update it after a verified feature milestone:

- `implemented`: feature code exists and relevant automated checks have passed; **not** proof of Windows GUI behavior.
- `validation`: code exists but needs on-device acceptance tests.
- `planned`: approved planned work not yet implemented.
- `research`: exploratory design, no delivery commitment.

GitHub API provides real-time release/build/commit metadata; it cannot itself determine whether a feature is finished. This JSON is intentionally versioned with the Windows branch and can feed a ChatGPT Site.

## Current priority

**M1** manually verify paste, tray, multi-monitor and sensitive clipboard handling on Windows 10/11; **M2** Star local archiving MVP; **M3** Notion / Obsidian and browser clipping adapters; **M4** opt-in AI knowledge organization. The actual public build is [v0.2.0-preview.1](https://github.com/wu0ming/Clipori/releases/tag/v0.2.0-preview.1).
