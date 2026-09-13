# ADR 0002: Use immutable edit snapshots and checked save

## Status

Accepted for Milestone 4.

## Context

The source file remains authoritative, while module editing exposes only one
`ContentRange` at a time. Copying every module and rebuilding the file later
would create synchronization and source-preservation risks. Saving decoded text
also needs a defined encoding and external-change policy.

## Decision

- Core replaces one exact `ContentRange` in the complete text and reparses it
  into a new immutable `SourceDocument`.
- The WPF editor buffer is transient. It is committed before selection changes
  and before save.
- The App filesystem adapter retains a recognized BOM, treats no-BOM input as
  strict UTF-8, and rejects invalid input rather than silently changing it.
- The adapter hashes the loaded bytes and refuses to save when current bytes no
  longer match.
- A save writes a same-directory temporary file and then replaces the source
  path. The App keeps edits dirty if writing fails.

## Consequences

Unrelated source text and current section headings are not reconstructed.
Ranges always describe the latest parsed snapshot. External edits cannot be
silently overwritten. Milestone 4 does not provide conflict merging, backup
history, legacy code-page detection, or stronger filesystem durability
guarantees.
