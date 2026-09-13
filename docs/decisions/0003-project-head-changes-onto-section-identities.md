# ADR 0003: Project HEAD changes onto section identities

## Status

Accepted for Milestone 5.

## Context

Line ranges move as source is edited, so they cannot identify the same logical
module across HEAD and the current file. Git process execution also must not
leak into parser or UI-independent comparison logic.

## Decision

- Core exposes an `IGitService` boundary and compares two parsed snapshots with
  `SectionComparer`.
- A section identity is `(exact name, 1-based same-name occurrence index)`.
- Ordinal `FullRange` text equality determines unchanged versus modified.
- The local adapter asks Git to apply working-tree filters to HEAD content for
  the file path before parsing it. This aligns checkout EOL and encoding
  transformations with the working file instead of treating `core.autocrlf`
  differences as module edits.
- Current-only identities are added; HEAD-only identities are removed. Renames
  therefore appear as one removal and one addition.
- App uses local Git through `ProcessStartInfo.ArgumentList`, with no shell.
- Missing repository state produces `not compared`; missing HEAD content makes
  current modules added. Operational Git failures are explicit but do not
  disable editing.
- Text before the first detected heading is outside module projection and is
  reported separately when it changes.

## Consequences

The result is deterministic and independent of line movement. Duplicate names
remain distinguishable, while renamed or reordered duplicate sections may not
preserve human-intended identity. Per-line diff details, alternate baselines,
conflict merging, and stronger semantic matching remain later work.
