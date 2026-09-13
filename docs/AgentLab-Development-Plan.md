# ModuLens Development Plan

本文件將 `PRD.md` 的產品需求轉換為可逐階段驗收的開發順序。產品範圍與功能定義以 PRD 為準；`Goal.txt` 提供目前狀態的短版摘要。

## 1. Current Status

- PRD v0.2 已建立 V1/V2 邊界與 section parser contract。
- Milestone 1 — Source Model：完成。
- Milestone 2 — Section Parser and CLI：完成。
- Milestone 3 — Read-only Module Explorer：完成。
- Milestone 4 — Editable Modules：完成。
- Milestone 5 — Git Module Status：完成。
- 下一個未開始階段：Milestone 6 — Module Diff。

已完成不代表已支援 per-module detailed diff、GUI graph、AST 或 repository analysis。任何階段都不因 roadmap 上相鄰而自動開始。

## 2. Architecture Boundaries

```text
ModuLens.Core
  SourceDocument / SourceSection / SourceRange
  SectionParser
  SectionComparer / IGitService contract
  不依賴 UI、Git process、filesystem write 或特定 AI provider

ModuLens.Cli
  負責讀取使用者指定的檔案
  呼叫 Core 並輸出 section summary

ModuLens.App
  .NET 10 WPF editable desktop shell
  負責 file dialog、checked filesystem save 與 module selection UI
  透過 ViewModel 將 Core sections 投影成 module list 與 editor
  透過無 shell 的 local Git adapter 讀取 HEAD file version

Future adapters
  telemetry 與其他外部整合
```

Core 的輸入是 caller 提供的 path metadata 與 source text。Section parser 不存取 filesystem，也不修改 source text。

## 3. Established Source Contract

- Offsets 採 zero-based half-open interval：`[StartOffset, EndOffset)`。
- 非空 range 的 public lines 採 1-based inclusive。
- 空 range 使用相同 offsets、`IsEmpty == true`、`EndLine == StartLine - 1`。
- `FullRange` 由完整 heading 起點延伸至下一 heading 前或 EOF。
- `HeaderRange` 包含完整 heading JSDoc block。
- `ContentRange` 保存 header 後的 raw source content，包括格式空白。
- `HeaderRange` 與 `ContentRange` 必須精確組成 `FullRange`。

目前 parser 只支援 PRD 0.2 定義的 structured JSDoc heading grammar。`sample.js` 是代表性 integration fixture；synthetic fixtures 才是 parser edge cases 的 authoritative contract。

## 4. V1 Delivery Sequence

| Milestone | Scope | Acceptance gate | Status |
|---|---|---|---|
| 1 — Source Model | Immutable document, section, and range models | Range semantics and source preservation covered by deterministic tests | Complete |
| 2 — Section Parser | Structured JSDoc detection and minimal CLI | Synthetic tests pass; `sample.js` produces 21 sections and representative ranges | Complete |
| 3 — Module Explorer | Read-only WPF module list, selection, and content source view | `sample.js` shows 21 modules; selecting `Transform` displays only its `ContentRange` | Complete |
| 4 — Editable Modules | Range editing, reparse, encoding-aware checked save | One module can be edited without changing unrelated text | Complete |
| 5 — Git Module Status | Working tree vs HEAD projected onto sections | Modified modules are identified deterministically | Complete |
| 6 — Module Diff | Per-module HEAD/current comparison | A changed module opens a detailed side-by-side diff | Planned |
| 7 — Relationship Graph | Basic module graph and synchronized selection | Module list, editor, graph, and Git status navigate consistently | Planned |

After functional V1, a separate V1.1 hardening stage may cover larger inputs, duplicate headings, malformed-input diagnostics, save safety, and measured performance. These concerns should be implemented in the earliest milestone that actually requires them rather than pre-built speculatively.

## 5. V2 Direction

V2 begins only after the single-file workflow is reliable. The planned order is:

1. Repository model and explorer.
2. Repository indexing.
3. Cross-file AST, scope, symbol, and relationship analysis.
4. Repository Git review projected onto logical modules.
5. Impact analysis.
6. Automatic module inference grounded in deterministic analysis signals.

## 6. Stage Completion Rules

Before marking a stage complete:

1. Define its contract, failure behavior, and acceptance examples.
2. Keep Core provider- and adapter-neutral.
3. Add deterministic tests appropriate to the risk.
4. Run build, tests, and a representative smoke test.
5. Update PRD or an ADR if implementation changes an established contract.
6. Update the daily worklog with the actual result and current user operation guide.
7. Commit only the files belonging to that independently reviewable stage.
