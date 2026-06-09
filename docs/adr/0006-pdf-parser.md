# ADR-0006 — PDF text parser (PdfPig), scope deferrals, and PdfSharp fixtures

- **Status:** Accepted (owner approved straight-through to GATE 5)
- **Date:** 2026-06-09
- **Context slice:** Slice 4c — PDF parser
- **Builds on:** ADR-0004 (`DocNest.Parsers`, wrapper rule)

## Context
PDF is the most common ingest format. The Python engine has a fast text path (PyMuPDF, font-size
headings) and an ML/OCR path (docling). The pure-managed, Apache-2.0 `UglyToad.PdfPig` exposes per-letter
font sizes — enough to port the fast text path. PdfPig has **no table finder** and **no OCR**.

## Decision
1. **`UglyToad.PdfPig`** (Apache-2.0) for text PDFs, behind the `PdfParser` `IParser` wrapper.
2. **Scope = text + headings only.** PDF **table extraction** and **OCR/scanned PDFs** are **explicitly
   deferred** to their own later hardening slices (the Charter "complex tables" and "image/scanned" and
   "large PDFs" targets). This slice must not crash on a text-less PDF — it yields an empty/`Introduction`
   document.
3. **Letter → line reconstruction** in `PdfLineExtractor`: cluster letters by `GlyphRectangle.Bottom`
   (descending Y = reading order, tolerance ≈ 3pt), order each line by `GlyphRectangle.Left`; line size =
   rounded median `PointSize`; bold = majority `FontName` containing "Bold". This rebuilds the Python
   "span" stream the heading heuristic consumes.
4. **Heading heuristic ported verbatim** (median font, ×1.15 threshold, bold rule, distinct-size level
   banding capped at 6, "Introduction" fallback, title = largest line).
5. **Fixtures built in-test with PdfSharp** (MIT, test-only); not committed binaries.

## Consequences
**Positive:** text-native PDFs ingest with one Apache-2.0 managed dependency (no native libs, no ML);
the geometry is isolated + tested; the scanned/table gaps are explicit, scheduled, and non-crashing.
**Negative / cost:** PDFs with tables lose table structure until the follow-up slice; line grouping is a
heuristic (mixed-size lines use the modal/median size); PdfSharp needs `UseWindowsFontsUnderWindows`.
**Neutral:** heading detection quality depends on the PDF actually using font-size contrast (same caveat
as the Python fast path; docling-style layout analysis is out of scope).

## Alternatives considered
- *iText / Aspose / PDFium:* commercial or native dependencies — rejected (cost, portability).
- *Attempt PDF tables now (custom ruling/line heuristic):* high risk without a finder; deferred.
- *Commit a binary `.pdf` fixture:* opaque; PdfSharp in-test is reproducible. Rejected.
- *docling-style ML layout:* heavy, not pure-.NET; out of scope for the fast path.
