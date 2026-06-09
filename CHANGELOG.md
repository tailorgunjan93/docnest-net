# Changelog

All notable changes to **DocNest .NET** are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/); the project adopts SemVer at its first NuGet release.

## [Unreleased]

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
