# ModuLens — Product Requirements Document

**Version:** 0.2
**Product Type:** Local developer tool / code comprehension tool
**Initial Platform:** Desktop / local development environment
**Initial Language Focus:** JavaScript / Userscript
**Long-term Scope:** Repository-level source architecture exploration

---

# 1. Product Summary

ModuLens is a developer tool for understanding, navigating, editing, and reviewing source code through **logical modules and relationships rather than raw files alone**.

The initial problem is large single-file JavaScript codebases, especially userscripts, where a file may contain thousands of lines covering multiple responsibilities such as configuration, state management, DOM handling, UI, event handling, browser APIs, and business logic.

Modern AI coding agents such as Codex can modify these codebases much faster than a developer can manually read and reconstruct their architecture.

ModuLens addresses this mismatch.

Instead of presenting only:

```text
script.js
2774 lines
```

ModuLens presents:

```text
script.js

├─ Config
├─ State
├─ Video Detection
├─ Normal Controls
├─ Overlay Lifecycle
├─ Control Bar
├─ Transform
├─ Input
├─ Fullscreen Lifecycle
└─ Bootstrap
```

The developer can select a logical module, inspect its source, edit it, understand its dependencies, and review Git changes at the module level.

The long-term product evolves from:

```text
Large file comprehension
```

into:

```text
Repository architecture comprehension
```

---

# 2. Problem Statement

AI-assisted development has significantly increased code generation and modification speed.

A coding agent may:

* create multiple functions,
* restructure existing logic,
* modify distant regions of a file,
* introduce new dependencies,
* modify several files,
* or perform repository-wide refactoring

within seconds or minutes.

The bottleneck increasingly becomes the human developer's ability to answer:

```text
What changed?

Where is this logic located?

Which responsibility does this code belong to?

What depends on this code?

What did the AI modify compared with the previous version?

Will this change affect other parts of the system?

How is this repository actually structured?
```

Traditional editors primarily expose code through the physical structure:

```text
Repository
└─ File
   └─ Lines
```

ModuLens introduces another representation:

```text
Repository
└─ Logical Module
   ├─ Symbols
   ├─ Dependencies
   ├─ Source locations
   ├─ Changes
   └─ Impact
```

The goal is not to replace VS Code or other IDEs.

The goal is to provide a **code comprehension layer** on top of existing source code.

---

# 3. Product Vision

ModuLens should allow a developer to move naturally between:

```text
Code
↓
Modules
↓
Relationships
↓
Changes
↓
Impact
```

A developer should be able to open unfamiliar or rapidly changing code and quickly build a mental model without reading every line sequentially.

Long term:

> ModuLens should answer "how is this system organized and what changed?" before the developer needs to manually inspect individual files.

---

# 4. Core Product Principles

## 4.1 Source code remains the source of truth

ModuLens must not require proprietary project formats to own the user's source code.

The original source file or repository remains authoritative.

Logical modules are representations of regions and symbols within the source.

---

## 4.2 Logical structure and physical structure are different concepts

Physical structure:

```text
src/
├─ foo.js
├─ bar.js
└─ baz.js
```

Logical structure may be:

```text
Authentication
├─ foo.js: validateToken
└─ bar.js: refreshSession

UI
├─ bar.js: createToolbar
└─ baz.js: renderStatus
```

ModuLens must eventually support both views.

---

## 4.3 Human correction is valid

Automatic module inference does not need to be perfect.

The product may suggest logical grouping based on static analysis, comments, symbol relationships, and heuristics.

The user must eventually be able to:

* rename a logical module,
* merge modules,
* split modules,
* move symbols between logical modules,
* confirm or reject inferred relationships.

---

## 4.4 Progressive sophistication

Do not begin with repository-wide semantic analysis.

The project is deliberately split into:

```text
V1 — Single File
V2 — Repository
```

V1 must already be useful independently.

---

# 5. Target User

Initial target user:

A software developer who:

* works with AI coding agents such as Codex,
* receives large code modifications,
* needs to understand code faster than linear reading allows,
* works with large JavaScript files or scripts,
* cares about architecture and business logic,
* wants visual relationships between responsibilities.

Initial test case:

```text
IG Overlay Video Transformer
~2774 lines
Single JavaScript userscript
```

The file already contains logical areas such as:

```text
Config
State
Debug
Helpers
Style Helpers
Video Detection
Normal Controls
Overlay Lifecycle
Control Bar
Control Actions
Video Events
Transform
Playback Rate
Mouse Drag
Wheel Zoom
Right Click
Keyboard
Fullscreen Lifecycle
Normal Mode Scanner
Page Events
Start
```

The checked-in `sample.js` is a representative V1 integration fixture. It is
intended to prove that the initial parser works on a real, large userscript,
including headings that contain explanatory text.

It must not be treated as the complete definition of valid input. The parser
contract is defined by Section 9 and by focused synthetic test fixtures. This
keeps future files generated or reorganized by developers and coding agents
from being forced to match one historical file exactly.

---

# 6. Product Roadmap

```text
V1
Single-file Code Comprehension
│
├─ Section detection
├─ Module explorer
├─ Module editor
├─ Module-aware Git diff
├─ Basic dependency visualization
└─ Source preservation

              ↓

V2
Repository Comprehension
│
├─ Repository indexing
├─ File/module graph
├─ Cross-file symbol analysis
├─ Repository Git diff
├─ Change impact analysis
└─ Architecture exploration
```

---

# 7. V1 Scope — Single File

## 7.1 V1 Goal

Given a large JavaScript source file, ModuLens should transform the developer experience from:

```text
Read 2774 lines sequentially
```

to:

```text
Browse logical responsibilities
→ select module
→ inspect/edit module
→ inspect relationships
→ inspect Git changes
```

No repository-wide understanding is required in V1.

Within V1, a logical module is the product-facing representation of one
`SourceSection`. A separate persistent module model is not required until a
later version needs modules that span multiple source regions or files.

---

# 8. V1 Functional Requirements

## 8.1 Open Source File

The user can open a `.js` file from the local filesystem.

Example:

```text
sample.js
```

ModuLens loads the file into a `SourceDocument`.

Suggested conceptual model:

```text
SourceDocument
├─ FilePath
├─ Text
├─ Lines
├─ Sections
└─ Version information
```

The tool must not modify the file simply by opening it.

---

# 9. Section Detection

## 9.1 Initial strategy

V1 should detect existing structured JSDoc section headings. The initial
grammar is deliberately narrow, but it supports both compact headings and the
description-bearing heading already present in `sample.js`.

Example:

```javascript
/**
 * ============================================================
 * Video detection
 * ============================================================
 */
```

A heading matches when all of the following are true:

1. The comment begins on a line whose trimmed text is `/**` and ends on a
   later line whose trimmed text is `*/`.
2. After removing indentation and the leading JSDoc `*`, the first non-empty
   content line is a separator made only of at least three `=` characters.
3. The next non-empty content line is the section name. The extracted name is
   trimmed and must not be empty.
4. The final non-empty content line before `*/` is a second separator. Zero or
   more descriptive JSDoc lines may appear between the name and this closing
   separator.

Leading indentation and the separator length do not affect recognition. The
parser extracts a normalized name but must not normalize or rewrite the source
text itself. A comment that only resembles a heading but does not satisfy the
complete grammar is ignored; parsing continues without throwing.

This should produce:

```text
SourceSection

Name:
Video detection

StartLine:
212

EndLine:
379
```

For this example, the section begins at the opening `/**` and ends on the line
immediately before the next detected heading. The final section ends at EOF.

---

## 9.2 No mandatory `@module` annotations

V1 should not require source modifications such as:

```javascript
// @module ...
```

when the existing source structure already provides usable headings.

The parser should first support only the heading grammar in Section 9.1.

Additional comment styles, annotations, and AI-based inference may come later.

---

## 9.3 Source ranges

Logical sections must reference the original `SourceDocument` using source ranges.

Preferred concept:

```text
SourceDocument
      │
      ├── SourceRange
      │      └── Config
      │
      ├── SourceRange
      │      └── Video Detection
      │
      └── SourceRange
             └── Transform
```

Do not create independent copies of every section as the primary source of truth.

This avoids synchronization problems.

For Milestones 1 and 2, use the following range contract:

* Every `SourceRange` uses a zero-based inclusive `StartOffset` and exclusive
  `EndOffset` into the original `SourceDocument.Text`: `[start, end)`.
* For a non-empty range, public `StartLine` and `EndLine` are both 1-based and
  inclusive.
* An empty range has equal offsets, `IsEmpty == true`, and uses
  `EndLine == StartLine - 1`. This represents an insertion point without
  pretending that the range contains a line.
* `FullRange` begins at the start of the line containing the opening `/**` and
  ends immediately before the next detected heading, or at EOF for the final
  section.
* `HeaderRange` begins with `FullRange` and contains the complete heading JSDoc
  block. It includes the closing `*/` line terminator when one is present.
* `ContentRange` begins immediately after `HeaderRange` and ends with
  `FullRange`. It is raw source content, so formatting blank lines after the
  heading are preserved rather than heuristically removed.
* The offsets form an exact partition:
  `FullRange.StartOffset == HeaderRange.StartOffset`,
  `HeaderRange.EndOffset == ContentRange.StartOffset`, and
  `ContentRange.EndOffset == FullRange.EndOffset`.
* Text before the first heading remains part of `SourceDocument.Text` but is
  not represented as a `SourceSection`.
* A heading followed immediately by another heading is valid and has an empty
  `ContentRange`.
* If no headings are found, parsing succeeds with an empty section collection.

For the current `sample.js` snapshot, representative ranges are:

```text
Video detection
  FullRange:    212–379
  HeaderRange:  212–216
  ContentRange: 217–379

RIGHT CLICK
  FullRange:    2079–2230
  HeaderRange:  2079–2100
  ContentRange: 2101–2230
```

Loading and parsing must preserve `SourceDocument.Text` character-for-character
and must not write to the input file. Byte encoding, BOM, atomic save, and
external-edit conflict handling belong to the editable-module milestone and do
not need to be solved in Milestones 1 and 2.

---

# 10. Module Explorer

The application should provide a module navigation panel.

Example:

```text
Modules

Config
State
Debug
Helpers
Style Helpers
Video Detection
Normal Controls
Overlay Lifecycle
Control Bar
Control Actions
Video Events
Transform
Playback Rate
Mouse Drag
Wheel Zoom
Right Click
Keyboard
Fullscreen Lifecycle
Normal Mode Scanner
Page Events
Start
```

Clicking a module selects it.

Selection should update:

* source editor,
* module information,
* relationship visualization,
* Git change state.

---

# 11. Module Editor

Selecting a module should allow the developer to directly inspect and edit the corresponding source range.

Example:

```text
Transform
```

shows:

```javascript
function applyTransform() {
    ...
}

function resetTransform() {
    ...
}

function zoom(delta) {
    ...
}

function move(dx, dy) {
    ...
}
```

Edits must update the underlying `SourceDocument`.

Saving must reconstruct a valid original source file.

---

# 12. Editing Model

The source file is authoritative.

Recommended conceptual model:

```text
SourceDocument
      │
      └── Text Buffer
             │
             ├── Section A range
             ├── Section B range
             └── Section C range
```

When a section changes:

```text
Edit selected range
       ↓
Update SourceDocument
       ↓
Recalculate affected ranges
       ↓
Save complete file
```

For Milestone 4, editing and saving use the following contract:

* The editor modifies only the selected `ContentRange`; the structured heading
  remains outside the editable buffer.
* A pending editor buffer is transient. Moving to another section or saving
  replaces the exact range in the complete source text and reparses the whole
  document into a new immutable `SourceDocument` snapshot.
* The save adapter writes the complete updated document rather than
  concatenating independent module copies.
* Files without a byte-order mark are decoded as strict UTF-8. UTF-8, UTF-16
  LE/BE, and UTF-32 LE/BE byte-order marks are detected and retained.
* Before saving, the adapter compares the current file bytes with the bytes
  loaded by ModuLens. An external change aborts the save without overwriting
  either version; there is no silent last-writer-wins fallback.
* A successful save first writes a temporary file in the source directory and
  then replaces the source path. Failed temporary writes are cleaned up.
* Opening another file or closing the window with pending changes requires an
  explicit discard confirmation.

This is the minimum safe single-file workflow. Advanced conflict merging,
backup/version recovery, additional legacy encodings, and filesystem-specific
durability guarantees remain outside Milestone 4.

Avoid:

```text
Module A copy
Module B copy
Module C copy

→ concatenate later
```

unless implemented purely as a transient editing abstraction.

---

# 13. Module-Aware Git Integration

Git support is a core V1 feature.

The purpose is not merely to reproduce a traditional line-based diff.

The tool should answer:

> Which logical modules changed?

---

# 14. Git Change Overview

When the current source file is tracked by Git, ModuLens should compare the working version against a selectable Git baseline.

Initial baseline:

```text
HEAD
```

For Milestone 5, Git status uses the following contract:

* The App invokes the local `git` executable with an explicit argument vector;
  it does not construct shell command strings. Core depends only on
  `IGitService` and parsed source snapshots.
* Status is refreshed after opening a file and after a successful save. A
  pending editor buffer is committed to the in-memory source before an
  overlapping Git refresh is applied, so refresh cannot discard edits.
* A file outside a Git work tree remains editable and its modules are shown as
  `not compared`.
* A repository without HEAD, or a working file absent from HEAD, marks every
  current module as `added`.
* When HEAD contains the file, sections are matched by exact case-sensitive
  name plus 1-based same-name occurrence index. Full raw `FullRange` text is
  compared with ordinal equality; line locations are not identity.
* Current sections remain in current source order. HEAD-only sections are
  appended as `removed` entries and have no editable current source.
* Changes before the first detected heading are reported in the Git summary as
  source outside detected modules, preventing an all-unchanged module result
  from hiding an unmapped file change.
* Git command or decoding failures do not block source editing. The UI removes
  comparison badges and displays an explicit unavailable message.

Future options may include:

```text
HEAD~1
specific commit
branch
tag
```

The module explorer should visually indicate change status.

Example:

```text
Config                  unchanged
State                   modified
Video Detection         unchanged
Overlay Lifecycle       modified
Control Bar             modified
Transform               modified
Keyboard                unchanged
```

---

# 15. Module Diff Summary

A dedicated Git view should show module-level changes.

Example:

```text
Current File vs HEAD

Modified modules: 4

Overlay Lifecycle
+32 -14

Control Bar
+18 -7

Transform
+9 -3

Playback Rate
+4 -1
```

This allows the developer to immediately identify areas touched by Codex.

---

# 16. Side-by-Side Module Diff

Selecting a modified module opens a detailed comparison:

```text
┌─────────────────────────┬─────────────────────────┐
│ HEAD                    │ Working Tree            │
│                         │                         │
│ function zoom(...)      │ function zoom(...)      │
│ {                       │ {                       │
│   old logic             │   new logic             │
│ }                       │ }                       │
└─────────────────────────┴─────────────────────────┘
```

Required capabilities:

* added lines,
* removed lines,
* modified lines,
* synchronized scrolling where practical,
* module name,
* source range,
* baseline commit information.

---

# 17. Module Diff Navigation

The developer should be able to navigate:

```text
Changed Modules
     ↓
Transform
     ↓
Detailed diff
     ↓
Open current code
```

And:

```text
Detailed diff
     ↓
Next changed module
```

The primary use case is reviewing AI-generated changes.

---

# 18. Structural Diff — Future V1.x

After basic line diff works, ModuLens may detect structural changes such as:

```text
Transform

Added function:
setScale()

Modified function:
zoom()

Removed function:
legacyZoom()
```

This is distinct from raw Git text differences.

Potential representation:

```text
Module: Transform

Symbols
+ setScale()
~ zoom()
- legacyZoom()
```

This should not block initial V1.

---

# 19. Basic Dependency Visualization

V1 should support a simple relationship graph.

Initial dependency information may come from:

1. existing source headings,
2. manually configured relationships,
3. simple static symbol detection.

V1 does not require a full JavaScript semantic engine.

Example:

```text
       Config
          │
          ▼
Helpers → Transform
          │
          ├── Controls
          └── Debug
```

---

# 20. Graph Interaction

Selecting a module in the tree should highlight the same node in the graph.

Selecting a graph node should select the module.

Example workflow:

```text
Click Overlay Lifecycle
          ↓
Editor opens Overlay code
          ↓
Graph highlights dependencies
          ↓
Git panel shows Overlay changes
```

This interaction is central to ModuLens.

---

# 21. Main V1 Layout

Suggested desktop layout:

```text
┌──────────────────┬──────────────────────────────┬───────────────────────┐
│ Module Explorer  │ Source Editor                │ Relationships         │
│                  │                              │                       │
│ Config           │ function ...                 │       Config          │
│ State            │                              │          │            │
│ Video Detection  │                              │          ▼            │
│ Overlay          │                              │      Transform        │
│ ▶ Transform *    │                              │      ↙      ↘        │
│ Keyboard         │                              │ Controls   Debug      │
│                  │                              │                       │
├──────────────────┴──────────────────────────────┴───────────────────────┤
│ Git Changes │ Problems │ Module Info │ Future: Impact                  │
└─────────────────────────────────────────────────────────────────────────┘
```

`*` indicates modified compared with Git baseline.

---

# 22. Git Review Mode

V1 should have an optional dedicated Git Review mode.

Example:

```text
Review Changes

4 modules modified

[Overlay Lifecycle]
[Control Bar]
[Transform]
[Playback Rate]
```

Selecting one opens:

```text
Module summary
+
Side-by-side diff
+
related modules
```

This mode specifically targets the workflow:

```text
Codex modifies code
        ↓
Developer opens ModuLens
        ↓
"Which modules did it touch?"
        ↓
Review only relevant logical areas
```

---

# 23. V1 Non-Goals

V1 should NOT initially attempt:

* entire repository indexing,
* complete ECMAScript semantic correctness,
* full language-server replacement,
* AI-generated architecture descriptions,
* support for every programming language,
* automatic refactoring,
* automatic Git commits,
* branch management UI,
* complete AST dependency resolution,
* build systems,
* package dependency analysis,
* cloud synchronization.

Avoid over-engineering.

---

# 24. Suggested V1 Architecture

Initial recommended technology:

```text
.NET 10 / C#
```

Core architecture:

```text
ModuLens
│
├─ ModuLens.Core
│  │
│  ├─ Documents
│  ├─ Sections
│  ├─ Parsing
│  ├─ Git
│  └─ Relationships
│
├─ ModuLens.App
│
└─ ModuLens.Core.Tests
```

Potential later UI technology can be selected separately.

The core domain must not depend directly on the UI framework.

---

# 25. Suggested Core Domain Model

Conceptual types:

```text
SourceDocument
SourceRange
SourceSection
SectionParser

GitRevision
FileDiff
SectionDiff

ModuleNode
ModuleEdge
DependencyGraph
```

Possible relationships:

```text
SourceDocument
 └─ SourceSection[]

SourceSection
 ├─ SourceRange
 └─ GitStatus

DependencyGraph
 ├─ ModuleNode[]
 └─ ModuleEdge[]
```

---

# 26. Git Service Boundary

Git behavior should be behind an abstraction.

Conceptually:

```csharp
public interface IGitService
{
    Task<GitFileVersion?> GetHeadVersionAsync(string filePath);
    Task<FileDiff> CompareWithHeadAsync(string filePath);
}
```

Implementation may initially call local Git.

Do not tightly couple parsing logic to Git execution.

---

# 27. Section Diff Algorithm

Initial module-aware diff may work as follows:

```text
HEAD version of file
        ↓
SectionParser
        ↓
HEAD sections


Working version of file
        ↓
SectionParser
        ↓
Working sections
```

Then match sections primarily by:

```text
(Exact Section Name, Occurrence Index)
```

The occurrence index distinguishes duplicate names in source order. A renamed
section is represented as one removed section and one added section in V1.
More sophisticated identity matching is deferred.

For each section:

```text
HEAD text == Working text
→ unchanged

HEAD text != Working text
→ modified

only in Working
→ added

only in HEAD
→ removed
```

This intentionally provides semantic grouping before sophisticated AST comparison exists.

---

# 28. Important Edge Case

Section locations may move between versions.

Therefore sections must NOT be compared only by line range.

Bad:

```text
Lines 534–886 vs Lines 534–886
```

Preferred V1:

```text
Section identity:
("Overlay lifecycle", 1)
```

Later:

```text
AST / symbol identity
```

may improve matching.

---

# 29. Testing Strategy

Testing should use two complementary fixture types:

1. Small synthetic fixtures are the authoritative tests for the Section 9
   grammar, range semantics, malformed input, and edge cases.
2. The checked-in `sample.js` is a representative integration fixture for a
   realistic large file. Its current snapshot contains 2,774 lines and 21
   headings, including the description-bearing `RIGHT CLICK` heading.

The integration fixture demonstrates realism but does not restrict all future
JavaScript files to the same names, order, layout, or number of sections.

Tests should include:

### Parser tests

* detects all expected headings,
* extracts correct names,
* produces correct ranges,
* handles final section to EOF,
* handles empty section,
* returns an empty collection when no headings exist,
* ignores a malformed heading candidate and continues,
* supports a heading with descriptive lines before its closing separator,
* preserves the original document text exactly.

### Editing tests

* replacing one section does not alter unrelated text,
* line/range information is recalculated,
* saving reconstructs valid complete text.

### Git tests

Given two versions:

```text
HEAD
Working Tree
```

verify:

* unchanged section,
* modified section,
* added section,
* removed section.

---

# 30. V1 Milestones

## Milestone 1 — Source Model

Deliver:

```text
SourceDocument
SourceSection
SourceRange
```

Acceptance:

A JavaScript file can be loaded without modification.

---

## Milestone 2 — Section Parser

Deliver:

```text
SectionParser
```

Acceptance:

The IG Transformer fixture is converted into named logical sections.

---

## Milestone 3 — Read-Only Module Explorer

Deliver:

```text
Module list
Module selection
Module source view
```

Initial desktop shell:

* .NET 10 WPF on Windows,
* filesystem access and file dialogs remain in `ModuLens.App`,
* parsing and source ranges remain in `ModuLens.Core`,
* the source pane displays the selected section's raw `ContentRange`,
* opening and browsing a file are read-only operations.

Acceptance:

Opening `sample.js` displays 21 modules in source order. Clicking `Transform`
displays only its content source region, excludes the heading comment, shows
its content/full line ranges, and does not modify the file.

---

## Milestone 4 — Editable Modules

Deliver:

```text
Module editor
Save
Range recalculation
```

Acceptance:

Editing one module and saving produces a valid complete userscript.

The selected module is reparsed after editing, unrelated source text remains
character-for-character unchanged, and an external file modification is not
silently overwritten.

---

## Milestone 5 — Git Module Status

Deliver:

```text
Compare current file with HEAD
Changed module detection
```

Acceptance:

Modified modules are visibly identifiable.

The module explorer labels `unchanged`, `modified`, `added`, and `removed`
identities against HEAD. Files without an available Git comparison remain
usable and display `not compared` rather than a fabricated status.

---

## Milestone 6 — Module Diff

Deliver:

```text
Side-by-side module diff
```

Acceptance:

Selecting a changed module displays HEAD vs working source.

---

## Milestone 7 — Relationship Graph

Deliver:

```text
Basic module graph
Module ↔ editor synchronization
```

Acceptance:

Selecting nodes and module entries navigates consistently.

---

# 31. Definition of V1 Complete

V1 is complete when this workflow is reliable:

```text
Open 2774-line userscript
        ↓
See logical modules
        ↓
Click "Overlay Lifecycle"
        ↓
Read/edit only that logical area
        ↓
See which modules changed vs HEAD
        ↓
Click a modified module
        ↓
View detailed Git diff
        ↓
Save
        ↓
Original userscript remains valid
```

At that point the tool already solves a real developer problem.

---

# 32. V2 — Repository Comprehension

V2 expands the source model from:

```text
SourceDocument
```

to:

```text
SourceRepository
```

The objective becomes understanding repository architecture.

---

# 33. V2 Repository Model

Conceptually:

```text
SourceRepository
│
├─ SourceProject
│
├─ SourceDocument[]
│
├─ LogicalModule[]
│
├─ Symbol[]
│
└─ DependencyGraph
```

V2 must understand that one logical module may span several files.

---

# 34. Repository Explorer

Example physical view:

```text
src/
├─ overlay/
│  ├─ lifecycle.js
│  └─ transform.js
├─ controls/
│  └─ toolbar.js
└─ main.js
```

Logical view:

```text
Overlay
├─ Lifecycle
└─ Transform

Playback
├─ Controls
└─ Events

Application
└─ Bootstrap
```

The user should be able to switch between these representations.

---

# 35. Repository Dependency Graph

V2 should progressively identify:

```text
IMPORTS
CALLS
READS
WRITES
LISTENS_EVENT
EMITS_EVENT
USES_DOM
USES_API
```

Example:

```text
main
 │
 ├──→ Overlay
 │       ├──→ Transform
 │       └──→ Fullscreen API
 │
 └──→ Controls
         └──→ Playback
```

---

# 36. AST and Symbol Analysis

V2 may introduce real language parsing.

Processing pipeline:

```text
Source
  ↓
Parser
  ↓
AST
  ↓
Scope Analysis
  ↓
Symbol Table
  ↓
Reference Resolution
  ↓
Dependency Graph
```

For JavaScript:

```text
FunctionDeclaration
CallExpression
Identifier
MemberExpression
AssignmentExpression
ImportDeclaration
ExportDeclaration
```

become meaningful analysis inputs.

---

# 37. Automatic Module Inference

V2 may suggest architecture using multiple signals.

Potential inputs:

```text
Comment headings
Call relationships
Shared state
Import relationships
File location
Naming similarity
Event relationships
DOM usage
External API usage
```

Example:

```text
zoom
move
resetTransform
applyTransform
```

may automatically cluster into:

```text
Transform
confidence: 0.94
```

The developer may accept or modify this grouping.

---

# 38. Repository Git Review

V2 expands Git awareness from:

```text
Which sections changed in one file?
```

to:

```text
Which architectural modules changed in this repository?
```

Example:

```text
Commit abc123

Authentication
  3 files
  7 symbols modified

Checkout
  2 files
  3 symbols modified

UI
  4 files
  12 symbols modified
```

This is more useful for reviewing large AI-generated changes than a flat list of files.

---

# 39. Architecture-Aware Diff

Traditional Git:

```text
12 files changed
+418
-233
```

ModuLens V2:

```text
3 logical areas changed

Authentication
  Token validation modified

Order Processing
  Pricing calculation modified
  DiscountResolver added

UI
  Checkout toolbar updated
```

The goal is not to replace Git.

The goal is to project Git changes onto architecture.

---

# 40. Impact Analysis

Eventually, selecting a symbol or module should answer:

```text
If this changes, what might be affected?
```

Example:

```text
updateControls

Called by:
- installNormalControls
- installOverlayControls
- applyTransform
- changePlaybackRate

Triggered by:
- timeupdate
- durationchange
- loadedmetadata
- play
- pause
- ratechange

Impact:
High
```

Potential graph traversal:

```text
Selected Node
     ↓
Incoming references
     ↓
Transitive callers
     ↓
Modules
     ↓
Affected features
```

---

# 41. Relationship to AI Coding Agents

ModuLens should remain useful without AI.

However, a major use case is:

```text
Human intent
     ↓
Codex modifies source
     ↓
ModuLens analyzes changes
     ↓
Human understands result
```

Possible future integration:

```text
Codex change
      ↓
ModuLens graph delta
      ↓
"This modification touched
3 modules and 11 symbols"
```

This is deliberately postponed until the local analysis model is reliable.

---

# 42. Long-Term Product Direction

Potential evolution:

```text
V1
Single-file module explorer

V2
Repository architecture explorer

V3
Change / impact intelligence

V4
AI-assisted architecture comprehension
```

Potential future capabilities:

* architecture snapshots,
* architecture drift detection,
* dependency cycle detection,
* hotspots,
* high-coupling modules,
* dead symbols,
* change history by logical module,
* commit architecture visualization,
* AI explanations grounded in static analysis,
* IDE integration,
* VS Code extension,
* C# support,
* TypeScript support,
* additional languages.

---

# 43. Product Identity

The key concept behind ModuLens is:

```text
Files tell you where code is stored.

ModuLens tells you how code is organized.
```

And in an AI-assisted development environment:

```text
AI writes faster.

Humans still need to understand.

ModuLens reduces that comprehension gap.
```

---

# 44. Initial Codex Implementation Instruction

Implement this project incrementally.

Do not attempt the full PRD immediately.

Start only with:

```text
Milestone 1
+
Milestone 2
```

Create:

```text
ModuLens.sln

src/
├─ ModuLens.Core
└─ ModuLens.Cli

tests/
└─ ModuLens.Core.Tests
```

Use:

```text
.NET 10
C#
xUnit
```

Implement only:

```text
SourceDocument
SourceRange
SourceSection
SectionParser
```

The first parser only needs to understand the narrow JSDoc heading grammar in
Section 9.1. Both of these shapes are valid:

```javascript
/**
 * ============================================================
 * Section Name
 * ============================================================
 */
```

```javascript
/**
 * ============================================================
 * Section Name
 *
 * Optional descriptive lines.
 * ============================================================
 */
```

Requirements:

* follow the range semantics defined in Section 9.3,
* preserve the original source text character-for-character in memory,
* do not modify the input file,
* do not introduce AST libraries,
* do not implement Git yet,
* do not implement GUI yet,
* do not generalize prematurely,
* use small synthetic fixtures for focused unit tests,
* use `sample.js` as an additional representative integration fixture.

The first runnable result should accept one JavaScript file path and print one
tab-separated row per detected section, in source order:

```text
Name<TAB>StartLine<TAB>EndLine
Config<TAB>14<TAB>71
```

The first row is the header. A valid file with no headings succeeds and prints
only the header. A missing argument, nonexistent file, or unreadable file must
write a clear error to standard error and return a non-zero exit code.

Stop after this milestone and report:

1. files created,
2. architectural decisions,
3. tests implemented,
4. test results,
5. known limitations,
6. suggested next milestone.

Do not proceed to later PRD milestones automatically.
