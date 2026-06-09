# ADR-0005 — OpenXML parsers (docx + xlsx) and in-test binary fixtures

- **Status:** Accepted (owner approved straight-through to GATE 5)
- **Date:** 2026-06-09
- **Context slice:** Slice 4b — OpenXML parsers
- **Builds on:** ADR-0004 (`DocNest.Parsers`, wrapper rule)

## Context
Word (`.docx`) and Excel (`.xlsx`) are OpenXML (ZIP+XML). The Python parsers use python-docx / openpyxl,
which hide two things the OpenXML SDK exposes raw: merged-cell repetition (docx) and shared-strings /
sparse rows (xlsx). The owner chose docx + xlsx **together** with the **full** xlsx multi-table heuristic.

## Decision
1. **`DocumentFormat.OpenXml`** (Microsoft, MIT) for both formats, referenced only inside the parsers and
   their internal readers (`DocxTable`, `XlsxReader`) — never from `Core`.
2. **docx merged cells are expanded by us:** `DocxTable.ToGrid` repeats `gridSpan` values across columns
   and copies `vMerge`-`continue` values down from the `restart` cell, then width-normalises — restoring
   the column alignment python-docx provides for free. Built and unit-tested in isolation first.
3. **xlsx reading is centralised** in `XlsxReader`: resolve shared strings, read cached `<v>` (and inline
   strings / booleans), and **densify each row by column letter** so sparse rows become rectangular —
   feeding the ported `SplitIntoTables` heuristic Python-equivalent rows.
4. **docx heading detection by style id** (`^heading\s*([1-6])$` regex on the id, plus `title`→1 /
   `subtitle`→2) — avoids resolving `styles.xml` display names; covers standard Word ids.
5. **Binary fixtures are built in-test** with the OpenXML SDK (the test project references the SDK), not
   committed as binary blobs — reproducible and reviewable. Supersedes the "committed generator" idea.

## Consequences
**Positive:** real office docs ingest; merged-cell alignment (the "complex tables" target) is explicit and
tested; one MIT dependency behind the wrapper; fixtures are diffable code, not opaque binaries.
**Negative / cost:** more parser code than the text formats (two low-level readers); a couple of fidelity
gaps vs openpyxl (dates as cached serials; booleans normalised) — documented, not asserted for exactness.
**Neutral:** `.doc`/`.xls` remain unsupported (clear `ParseException`).

## Alternatives considered
- *A higher-level library (ClosedXML / NPOI) for xlsx:* heavier/extra deps; OpenXML SDK already needed for
  docx — one dependency is cleaner. Rejected.
- *Commit binary `.docx`/`.xlsx` fixtures:* opaque, hard to review/diff; in-test construction is clearer. Rejected.
- *One-table-per-sheet xlsx first:* owner chose the full heuristic for parity. Rejected.
- *Resolve docx style names from `styles.xml`:* unnecessary for standard ids; added complexity. Deferred.
