# Phase 2 — Design (DSA + Architecture)
## Slice 1: Core Domain Model + Wrapper Contracts

**Status:** Phase 2 (awaiting GATE 2) · **Owner:** Gunjan · **ADR:** [ADR-0001](../adr/0001-domain-records-and-wrapper-interfaces.md)

---

### 1. DSA pass (efficiency + memory)
This slice is pure data contracts; the "algorithms" are construction, equality, and (de)serialisation.

| Element | Time | Space | Why optimal |
|---|---|---|---|
| `record` construction / `with` | O(fields) | O(fields) | value semantics; no defensive copies; collections stored as already-built `IReadOnlyList<T>` |
| Record value-equality (tests) | O(fields) | O(1) | compiler-generated; only used in tests |
| `DocId.FromPath` | O(n) over filename length | O(n) | single pass of regex replacements; filenames are short |
| JSON (de)serialise | O(payload) | O(payload) | `System.Text.Json` **source-generated** context → no runtime reflection, AOT-friendly, lower alloc |

**Memory/NFR:** domain objects are small and bounded; the only sizeable field is
`Section.Embedding` (`byte[]`), bounded by `dims × bytes-per-element` (quantised). No buffers,
streams, or large allocations enter this slice — bounded-memory NFR is trivially satisfied here
and enforced for real in the parsing/embedding slices.

### 2. Architect pass — SOLID
- **SRP:** each record models exactly one concept; each interface is one extension role; exceptions
  are one failure mode each.
- **OCP:** new parsers/embedders/LLMs/stores/search backends are added by *implementing an
  interface*, never by editing core. The domain is closed for modification, open for extension.
- **LSP:** interface contracts are minimal and total; trivial test fakes (Phase 3 F1/F2) prove any
  implementation substitutes cleanly.
- **ISP:** five small focused interfaces instead of one mega-provider — a parser author never sees
  embedding methods.
- **DIP:** `DocNest.Core` and all future logic depend on `DocNest.Abstractions` (the interfaces),
  never on concretions. Extension authors reference only `Abstractions`.

### 3. Design patterns used
- **Immutable DTO / Value Object** — the domain records.
- **Strategy** — `IEmbedder`, `ISearchProvider`, `ILlmProvider` (swap behaviour without touching callers).
- **Abstract product for Factory/Template-Method** — `IParser` (the `ParserFactory` + parse-skeleton land in Slice 4).
- **Repository (later)** — `IStorageBackend` is the seam the `.udf` repository sits behind (Slice 2).

### 4. Wrapper rule (boundary definition)
No external library is referenced in this slice (BCL only). The five interfaces **are** the wrapper
boundaries that every future external dependency must sit behind:
`OpenXML/PDF → IParser`, `ONNX → IEmbedder`, `LLM SDKs → ILlmProvider`,
`System.IO.Compression → IStorageBackend`, `SQLite/BM25 → ISearchProvider`.
Core logic will only ever see these interfaces — never a third-party type. **Domain records are
persistence-ignorant**: the `.udf` wire DTOs (Slice 2) sit behind `IStorageBackend` and map to/from
these records, so a wire-format change never ripples into the domain (ADR-0001).

### 5. JSON strategy (decision)
- `DocNestJson : JsonSerializerContext` — source-generated, registered for every domain record.
- Naming: snake_case via `[JsonPropertyName]` per property (explicit > policy, to lock the contract).
- `Section.SectionId` is `[JsonIgnore]` (computed alias of `Id`).
- Collections never serialise as `null` (init to `[]`); null-vs-omit for *optional scalars* is
  **owned by the Slice-2 wire DTOs** and pinned there against a Python fixture — the domain records
  use `JsonIgnoreCondition.Never` so unit round-trips are self-consistent.
- This context lets later slices reuse one configured serialiser; the authoritative `.udf` shape is
  still produced by the Slice-2 DTOs.

### 6. Code plan — file by file (signatures; nothing prose-free)

**`DocNest.Abstractions`** (TFM net8.0, `<Nullable>enable</Nullable>`, no package refs)
- `IParser.cs` — `Task<RawDocument> ParseAsync(string path, CancellationToken ct = default)`; `bool Supports(string path)`
- `IEmbedder.cs` — `Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)`; `int Dims { get; }`; `string ModelName { get; }`
- `ILlmProvider.cs` — `Task<string> CompleteAsync(string prompt, string system = "", double temperature = 0.1, int maxTokens = 512, CancellationToken ct = default)`; `string ProviderName { get; }`; `string ModelName { get; }`
- `IStorageBackend.cs` — `Task<string> WriteArchiveAsync(IReadOnlyDictionary<string, byte[]> entries, string outputPath, CancellationToken ct = default)`; `Task<byte[]> ReadEntryAsync(string archivePath, string name, CancellationToken ct = default)`; `Task<IReadOnlyList<string>> ListEntriesAsync(string archivePath, CancellationToken ct = default)`; `string BackendName { get; }`
- `ISearchProvider.cs` — `void BuildIndex(IReadOnlyList<IReadOnlyList<string>> corpus)`; `IReadOnlyList<double> GetScores(IReadOnlyList<string> queryTokens)`; `string BackendName { get; }`
- `Exceptions.cs` — `DocNestException : Exception` (3 ctors) + `ParseException`, `UnsupportedFormatException`, `EmbedException`, `IntelligenceException`, `UdfWriteException`, `UdfReadException`, `SizeLimitException`, `ConnectorException`, `QuantizationException`

**`DocNest.Core`** (refs `Abstractions`)
- `Models/TableData.cs`, `ImageRef.cs`, `Section.cs`, `KeyNumber.cs`, `RawDocument.cs`, `DocMeta.cs`, `Document.cs`, `Catalogue.cs`, `SectionIndexEntry.cs` — `public sealed record` with `[JsonPropertyName]` per the Dev-doc mapping table; collections `IReadOnlyList<T> = []`.
- `DocId.cs` — `public static string FromPath(string path)` (ports `_make_doc_id` regex rules).
- `Udf.cs` — `public const string Version = "1.0";` (mirrors Python `UDF_VERSION`).
- `Json/DocNestJson.cs` — `[JsonSerializable(typeof(...))] partial class DocNestJson : JsonSerializerContext`.

**`DocNest.Core.Tests`** (xUnit + FluentAssertions; refs `Core`)
- `DomainDefaultsTests.cs` (U1,U2,U6), `SectionAliasTests.cs` (U3), `DocIdTests.cs` (U4),
  `ExceptionHierarchyTests.cs` (U5), `JsonRoundTripTests.cs` (record self-round-trip),
  `ContractFakeTests.cs` (F1,F2 trivial fakes implement each interface).
  *(Interop golden-fixture I1/I2 deferred to Slice 2 per Phase 1 finding.)*

**Solution scaffolding**
- `DocNest.sln`, `Directory.Build.props` (`Nullable=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true`, analyzers), `Directory.Packages.props` (CPM: pin `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FluentAssertions`).

### 7. Resolved open questions
- Q1 → package id **`DocNest`**. Q2 → **two assemblies** (`Abstractions` + `Core`). Q4 → `IEmbedder`
  returns `IReadOnlyList<float[]>`. Q3 (null vs omit) → **moved to Slice 2** wire DTOs (Phase 1 finding).

**GATE 2 result (proposed):** DSA complexity stated; SOLID + patterns justified; wrapper boundaries
defined; file-by-file code plan with signatures; ADR-0001 recorded. → Eligible to proceed to Phase 3
(test-first). *(Presented for your sign-off.)*
