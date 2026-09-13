# ADR 0004: Use Aligned Full-Range Lines for Module Diff

## Status

Accepted for Milestone 6.

## Context

Milestone 5 identifies changed logical sections but does not explain their
line-level differences. The first diff view must remain deterministic, preserve
public source line numbers, show heading changes, and keep Core independent of
WPF and local Git execution.

## Decision

`SectionDiffer` compares the complete `FullRange` of the HEAD and working
sections supplied by `SectionComparer`. Lines are compared ordinally after line
terminators are removed for display; the original documents and ranges are not
modified.

The differ uses a longest-common-subsequence table to anchor unchanged lines.
Between anchors, HEAD-only and working-only lines are paired in source order as
`modified`; unpaired lines are `removed` or `added`. Every displayed line number
is the original document's 1-based absolute line number. Added and removed
sections use an absent placeholder on the missing side.

The WPF view renders both sides in one aligned row list. Vertical scrolling is
therefore inherently synchronized. Changed module selection opens Diff; Current
remains available for editing. Previous/Next moves through changed modules
without wrapping.

## Consequences

- Heading description changes are visible because comparison uses `FullRange`.
- The UI does not require Git process or parsing logic of its own.
- The first implementation is a readable line diff, not a token-, word-, AST-,
  or move-aware diff.
- The dynamic-programming table uses `O(head lines × working lines)` memory and
  time. Measured large-input hardening remains a V1.1 concern.
- A multi-line replacement is paired positionally within its changed hunk; this
  is deterministic but does not claim semantic correspondence.
