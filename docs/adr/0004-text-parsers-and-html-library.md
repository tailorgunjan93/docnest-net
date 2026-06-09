# ADR-0004 — Text-format parsers, the `DocNest.Parsers` assembly, and the HTML library

- **Status:** Accepted (owner approved straight-through to GATE 5)
- **Date:** 2026-06-09
- **Context slice:** Slice 4 — text-format parsers
- **Builds on:** ADR-0001 (`IParser` contract), Slice 3 (`DocNestPipeline` injects `IParser`)

## Context
DocNest needs parsers to ingest real files. The three text formats (md/html/csv) are
dependency-light; OpenXML (docx/xlsx) and PDF carry heavier, riskier dependencies. The owner approved
splitting parsers into **Slice 4 (text) → 4b (OpenXML) → 4c (pdf)** and chose **AngleSharp + zero-dep
CSV** for this slice.

## Decision
1. **New `DocNest.Parsers` assembly** holding `MarkdownParser`, `CsvParser`, `HtmlParser`, and
   `ParserFactory`. It references `DocNest.Abstractions` (`IParser`, `RawDocument`) and `DocNest.Core`
   (`DocId.FromPath`). `Core` does **not** reference `Parsers` (no cycle); consumers compose a
   `ParserFactory` and pass an `IParser` into `DocNestPipeline`.
2. **HTML → AngleSharp** (MIT, W3C-compliant HTML5 parser + DOM). Referenced **only** inside
   `HtmlParser` (the `IParser` wrapper); never from `Core`. Pinned centrally.
3. **CSV → zero dependency.** A small internal RFC-4180 state-machine reader (quotes, escaped `""`,
   embedded delimiters/newlines) + a delimiter heuristic (`,` `\t` `;` `|`; `.tsv` = tab) + an
   encoding cascade (UTF-8-BOM → UTF-8-strict → Latin-1). Honours the minimal-deps motto.
4. **Markdown → zero dependency** (ATX line scan with fenced-code tracking).
5. **Parsers leave `Section.Id = ""`**; the normaliser (Slice 3) owns §ids. Sync CPU work is wrapped
   as `Task`; file I/O is genuinely async.

## Consequences
**Positive:** real-file ingestion (M2) with one isolated MIT dependency; AngleSharp stays behind the
wrapper; md/csv add zero deps; the factory is open for the docx/xlsx/pdf parsers to register later.
**Negative / cost:** a hand-rolled CSV reader to maintain (small, well-tested); AngleSharp's
`TextContent` lacks BeautifulSoup's `separator` option, so whitespace joining differs slightly from
Python (documented; not byte-asserted).
**Neutral:** the heavy formats' dependency choices (`DocumentFormat.OpenXml`, `UglyToad.PdfPig`) are
deferred to their own ADRs in 4b/4c.

## Alternatives considered
- *CsvHelper / Sep:* robust, but a dependency for a small need — rejected (motto).
- *HtmlAgilityPack:* older, less standards-compliant DOM than AngleSharp — rejected.
- *All parsers in one slice:* mixes three dependency/risk profiles and inflates blast radius — rejected
  in favour of the 4 → 4b → 4c split.
- *Parsers in `Core`:* would pull AngleSharp into the core assembly — rejected (wrapper rule).
