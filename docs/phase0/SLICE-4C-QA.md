# Phase 0.3 — QA / User Document
## Slice 4c: PDF parser (text PDFs)

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### What "working" means
A user points DocNest at a text-native `.pdf` and gets a faithful section tree (title + font-size
headings + body text) that flows into a `.udf` — matching the Python fast PDF path's structure.

### Test plan (test-first — written in Phase 3, failing first)

**Unit — PdfParser (against a PdfSharp-built fixture)**
- U1: a PDF with a large title, two heading-size lines, and body → sections at the right levels in order.
- U2: title = the largest-size text line.
- U3: a heading that is **bold but only slightly larger** than body is detected (bold rule).
- U4: body text under a heading is captured in that section's `Text`.
- U5: a uniform-font PDF (no headings) → a single "Introduction" section.
- U6: level banding — three distinct heading sizes → levels 1/2/3 (largest = 1); >6 distinct → capped at 6.
- U7: parser leaves `Section.Id = ""`; `Supports(".pdf")` true, other extensions false.

**Unit — line extraction**
- U8: letters on the same baseline group into one line in left-to-right order; lines are ordered top-to-bottom.
- U9: empty/whitespace lines are dropped.

**Negative**
- U10: a missing PDF → `ParseException`; an empty (0-byte) file → `ParseException`; a corrupt PDF → `ParseException`.
- U11: a PDF with no extractable text (e.g. an image-only page, no OCR this slice) → empty or
  "Introduction"-only document, **no crash** (documents the scanned-PDF gap).

**Integration**
- I1: PdfSharp fixture → `factory.Get(path).ParseAsync` → `DocNestPipeline.Process` → `Document` with
  §ids → `.udf` round-trip loss-free (regression tie-in).

### Fixtures
A `PdfSharp`-built `.pdf` in a temp file: a 24pt title, two 16pt/13pt heading lines, and 11pt body
paragraphs (so median ≈ 11, thresholds bite predictably). Built in-test, parsed with PdfPig.

### Edge / negative cases
- A PDF whose text has mixed sizes within a line (use the modal size; documented).
- Non-ASCII glyphs in headings/body survive into the `.udf` (ties to Slice-2 encoding).
- A multi-page PDF — sections span pages in reading order.
- An image-only/scanned PDF — produces little/no text (OCR is a future slice); must not crash.

### What constitutes a regression (regression-suite seeds)
- A heading mis-levelled, or body text promoted to a heading (threshold/bold drift).
- Title resolution picking the wrong line.
- Line grouping merging two visual lines or splitting one (reading-order break).
- A parser populating `Section.Id`, or a crash on a text-less PDF instead of a graceful empty result.
