# ADR 0001: Use WPF for the Initial Desktop Shell

- Status: Accepted
- Date: 2026-09-13
- Scope: Milestone 3 — Read-only Module Explorer

## Context

ModuLens V1 needs a local desktop shell that can open a JavaScript file, show detected logical modules, and display the selected module's source. The Core model and parser must remain independent of UI technology. The initial target environment is Windows with .NET 10, and Milestone 3 should not introduce editing, Git, graph, or repository-analysis scope.

## Decision

Use .NET 10 WPF for `ModuLens.App`.

- WPF provides the required file dialog, list selection, and read-only source view without third-party UI packages.
- `ModuLens.App` owns filesystem reads and Windows UI concerns.
- `ModuLens.Core` remains UI- and filesystem-neutral.
- `MainWindowViewModel` converts a parsed `SourceDocument` into read-only presentation state and is tested without showing a window.
- The source pane displays `SourceSection.ContentRange`, while the UI also reports content and full line ranges.

## Consequences

- The first desktop shell is Windows-specific.
- Core can still be reused by another UI technology later.
- WPF code remains at the adapter boundary and must not leak into Core.
- Cross-platform UI selection is deferred and should be made only when product requirements justify it.
- Milestone 4 may extend the presentation layer for editing, but save and conflict semantics must be defined before enabling writes.
