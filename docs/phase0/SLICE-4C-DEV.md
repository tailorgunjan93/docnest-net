# Phase 0.2 — Dev / Technical Document
## Slice 4c: PDF parser (text PDFs)

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### 1. Python reference — read end-to-end (files traced)
| Python file | Logic | Port (this slice) |
|---|---|---|
| `parsers/pymupdf_pdf.py` | spans `{text,size,bold,y0}` ordered by y; `_median_font_size`; `_extract_title` (largest); `_build_sections` (heading if `size≥median×1.15` or bold & `≥median×1.05` & `<100`; level by ranked distinct sizes; "Introduction" fallback) | **Yes — text + headings** |
| same — `find_tables`, `_ocr_page`, `_bbox_contains`, `_downscale_png` | table detection + OCR | **Deferred** (no PdfPig table finder; OCR is its own slice) |

### 2. Dependency decision
- **`UglyToad.PdfPig`** (Apache-2.0, pure-managed) — exposes `Page.Letters` with `PointSize`,
  `GlyphRectangle` (bbox), and `Font` (name → bold). Behind the `PdfParser` `IParser` wrapper.

### 3. PdfPig → Python-span mapping (the geometry work)
PyMuPDF gives ready-made spans; PdfPig gives **letters**. We reconstruct line-level "spans":
- Open `PdfDocument.Open(path)`; iterate `document.GetPages()` (page order).
- Per page: take `page.Letters`; **group into lines** by baseline Y (PDF Y increases upward, so reading
  order is descending Y); sort each line's letters by X; join `Letter.Value` into the line text.
- Per line: **size** = the rounded (1 dp) modal/median `PointSize` of its letters; **bold** = majority
  of letters whose `Font.Name` contains "Bold" (or `Letter.Font.IsBold` if available); **y0** = line top.
- Emit ordered line-blocks `{text,size,bold,y0}`; feed the **same** median/threshold/level logic as Python.
- Edge: empty `text` lines dropped; a page with no letters contributes nothing.

### 4. Heading logic (ported verbatim)
- `median` = median of line sizes; `headingMin = median × 1.15`.
- distinct heading sizes (`size ≥ headingMin`) sorted descending → `size→level` (`min(i+1, 6)`).
- `isHeading = size ≥ headingMin || (bold && size ≥ median × 1.05 && text.Length < 100)`.
- non-heading text appended to the current section (`text + "\n"`); pre-heading → "Introduction".
- title = the largest-size line's text (fallback `ParserText.FilenameToTitle`).

### 5. Layout (additive)
```
src/DocNest.Parsers/Pdf/PdfParser.cs        (+ internal PdfLineExtractor for the letter→line grouping)
tests/DocNest.Parsers.Tests/Pdf/…           + a PdfSharp-built fixture
```
- Add `UglyToad.PdfPig` to `DocNest.Parsers`; register `PdfParser` in `ParserFactory`.
- Parsing is sync CPU work wrapped as `Task` (PdfPig is synchronous).

### 6. Fixtures
PDFs are binary and PdfPig is **read-only**. Build a known text PDF **in-test** with **PdfSharp**
(MIT) — draw a large-font title, two heading-size lines, and body text — then parse it with PdfPig
(mirrors the Slice-4b in-test fixture approach). Test-only dependency.

### 7. Backward-compat surface
- Additive: one parser + one runtime dependency on `DocNest.Parsers`; one `ParserFactory` registration.
  No Slice-1..4b changes; no `.udf` change.

### 8. Open questions (resolve GATE 0 / Phase 2)
- Q1: **Scope** — text + headings only this slice; PDF **tables** and **OCR/scanned** as their own
  later slices? (Recommend yes — PdfPig has no table finder; OCR needs an engine.)
- Q2: **Fixture** — build in-test with **PdfSharp** (test dep) vs commit a small binary `.pdf`. (Recommend PdfSharp in-test.)
- Q3: line-grouping tolerance (Y-baseline epsilon) and bold detection (`Font.Name` contains "Bold" vs
  `Font.IsBold`) — resolve empirically against the fixture; defaults documented in ADR-0006.
