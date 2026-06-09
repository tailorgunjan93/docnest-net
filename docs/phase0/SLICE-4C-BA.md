# Phase 0.1 — BA / Functional Document
## Slice 4c: PDF parser (text PDFs, font-size headings)

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan · **Depends on:** Slices 1–4b (✅)

---

### WHY
PDF is the most common document format DocNest must read. The Python engine has two PDF paths: a
fast text path (PyMuPDF, font-size heading detection) and an ML/OCR path (docling) for scanned PDFs.
The .NET equivalent of the **fast text path** is **`UglyToad.PdfPig`** (Apache-2.0, pure-managed, no
native libs, no ML) — it exposes per-letter font sizes, so we can port the font-size heading
heuristic directly. This slice ships that, completing the **common-format** parser set (md, html,
csv, docx, xlsx, **pdf**) and reaching **M2** for text-native PDFs.

### WHAT — exact functional behaviour
1. **`PdfParser`** (`.pdf`): extract text per page via PdfPig; group letters into **lines** with a
   representative **font size** and **bold** flag; detect **headings** by relative font size
   (`size ≥ median × 1.15`, or **bold** and `size ≥ median × 1.05` and short), assign **levels** by
   ranking distinct heading sizes (largest = level 1, capped at 6); content before the first heading
   → "Introduction"; **title** = the largest text line (fallback: filename).
2. Registers in `ParserFactory` so `factory.Get("report.pdf")` works.

**Before vs after**
- *Before:* PDFs cannot be ingested.
- *After:* text-native `.pdf` files ingest end-to-end (`parse → normalise → enrich → .udf`).

**Acceptance criteria**
- AC1: heading detection by font-size threshold + bold heuristic matches the Python `PyMuPDFParser`
  logic (median font, 1.15 threshold, level banding, "Introduction" fallback).
- AC2: title = largest text line; level assignment by descending distinct heading sizes (cap 6).
- AC3: a missing/empty PDF → `ParseException`; a PDF with no extractable text → empty/`Introduction`-only doc.
- AC4: parser leaves `Section.Id = ""`; output flows through the pipeline → `.udf` and round-trips.
- AC5: `Supports(".pdf")` true.

### Non-goals (this slice — explicitly deferred)
- **PDF table extraction** — PdfPig has no built-in table finder (PyMuPDF's `find_tables` has no
  managed equivalent). Deferred to a dedicated hardening slice; this slice extracts **text only**.
- **Scanned / image PDFs (OCR)** — the Charter "image/scanned PDFs" target; needs an OCR engine.
  Deferred to its own slice (an `IOcrProvider` wrapper).
- **Large-PDF streaming** beyond PdfPig's page-at-a-time reading (the "large PDFs" target) — basic
  page-by-page is bounded; deeper hardening deferred.

### HOW — scenarios
- *Resume/report:* a text PDF with a large title, `## `-sized headings, and body text → a section tree.
- *Bold headings:* a PDF whose headings are bold but only slightly larger → detected via the bold rule.
- *Edge:* a PDF with uniform font (no headings) → one "Introduction" section; an empty/corrupt PDF → `ParseException`.

### Traceability
Serves **Reliability** + **Cost/Privacy** (local, no ML, Apache-2.0 dep). Completes common-format
coverage; tables/OCR/large-PDF hardening are explicitly scheduled as follow-ups (Charter targets).
