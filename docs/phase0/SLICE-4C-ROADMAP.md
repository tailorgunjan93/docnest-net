# Phase 0.4 — Roadmap: Slice 4c (PDF parser — text PDFs)

> Consolidates the Slice-4c BA/Dev/QA. One Apache-2.0 dependency (`UglyToad.PdfPig`) behind the
> `PdfParser` wrapper. Completes the **common-format** parser set. Parser line:
> **4 (text) ✅ → 4b (OpenXML) ✅ → 4c (pdf) → [later] pdf-tables, OCR/scanned, large-PDF hardening.**

## Ordered steps (what GATE 0 approves entering Phase 1 for)
1. **Phase 1 (Impact & risk):** new `PdfParser` + `UglyToad.PdfPig` on `DocNest.Parsers`; one
   `ParserFactory` registration. Watch-items: letter→line grouping correctness (reading order); font-size
   heading parity; graceful handling of text-less PDFs. Risk Low/Med → Low (isolate `PdfLineExtractor`,
   test it first).
2. **Phase 2 (Design + ADR-0006):** resolve Q1–Q3 (scope = text+headings; fixture via PdfSharp;
   line-grouping epsilon + bold detection); the letter→line algorithm; file-by-file plan. Record the
   explicit **deferral** of PDF tables and OCR as scheduled hardening slices.
3. **Phase 3 (Test-first):** build the `PdfSharp` fixture; write U1–U11 + I1 failing first.
4. **Phase 4 (Implement):** `PdfLineExtractor` (letters → ordered line-blocks), then `PdfParser`
   (median/threshold/level/Introduction/title); register in `ParserFactory`.
5. **Phase 5 (Verify):** full suite (Slices 1–4c) green; PDF fixture → `.udf` round-trip.
6. **Phase 6:** defects → regression + unit tests then fix (line-grouping / heading edge cases).
7. **Phase 7:** branch `slice/4c-pdf` → green → merge `main`; CHANGELOG; tag `v0.0.6-pdf`.

**Risk/impact expectation:** Low/Med → Low — one mature Apache-2.0 dependency behind a wrapper;
additive; the geometry (line grouping) is unit-tested in isolation; tables/OCR explicitly out of scope.

## Decisions needed from owner at GATE 0
- Approve the four Phase 0 documents, **and** confirm Q1: text + headings only this slice, with PDF
  **tables** and **OCR/scanned** as their own later hardening slices?
- Q2: fixture via **PdfSharp** in-test (test dep) vs a committed binary `.pdf`?
