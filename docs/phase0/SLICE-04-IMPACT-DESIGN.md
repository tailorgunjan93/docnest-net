# Phase 1 + Phase 2 — Impact/Risk & Design
## Slice 4: Text-format parsers + ParserFactory

**Status:** Phases 1–2 (owner approved straight-through to GATE 5) · **Owner:** Gunjan
**ADR:** [ADR-0004](../adr/0004-text-parsers-and-html-library.md) · **Libraries:** AngleSharp + zero-dep CSV.

---

## Phase 1 — Impact & Risk
**Blast radius:** new `DocNest.Parsers` assembly only. No changes to Slices 1–3 or the `.udf` schema.

| Area | Change | Breaking? |
|---|---|---|
| `DocNest.Parsers` (new) | `MarkdownParser`, `CsvParser`, `HtmlParser`, `ParserFactory`, internal CSV reader | No — new |
| Dependency | **AngleSharp** (MIT) — HTML only, behind `IParser`; centrally pinned | New dep (isolated) |
| Tests | per-parser + factory + pipeline/`.udf` tie-in + fixtures | No — new |

**Watch-items / mitigations:**
1. **HTML table grid parity** (rowspan/colspan) — the Charter "complex tables" target. Mitigation:
   port `_expand_grid` verbatim; dedicated grid tests.
2. **CSV quoting/delimiter parity** — hand-rolled RFC-4180 + delimiter heuristic. Mitigation: case
   tests (quoted delimiters, escaped `""`, ragged rows, `;`/`|`/tab). *Known minor divergence:* Python
   loses embedded newlines (via `splitlines`); ours preserves them inside quotes (arguably better) —
   documented, not asserted for byte-parity.
3. **Encoding cascade** — BOM/UTF-8-strict/Latin-1. Mitigation: explicit cascade + a Latin-1 fixture.
4. **AngleSharp behind the wrapper** — never referenced from `Core`; only inside `HtmlParser`.

**Risk = Low, Impact = Low** — one mature MIT dependency (HTML only), additive, parity-tested.

## Phase 2 — Design
**DSA:** Markdown O(lines); CSV O(bytes) state-machine parse + O(rows×cols) normalise; HTML O(nodes)
DOM walk + O(cells) grid. Linear; memory O(document) (true streaming for huge files deferred to the
heavy-format slices).

**SOLID / patterns:**
- **SRP** — one parser per format; `ParserFactory` only routes.
- **OCP / Factory + Registry** — `ParserFactory` holds an ordered `IParser` list; `Register`/`Unregister`
  add formats without edits (docx/xlsx/pdf register here in 4b/4c).
- **DIP / wrapper** — `DocNestPipeline` (Slice 3) already injects `IParser`; AngleSharp lives only
  inside `HtmlParser`. Parsers depend on `Abstractions` + `Core` (`DocId.FromPath`), never the reverse.
- **Template-ish** — each parser: read → build sections (leave `Id=""`) → `RawDocument`. Sync CPU work
  wrapped as `Task` (no fake async); file reads are real async.

**Code plan (signatures):**
- `DocNest.Parsers/ParserFactory.cs` — `IParser Get(string path)`; `bool Supports(string path)`;
  `void Register(IParser p, int position = 0)`; `void Unregister<T>() where T : IParser`. Defaults:
  Markdown, Csv, Html.
- `MarkdownParser.cs` — `[GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*$")]`; ATX scan + fence tracking.
- `CsvParser.cs` (+ internal `CsvReader` RFC-4180 state machine + `DetectDelimiter` + `DecodeCascade`).
- `HtmlParser.cs` — AngleSharp `ParseDocument`; `QuerySelectorAll("h1,…,h6")`; sibling walk; `ExpandGrid`.
- `ParserText.cs` (internal) — `WordCount`, `TitleCase`, `FilenameToTitle`, `SplitLines`.

**Resolved open questions:** Q1 → text-only scope (docx/xlsx = 4b, pdf = 4c); Q2 → **zero-dep CSV**;
Q3 → **AngleSharp**; Q4 → PDF-engine selection deferred to 4c (factory registry API built now).
