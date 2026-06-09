# Phase 0.3 — QA / User Document
## Slice 4b: OpenXML parsers (docx + xlsx)

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### What "working" means
A user points DocNest at a real `.docx`/`.xlsx` and gets faithful sections + aligned tables that flow
to a `.udf` — matching the Python parsers, including merged-cell tables (Word) and multi-table sheets (Excel).

### Test plan (test-first — written in Phase 3, failing first)

**Unit — DocxParser**
- U1: `Heading 1`/`Heading 2`/`Heading 3` styles → sections at levels 1/2/3 in document order.
- U2: a table placed between two headings appears inside the **correct** section (ordered body walk).
- U3 (complex tables): a table with `gridSpan` (horizontal merge) and `vMerge` (vertical merge) →
  a rectangular `TableData` with the merged value **repeated** across covered cells (no drift, no dedup).
- U4: pseudo-headings — an ALL-CAPS line, a >50 %-bold line, and a `Label:` line each start a section
  (title has the trailing `:` stripped); a normal sentence does not.
- U5: content before the first heading → an "Introduction" section; `List` paragraphs → `- ` bullets.
- U6: title precedence — core-properties title, else first `Title`/`Heading 1`, else filename.
- U7: `Supports(".docx")` true; `.doc` false.

**Unit — ExcelParser**
- U8: two sheets → two sections titled by sheet name; an empty sheet is skipped.
- U9: one sheet with two stacked tables (blank row between) → two `TableData` (logical-table split).
- U10: header detection / numeric-data heuristic — a label row after a numeric data row starts a new
  table; a text data row (`Alice,30,NYC`) does **not** falsely split.
- U11: a leading merged-cell title row (single populated cell spanning the sheet) is skipped; headers
  start at the first multi-cell row; single-column sheets keep row 0 as header.
- U12: row width normalisation (pad/truncate) to header count; trailing empty headers trimmed.
- U13: `.xls` → `ParseException` (clear message); a no-data-sheet file → `ParseException`.
- U14: `Supports(".xlsx")` and `.xls` (routed) true.

**Integration**
- I1: `.docx` and `.xlsx` fixtures → `factory.Get(path).ParseAsync` → `DocNestPipeline.Process` →
  `Document` with §ids; → `.udf` round-trip loss-free (regression tie-in).
- I2 (cell types): an `.xlsx` mixing shared-string text, inline numbers, and a boolean reads correctly.

### Fixtures (binary → generated reproducibly)
A committed generator builds `sample.docx` (Heading 1/2, a paragraph, a `vMerge`+`gridSpan` table, an
ALL-CAPS pseudo-heading) and `sample.xlsx` (two sheets; one sheet with two stacked tables; a merged
title row; mixed cell types). Generator + outputs committed under `tests/fixtures/` + `tools/`.

### Edge / negative cases
- A docx whose only content is a table (no headings) → a single "Tables"/"Introduction" section.
- A docx using `Heading1` style **id** but a renamed display name — must still detect level 1 (Q2).
- An xlsx with a fully empty sheet, a single-column sheet, and ragged rows.
- Non-ASCII throughout (headings, cells) → survives into the `.udf` (ties to Slice-2 encoding).
- A corrupt/truncated OpenXML package → `ParseException`, not an unhandled crash.

### What constitutes a regression (regression-suite seeds)
- **Merged-cell drift** in a docx table — columns misalign or a merged value isn't repeated (the
  flagship "complex tables" target). **Highest severity.**
- A heading level wrong, or a pseudo-heading mis-classified.
- xlsx logical-table split firing on a text data row, or failing to split two real tables.
- A shared-string cell read as its index, or a sparse row mis-densified (column shift).
- `.xls` silently accepted, or a parser populating `Section.Id`.
