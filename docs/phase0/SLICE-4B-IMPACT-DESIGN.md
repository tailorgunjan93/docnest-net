# Phase 1 + Phase 2 — Impact/Risk & Design
## Slice 4b: OpenXML parsers (docx + xlsx)

**Status:** Phases 1–2 (owner approved straight-through to GATE 5) · **Owner:** Gunjan
**ADR:** [ADR-0005](../adr/0005-openxml-parsers.md) · **Scope:** docx + xlsx together; **full** xlsx multi-table heuristic.

---

## Phase 1 — Impact & Risk
**Blast radius:** additions to `DocNest.Parsers` only (+ `DocumentFormat.OpenXml`). No Slice-1/2/3/4
changes beyond two new `ParserFactory` registrations; no `.udf` change.

**Watch-items / mitigations (this is the Med-risk slice):**
1. **docx merged-cell expansion** (`gridSpan` horizontal, `vMerge` restart/continue vertical) — OpenXML
   does **not** auto-repeat (python-docx does). Mitigation: an isolated, unit-tested `DocxTable.ToGrid`
   built + tested **before** the parser uses it; dedicated merged-cell cases. **Flagship "complex tables".**
2. **xlsx low-level cell reading** — shared-strings indirection, sparse rows, cell types. Mitigation: an
   isolated `XlsxReader` that densifies each row by column-letter and resolves shared strings, unit-tested.
3. **xlsx logical-table heuristic parity** — header-after-numeric split + merged-title-row skip.
   Mitigation: faithful port + the Python case table.
**Risk Med → Low** by building/testing the two readers in isolation first. Impact Low (additive).

## Phase 2 — Design
**DSA:** docx body walk O(blocks); grid expansion O(cells). xlsx O(cells) read + O(rows) split. Linear;
memory O(document).

**SOLID / patterns:** one parser per format (SRP); both `IParser` (Strategy) registered in the Factory
(OCP); `DocumentFormat.OpenXml` lives only inside the parsers + internal readers (wrapper/DIP).

**Code plan (signatures):**
- `Office/DocxTable.cs` (internal) — `static List<List<string>> ToGrid(Table table)` (gridSpan + vMerge expansion, width-normalised).
- `Office/DocxParser.cs` — `IParser`; ordered `body.Elements()` walk; `HeadingLevel(styleId)` (regex
  `^heading\s*([1-6])$` on the id + `title`→1/`subtitle`→2); `IsPseudoHeading(paragraph)` (ALL-CAPS / >50 %
  bold / colon label ≤100); `Introduction`/`Tables` fallbacks.
- `Office/XlsxReader.cs` (internal) — `static IReadOnlyList<(string Name, List<List<string>> Rows)> ReadSheets(string path)`
  (shared strings, column-letter densify, cached `<v>`/inline/bool cell text).
- `Office/ExcelParser.cs` — `IParser`; per sheet → logical-table split (`SplitIntoTables`,
  `LooksLikeHeader`, `IsMostlyNumeric`, `IsNumeric`) → `BuildTable` (width-norm) → text summary.
- `ParserFactory` ctor also registers `DocxParser`, `ExcelParser`.

**Fixtures (design decision):** rather than commit opaque binaries, the **tests build** the `.docx`/
`.xlsx` in a temp dir with the OpenXML SDK (known headings, a `vMerge`+`gridSpan` table, two stacked
sheet-tables, shared strings + numbers), then parse them. Fully reproducible, diffable, no binary blobs.
The test project references `DocumentFormat.OpenXml` for this. (Supersedes the committed-generator idea.)

**Resolved open questions:** Q1 → together; Q2 → match style **id** via regex (no styles.xml resolution);
Q3 → **full** multi-table heuristic; Q4 → build fixtures **in-test** (not committed binaries).
