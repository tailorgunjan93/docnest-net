# Phase 0.4 — Roadmap: Slice 4 (Text-format parsers + ParserFactory)

> Consolidates the Slice-4 BA/Dev/QA into ordered steps. This is the **first slice with an external
> runtime dependency** (AngleSharp, for HTML only). Program roadmap ([../ROADMAP.md](../ROADMAP.md))
> gains a parser sub-split: **4 (text) → 4b (OpenXML docx/xlsx) → 4c (pdf)**.

## Proposed parser sub-split (rationale: isolate dependency + risk profiles)
| Slice | Formats | New dependency | Risk |
|---|---|---|---|
| **4 (this)** | md, html, csv + `ParserFactory` | AngleSharp (HTML only); md/csv zero-dep | Low |
| 4b | docx, xlsx | `DocumentFormat.OpenXml` (MIT) | Med (table/structure complexity) |
| 4c | pdf | `UglyToad.PdfPig` (Apache-2.0, pure-managed) | Med/High (font-size heading heuristics, scanned PDFs) |

Reaching **M2** (a real file → Python-readable `.udf`) happens at the end of Slice 4 for text formats;
docx/xlsx/pdf extend coverage in 4b/4c.

## Ordered steps for Slice 4 (what GATE 0 approves entering Phase 1 for)
1. **Phase 1 (Impact & risk):** new `DocNest.Parsers` assembly; AngleSharp behind the `IParser`
   wrapper; pin it centrally. Watch-items: HTML table grid parity; CSV quoting/delimiter parity;
   encoding cascade. Risk/Impact expected Low/Low.
2. **Phase 2 (Design + ADR-0004):** resolve Q1–Q4 (scope, CSV approach, HTML lib); the parser base
   pattern (sync core wrapped as `Task`); the CSV RFC-4180 reader + delimiter heuristic; file-by-file plan.
3. **Phase 3 (Test-first):** commit fixtures; write U1–U17 + I1–I2 (+ guarded Python cross-check) failing first.
4. **Phase 4 (Implement):** `MarkdownParser`, `CsvParser`, `HtmlParser`, `ParserFactory`.
5. **Phase 5 (Verify):** full suite (Slices 1–4) green; pipeline + `.udf` round-trip on real fixtures.
6. **Phase 6:** defects → regression + unit tests then fix (table-grid bugs especially).
7. **Phase 7:** branch `slice/04-parsers` → green → merge `main`; CHANGELOG; tag `v0.0.4-parsers`.

**Risk/impact expectation:** Low/Low — one well-established MIT dependency (AngleSharp) behind a
wrapper; md/csv zero-dep; additive. Parity guarded by fixtures + case tables before code.

## Decisions needed from owner at GATE 0
- Approve the four Phase 0 documents **and the parser sub-split** (text now; OpenXML + PDF as 4b/4c).
- Dev Q2 (CSV: zero-dep hand-rolled vs CsvHelper) and Q3 (HTML: AngleSharp vs HtmlAgilityPack) — or
  defer to ADR-0004.
