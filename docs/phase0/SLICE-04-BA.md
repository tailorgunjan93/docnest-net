# Phase 0.1 — BA / Functional Document
## Slice 4: Text-format parsers (md / html / csv) + ParserFactory

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan · **Depends on:** Slices 1–3 (✅)

---

### WHY
Until now the pipeline consumes a `RawDocument` built by tests. To process **real files**, DocNest
needs **parsers** — Stage 1: turn a file into a `RawDocument` (heading-delimited sections + tables),
leaving §ids to the normaliser. This slice delivers the three **dependency-light text formats**
(`md`, `html`, `csv`) and the **`ParserFactory`** that routes a file to the right parser. Heavier
formats (OpenXML `docx`/`xlsx`, `pdf`) are **separate downstream slices** — different dependencies,
different risk — so this slice stays Low/Low and gets us to "real files in → `.udf` out" soonest
(reaching milestone **M2** once a real file round-trips into a Python-readable `.udf`).

### WHAT — exact functional behaviour
1. **`MarkdownParser`** (`.md`, `.markdown`, pure BCL): line-by-line ATX heading scan (`#`…`######`);
   body text accumulated per section; **fenced code blocks** (` ``` ` / `~~~`) don't yield headings;
   text before the first heading → a preamble section; first H1 → doc title; no headings → one
   section; empty file → one empty section.
2. **`CsvParser`** (`.csv`, `.tsv`, pure BCL): delimiter auto-detect (`,` `\t` `;` `|`; `.tsv` = tab);
   encoding cascade (UTF-8-BOM → UTF-8 → Latin-1); first non-empty row → headers (trailing empties
   trimmed); remaining rows → one `TableData` (row widths normalised); one section + a plain-text
   table summary; empty/header-less file → `ParseException`.
3. **`HtmlParser`** (`.html`, `.htm`): walk `h1`–`h6` for the section tree; collect sibling text +
   `<table>` elements until the next heading; **expand `rowspan`/`colspan`** into a dense grid;
   title from `<title>` → first `<h1>` → filename; no headings → one section from body text.
4. **`ParserFactory`**: ordered registry of `IParser`; `Get(path)` → first parser whose `Supports`
   matches (else `UnsupportedFormatException`); `Supports(path)`; `Register`/`Unregister` at runtime.

**Before vs after**
- *Before:* `RawDocument`s are hand-built in tests.
- *After:* `factory.Get("notes.md").ParseAsync("notes.md")` → a real `RawDocument`; the full path
  `parse → normalise → enrich → .udf` works on actual `.md`/`.html`/`.csv` files.

**Acceptance criteria**
- AC1: Markdown heading hierarchy, fenced-code handling, preamble, title fallback, no-heading and
  empty-file behaviours match Python `MarkdownParser`.
- AC2: CSV/TSV delimiter detection, encoding cascade, header/row normalisation, and the table summary
  match Python `CSVParser`; empty/header-less → `ParseException`.
- AC3: HTML heading hierarchy, table extraction **with rowspan/colspan grid expansion**, and title
  resolution match Python `HTMLParser`.
- AC4: `ParserFactory` routes by extension (first match wins); unknown format → `UnsupportedFormatException`;
  runtime `Register`/`Unregister` work.
- AC5: parser output flows through Slice-3 pipeline → Slice-2 `.udf` and round-trips (regression tie-in).
- AC6: parsers leave `Section.Id = ""` (the normaliser owns §ids); `DocId` via `DocId.FromPath`.

### Non-goals (this slice)
- No `docx`/`xlsx` (OpenXML) — next slice.
- No `pdf` (PdfPig) — the slice after.
- No OCR, no image extraction to assets, no library/multi-doc.

### HOW — scenarios
- *Markdown:* a README with `#`/`##` + a fenced code block containing `# not a heading` → correct tree.
- *CSV:* a `;`-delimited file with a BOM → headers + rows; a `.tsv` → tab-split.
- *HTML:* a page with a `<table>` using `rowspan` → a rectangular `TableData`.
- *Factory:* `Get("x.unknown")` → `UnsupportedFormatException` listing supported formats.
- *Edge:* empty `.md`; header-only CSV; HTML with no headings; mixed-width CSV rows.

### Traceability
Serves **Reliability** (structure-first parsing) and **Cost/Privacy** (local, dependency-light).
Gets DocNest to real-file ingestion (M2) without taking on the heavy OpenXML/PDF risk yet.
