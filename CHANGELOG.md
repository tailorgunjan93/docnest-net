# Changelog

All notable changes to **DocNest .NET** are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/); the project adopts SemVer at its first NuGet release.

## [Unreleased]

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
