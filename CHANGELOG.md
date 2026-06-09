# Changelog

All notable changes to **DocNest .NET** are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/); the project adopts SemVer at its first NuGet release.

## [Unreleased]

### Added — Slice 4b: OpenXML parsers (docx + xlsx)
- **`DocNest.Parsers/Office`** — `DocxParser` (ordered body walk; heading styles + ALL-CAPS/bold/colon
  pseudo-headings; tables with `gridSpan`/`vMerge` **merged-cell expansion** via `DocxTable`) and
  `ExcelParser` (each sheet → section; shared-strings + sparse-row reading via `XlsxReader`; logical-table
  split heuristic; `.xls` rejected). Both register in `ParserFactory`.
- **Dependency:** `DocumentFormat.OpenXml` (MIT) — docx/xlsx only, behind the Office `IParser` wrappers.
- **Tests** — +9; `.docx`/`.xlsx` fixtures built in-test via the OpenXML SDK (no committed binaries).
- Addresses the "complex tables" hardening target (Word merged cells). ADR-0005.

### Added — Slice 4: text-format parsers + ParserFactory
- **`DocNest.Parsers`** (new assembly) — `MarkdownParser` (zero-dep ATX scan with fenced-code
  tracking), `CsvParser` (zero-dep RFC-4180 reader + delimiter heuristic + UTF-8-BOM→UTF-8→Latin-1
  cascade), `HtmlParser` (AngleSharp; `h1`–`h6` hierarchy + `<table>` rowspan/colspan grid expansion),
  and `ParserFactory` (ordered registry, first-match routing, runtime register/unregister).
- **Dependency:** AngleSharp (MIT) — HTML only, kept behind the `HtmlParser` `IParser` wrapper.
- **Tests** — +18 (per-parser case tables incl. HTML rowspan grid + CSV quoting; ParserFactory;
  parse → pipeline → `.udf` round-trip for `.md`/`.csv`/`.html`). Real text files now ingest end-to-end.
- Parser sub-split: OpenXML (docx/xlsx) and PDF land in later slices (4b/4c).

### Added — Slice 3: pipeline + normaliser (milestone M1)
- **`DocNest.Core/Pipeline`** — `SectionNormaliser` (immutable two-pass §id assignment with compact
  depth, parent/child links, `token_count = int(words×1.3)`, table column-width normalisation) and
  `DocNestPipeline` (normalise → deterministic key-numbers → deterministic keywords; optional injected
  `IParser` for `ProcessAsync(path)`).
- **`DocNest.Core/Intelligence`** — `KeyNumberExtractor` and `KeywordExtractor`: pure, LLM-free,
  `[GeneratedRegex]` ports of the Python `key_numbers.py` / `keywords.py` (figure detection + label
  binding + noise filters; frequency × specificity keywords with title priority).
- **Tests** — +28 (normaliser, extractor parity case tables, pipeline incl. a `.udf` round-trip tie-in).
- **M1 reached:** a `.udf` round-trips between Python and .NET on synthetic documents (Slices 1–3).

### Added — Slice 2: `.udf` read/write
- **`DocNest.Storage`** — `ZipStorageBackend` implementing `IStorageBackend` over
  `System.IO.Compression` (DEFLATE for JSON/text, stored for binary/images).
- **`DocNest.Core/Udf`** — wire DTOs (`ManifestDto`/`CatalogueDto`/`ContentDto`/…) modelling the exact
  `.udf` JSON schema; a dedicated `UdfJson` serializer that writes `null`s and emits raw non-ASCII
  (`UnsafeRelaxedJsonEscaping`) to match Python's `ensure_ascii=False`; `UdfWriter`, `UdfReader` +
  `UdfPackage.ToDocument()` (reconstruct by §id join), and `SourceSanitiser`.
- **Tests** — `DocNest.Storage.Tests` + `.udf` read/write/round-trip/key-parity/version-gate/embeddings
  tests; cross-ecosystem interop tests E1 (load a Python-built golden `.udf`) and E2 (Python reads a
  .NET-built `.udf`), both `[SkippableFact]` (skip with reason until a fixture / Python env exists).
- **Tooling** — `tools/make_fixture.py` to generate the golden `tests/fixtures/sample.udf` from Python.

### Changed
- Renamed the `.udf` constant holder `Udf` → **`UdfFormat`** so the new `DocNest.Udf` namespace doesn't
  collide with a same-named type. `UdfFormat.Version` is unchanged (`"1.0"`). (Pre-release; no consumers.)

### Added — Slice 1: Core domain + contracts
- **`DocNest.Abstractions`** — immutable domain records (`TableData`, `ImageRef`, `Section`,
  `KeyNumber`, `RawDocument`, `DocMeta`, `Document`, `SectionIndexEntry`, `Catalogue`), the five
  wrapper interfaces (`IParser`, `IEmbedder`, `ILlmProvider`, `IStorageBackend`, `ISearchProvider`),
  the `DocNestException` hierarchy (base + 9 specific types), and the `Udf.Version` (`"1.0"`) constant.
- **`DocNest.Core`** — `System.Text.Json` source-generated context (`DocNestJson`) and
  `DocId.FromPath` (ports the Python `_make_doc_id` slug rules).
- **Tests** — xUnit suite (32 tests): defaults parity, `SectionId` alias non-serialisation,
  exception hierarchy, JSON round-trip / key fidelity / forward-compat, and contract implementability.

### Notes
- Targets `net8.0`; the library has **zero runtime package dependencies**. Central Package Management.
- JSON serialisation omits nulls (`WhenWritingNull`); collection properties are guaranteed never-null
  via null-coalescing `init` setters.
- The `.udf` wire schema (`manifest`/`catalogue`/`content` + `embeddings.bin`) is a **separate
  contract** from the in-memory domain and is implemented in Slice 2; domain records are
  persistence-ignorant. See [ADR-0001](docs/adr/0001-domain-records-and-wrapper-interfaces.md).
