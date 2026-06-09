# Phase 1 + Phase 2 — Impact/Risk & Design
## Slice 4c: PDF parser (text PDFs)

**Status:** Phases 1–2 (owner approved straight-through to GATE 5) · **Owner:** Gunjan
**ADR:** [ADR-0006](../adr/0006-pdf-parser.md) · **Scope:** text + headings only; tables + OCR deferred.

---

## Phase 1 — Impact & Risk
**Blast radius:** new `PdfParser` + `UglyToad.PdfPig` on `DocNest.Parsers`; one `ParserFactory`
registration. No Slice-1..4b changes; no `.udf` change.

**Watch-items / mitigations:**
1. **Letter → line grouping** (reading order) — PdfPig gives letters, not lines. Mitigation: isolate
   `PdfLineExtractor`, unit-test it; cluster by baseline Y (descending = top→bottom), order each line by X.
2. **Heading parity** (font-size threshold + bold + level banding). Mitigation: port the Python logic
   verbatim; assert against a PdfSharp fixture with known sizes (24/16/13/11 pt).
3. **Text-less / scanned PDFs** — no OCR this slice. Mitigation: a page with no letters contributes
   nothing → empty/`Introduction`-only doc; **never crash** (documents the scanned-PDF gap as a future slice).
**Risk Low/Med → Low.** Impact Low (additive). PdfPig reads page-at-a-time (bounded memory).

## Phase 2 — Design
**DSA:** per page O(letters) cluster + O(letters log) sort; sections O(blocks). Linear; memory page-bounded.

**SOLID / patterns:** `PdfParser` is one `IParser` (Strategy) registered in the Factory (OCP); PdfPig
lives only inside `PdfParser` + `PdfLineExtractor` (wrapper/DIP). Sync work wrapped as `Task`.

**Code plan (signatures):**
- `Pdf/PdfLineExtractor.cs` (internal) — `static List<PdfLine> Extract(PdfDocument document)`; `PdfLine`
  = `record(string Text, double Size, bool Bold)`. Clusters letters by `GlyphRectangle.Bottom`
  (tolerance ≈ 3pt), orders by `GlyphRectangle.Left`; size = rounded median `PointSize`; bold = majority
  `FontName` contains "Bold".
- `Pdf/PdfParser.cs` — `IParser`; `PdfParser(double headingThreshold = 1.15)`; `MedianSize`,
  `ExtractTitle` (largest block), `BuildSections` (port of `_build_sections`: `headingMin = median×1.15`;
  distinct heading sizes → levels `min(i+1,6)`; bold rule `size ≥ median×1.05 && len<100`;
  "Introduction" fallback).
- `ParserFactory` ctor registers `PdfParser`.

**Fixtures:** built **in-test with PdfSharp** (MIT, test-only): a 24pt bold title, 16pt + 13pt headings,
11pt body — parsed back with PdfPig. `GlobalFontSettings.UseWindowsFontsUnderWindows = true` so the
system font resolves on Windows.

**Resolved open questions:** Q1 → text+headings only (PDF tables + OCR = later slices); Q2 → PdfSharp
in-test fixture; Q3 → baseline-Y tolerance ≈ 3pt, bold via `FontName` contains "Bold" (revisit empirically).
