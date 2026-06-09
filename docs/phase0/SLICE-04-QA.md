# Phase 0.3 — QA / User Document
## Slice 4: Text-format parsers + ParserFactory

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### What "working" means
A user points DocNest at a real `.md`/`.html`/`.csv` file and gets a faithful `RawDocument` (sections,
headings, tables) that flows through the pipeline into a `.udf` — matching what the Python parsers
produce for the same files.

### Test plan (test-first — written in Phase 3, failing first)

**Unit — MarkdownParser**
- U1: ATX headings `#`/`##`/`###` → sections with correct `Level`/`Title`/`Text`.
- U2: fenced code block containing `# not a heading` does NOT create a section.
- U3: text before the first heading → a preamble section; first `#` H1 → document title.
- U4: a file with no headings → one section; an empty file → one empty section.
- U5: `Supports` true for `.md`/`.markdown`, false otherwise.

**Unit — CsvParser**
- U6: comma file → headers + rows; row widths normalised (pad/truncate) to header count.
- U7: delimiter detection for `;` and `|`; `.tsv` always tab.
- U8: encoding — a UTF-8-BOM file and a Latin-1 file both decode correctly.
- U9: RFC-4180 quoting — a quoted field containing the delimiter and an escaped `""` parse as one cell.
- U10: empty file and header-only file → `ParseException`; trailing empty header columns trimmed.

**Unit — HtmlParser**
- U11: `h1`–`h3` hierarchy with body text per section.
- U12: a `<table>` with `rowspan`/`colspan` → a correctly densified rectangular `TableData`.
- U13: title from `<title>`, falling back to first `<h1>`, then filename.
- U14: a page with no headings → one section from body text.

**Unit — ParserFactory**
- U15: `Get` routes by extension (first match wins); `Supports` reflects the registry.
- U16: unknown extension → `UnsupportedFormatException` (message lists supported formats).
- U17: `Register`/`Unregister` add/remove a parser at runtime.

**Integration — end-to-end through the pipeline**
- I1: `factory.Get(path).ParseAsync(path)` → `DocNestPipeline.Process` → `Document` with §ids and
  deterministic intelligence, for one `.md`, one `.csv`, one `.html` fixture.
- I2: that `Document` → `.udf` (Slice 2) → round-trips loss-free (regression tie-in).

### Fixtures
Small committed files under `tests/fixtures/`: `sample.md` (headings + fenced code + preamble),
`sample.csv` (quoted fields, ragged rows), `sample.html` (headings + a rowspan table, a `<title>`).
Where a Python env is available, a guarded cross-check (like Slice-2 E2) can compare the .NET
`RawDocument` to the Python parser's output; the committed expectations are the baseline regardless.

### Edge / negative cases
- Markdown with `~~~` fences and Setext-style headings (documented: only ATX supported, matching Python).
- CSV with embedded newlines inside quotes; a single-column file (delimiter heuristic falls back to `,`).
- HTML with nested tables, missing `<caption>`, malformed markup (AngleSharp recovers like a browser).
- Non-ASCII throughout (headings, cells) survives into the `.udf` (ties to Slice-2 encoding tests).

### What constitutes a regression (regression-suite seeds)
- A heading mis-levelled or a fenced-code `#` treated as a heading.
- CSV delimiter mis-detection or a quoted field split incorrectly.
- HTML table grid misaligned (rowspan/colspan) — drops column meaning (Charter "complex tables" target).
- `ParserFactory` routing to the wrong parser or not raising on unknown formats.
- Any parser populating `Section.Id` (must stay empty — the normaliser owns §ids).
