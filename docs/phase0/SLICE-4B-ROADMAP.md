# Phase 0.4 — Roadmap: Slice 4b (OpenXML parsers — docx + xlsx)

> Consolidates the Slice-4b BA/Dev/QA into ordered steps. One MIT dependency
> (`DocumentFormat.OpenXml`) behind two `IParser` wrappers. This is a **Med-complexity** slice — the
> docx merged-cell expander and the xlsx shared-strings/logical-table reader are the risk areas.
> Program roadmap: parser line is **4 (text) ✅ → 4b (OpenXML) → 4c (pdf)**.

## Ordered steps (what GATE 0 approves entering Phase 1 for)
1. **Phase 1 (Impact & risk):** new parsers + `DocumentFormat.OpenXml` on `DocNest.Parsers`; two
   `ParserFactory` registrations. Watch-items: docx merged-cell (`gridSpan`/`vMerge`) alignment; xlsx
   shared-strings + sparse-row densification; xlsx logical-table heuristic parity. Risk **Med → reduce
   to Low** by isolating the grid/cell readers behind tested helpers before wiring the parsers.
2. **Phase 2 (Design + ADR-0005):** resolve Q1–Q4 (scope, style matching, xlsx heuristic depth, fixture
   generation); the merged-cell expansion algorithm; the xlsx cell/row reader; file-by-file plan.
3. **Phase 3 (Test-first):** build the **fixture generator** (`tools/make_office_fixtures`); commit
   `sample.docx`/`sample.xlsx`; write U1–U14 + I1–I2 failing first. Merged-cell + logical-table cases first.
4. **Phase 4 (Implement):** internal OpenXML helpers (docx grid expander; xlsx row reader + table
   splitter), then `DocxParser`, `ExcelParser`; register both in `ParserFactory`.
5. **Phase 5 (Verify):** full suite (Slices 1–4b) green; office fixtures → `.udf` round-trip; merged-cell
   alignment holds.
6. **Phase 6:** defects → regression + unit tests then fix (table-grid and cell-type bugs especially).
7. **Phase 7:** branch `slice/4b-openxml` → green → merge `main`; CHANGELOG; tag `v0.0.5-openxml`.

**Risk/impact expectation:** Med→Low after mitigations — one mature MIT dependency behind wrappers;
additive; the two hard readers are unit-tested in isolation before the parsers use them.

## Decisions needed from owner at GATE 0
- Approve the four Phase 0 documents, **and** Q1: docx + xlsx together in Slice 4b, or split (4b-docx /
  4b-xlsx) for smaller gates?
- Q3: xlsx — port the full multi-table split heuristic now, or ship one-table-per-sheet first and harden
  later? (Affects scope/risk.)
- Q2 (style matching) and Q4 (fixture generation) can defer to ADR-0005.
