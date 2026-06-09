# Phase 0.1 — BA / Functional Document
## Slice 4b: OpenXML parsers (docx + xlsx)

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan · **Depends on:** Slices 1–4 (✅)

---

### WHY
DocNest must ingest the two most common office formats: Word (`.docx`) and Excel (`.xlsx`). Both are
ZIP+XML (OpenXML) and read with one MIT dependency (`DocumentFormat.OpenXml`). They extend the
parser coverage from Slice 4 toward **M2** (real office docs → Python-readable `.udf`). Word brings
the Charter "complex tables" hardening target into play (merged cells).

### WHAT — exact functional behaviour
1. **`DocxParser`** (`.docx`): walk the document **body in order** (paragraphs + tables, so a table
   appears in the section it belongs to). Headings from Word **styles** (`Heading 1`–`Heading 6`,
   `Title`→1, `Subtitle`→2, plus localised `heading N`). **Pseudo-headings** when no style is used:
   ALL-CAPS line, >50 % bold, or a `colon:` field label (≤100 chars). Normal/List paragraphs append
   to the current section (`List` styles → `- ` bullets). Content before the first heading →
   "Introduction". Tables → `TableData` (**merged cells expanded** via `gridSpan`/`vMerge` so columns
   stay aligned). Title from core properties → first `Title`/`Heading 1` → filename.
2. **`ExcelParser`** (`.xlsx`; `.xls` routed here → clear `ParseException`): each **worksheet → one
   Section**; rows split into **logical tables** (a new header row after predominantly-numeric data
   starts a new table); merged-cell title rows skipped; first row = headers (trailing empties
   trimmed); rows normalised to header width; a text summary per table for retrieval; empty sheets
   skipped; a file with no data sheets → `ParseException`.
3. Both register in `ParserFactory` (Slice 4) so `factory.Get("report.docx")` just works.

**Before vs after**
- *Before:* only md/html/csv parse.
- *After:* `.docx` and `.xlsx` ingest end-to-end (`parse → normalise → enrich → .udf`).

**Acceptance criteria**
- AC1: docx heading levels from styles match Python; pseudo-heading detection (ALL-CAPS / bold / colon)
  matches; paragraphs/tables appear in document order.
- AC2: docx table extraction expands `gridSpan`/`vMerge` into an aligned rectangular `TableData`
  (the "complex tables" target) — no column drift, no dedup of legitimately repeated values.
- AC3: xlsx each sheet → a Section; logical-table split heuristic matches Python on the case table;
  header/row normalisation matches; `.xls` → `ParseException`.
- AC4: titles resolved per the documented precedence; parsers leave `Section.Id = ""`.
- AC5: output flows through the pipeline → `.udf` and round-trips (regression tie-in).

### Non-goals (this slice)
- No `.doc`/`.xls` (legacy binary) parsing — clear error only.
- No images/embedded objects to assets; no charts; no cell styling/formatting.
- No PDF (Slice 4c).

### HOW — scenarios
- *docx:* a report with `Heading 1`/`Heading 2` + a table with a vertically-merged first column → a
  section tree with an aligned table. A claim form using ALL-CAPS labels (no styles) → pseudo-heading sections.
- *xlsx:* a workbook with two sheets, one sheet holding two stacked tables separated by a blank row → two sections, the multi-table sheet yielding two `TableData`.
- *Edge:* a docx with a table before any heading → "Tables"/"Introduction" section; an empty sheet; `.xls` → error.

### Traceability
Serves **Reliability** + the **complex-tables** hardening target; keeps **Cost/Privacy** (local, one
MIT dep). Extends real-file coverage toward M2.
