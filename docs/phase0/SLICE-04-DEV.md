# Phase 0.2 — Dev / Technical Document
## Slice 4: Text-format parsers + ParserFactory

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### 1. Python reference — read end-to-end (files traced)
| Python file | Logic | Port target |
|---|---|---|
| `parsers/base.py` `IParser` | `parse`/`supports` + `_make_doc_id` | already have `IParser` (Slice 1) + `DocId.FromPath` |
| `parsers/md.py` | ATX heading scan; fence tracking; preamble; first-H1 title; no-heading & empty fallbacks; `token_count=max(1,words)` | `MarkdownParser` |
| `parsers/csv.py` | encoding cascade; delimiter detect (`Sniffer`/tsv); first-row headers (trailing empties trimmed); row width-norm; `TableData`; text summary | `CsvParser` |
| `parsers/html.py` | `h1`–`h6` walk; sibling collection until next heading; `<table>` → grid with `rowspan`/`colspan` expansion; title `<title>`→`<h1>`→stem | `HtmlParser` |
| `parsers/factory.py` | ordered registry; first-match `get`; `supports`; `register`/`unregister` | `ParserFactory` |

### 2. External-dependency decisions (the crux of this slice)
- **Markdown, CSV → zero dependency (BCL only).** Markdown is a line scan. CSV: .NET has no general
  CSV reader in the BCL we want to lean on; **hand-roll a small RFC-4180 splitter** (quotes, escaped
  quotes, embedded delimiters/newlines) + a delimiter heuristic — keeps the *Cost-Effective / minimal-deps*
  motto. (Alternative `CsvHelper`/`Sep` rejected to avoid a dependency for a small need — open Q2.)
- **HTML → one MIT library.** Recommend **AngleSharp** (W3C-compliant HTML5 parser + real DOM +
  `QuerySelectorAll`, MIT) over HtmlAgilityPack (older, less standards-compliant). Sits behind the
  `IParser` wrapper; never referenced from core. (Open Q3.)
- **Encoding cascade:** detect BOM; try `new UTF8Encoding(false, throwOnInvalidBytes: true)`; on
  `DecoderFallbackException` fall back to `Latin1` (`ISO-8859-1`). Matches Python's
  `utf-8-sig → utf-8 → latin-1`.

### 3. Mapping notes / gotchas
- Parsers return `RawDocument` with `Section.Id = ""`; `DocId.FromPath` (Slice 1) replaces `_make_doc_id`.
- `Section.TokenCount` here is the parser's rough count, but the **normaliser overwrites it**
  (Slice 3) — so parity on the parser's token_count is not load-bearing (assert post-pipeline instead).
- CSV `Sniffer` → a heuristic: count candidate delimiters (`, \t ; |`) outside quotes on the first
  ~8 KB; pick the most frequent consistent one; default `,`. `.tsv` forces tab.
- HTML grid expansion (`_expand_grid`) ports directly: an `occupied` map keyed by `(row,col)`, write a
  spanning cell into every covered cell, skip pre-occupied cells, then densify to `max_cols`.
- `MarkdownParser` async signature: parsing is sync CPU work; wrap as `Task.FromResult` to satisfy
  `IParser.ParseAsync` (no fake async). File reads use `File.ReadAllText*Async`.

### 4. DSA / complexity
- Markdown: O(lines). CSV: O(bytes) split + O(rows×cols) normalise. HTML: O(nodes) DOM walk +
  O(cells) grid. All linear in input; memory O(document) — fine (bounded-memory NFR met; true streaming
  for *large* files is a hardening concern deferred with the heavy formats).

### 5. Layout (new assembly)
```
src/DocNest.Parsers/              # refs Abstractions (+ AngleSharp for HTML)
  ParserFactory.cs
  MarkdownParser.cs
  CsvParser.cs            (+ internal Csv reader/delimiter helpers)
  HtmlParser.cs
tests/DocNest.Parsers.Tests/      # per-parser + factory + pipeline tie-in
  fixtures/ (sample.md, sample.csv, sample.html …)
```
*Decision (ADR-0004): a dedicated `DocNest.Parsers` assembly so AngleSharp stays behind the `IParser`
wrapper and out of `Core`. `DocNestPipeline` (Slice 3) already accepts an injected `IParser`; the
factory can supply one.*

### 6. Backward-compat surface
- Additive: new `DocNest.Parsers` assembly; no Slice-1/2/3 changes; no `.udf` schema change.
- `RawDocument` shape is the contract between parsers and the normaliser (already fixed in Slice 1).

### 7. Open questions (resolve GATE 0 / Phase 2)
- Q1: **Scope** — this slice = text formats (md/html/csv) only, with `docx`/`xlsx` (Slice 4b, OpenXML
  SDK) and `pdf` (Slice 4c, PdfPig) as their own slices? (Recommend yes.)
- Q2: CSV — zero-dep hand-rolled RFC-4180 vs `CsvHelper`/`Sep`. (Recommend zero-dep.)
- Q3: HTML — **AngleSharp** vs HtmlAgilityPack. (Recommend AngleSharp.)
- Q4: `ParserFactory` PDF-engine selection (Python’s `pdf_engine`) — N/A until Slice 4c; the factory’s
  registry API is built now, PDF registration added later.
