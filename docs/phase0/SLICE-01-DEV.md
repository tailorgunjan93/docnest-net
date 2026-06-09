# Phase 0.2 — Dev / Technical Document
## Slice 1: Core Domain Model + Wrapper Contracts

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

> **Open questions resolved (Phase 2 / ADR-0001):** Q1 → package id `DocNest`; Q2 → two assemblies
> (`Abstractions` + `Core`); Q4 → `IEmbedder` returns `IReadOnlyList<float[]>`; Q3 (null vs omit) →
> moved to the Slice-2 wire DTOs, pinned against a Python fixture there.

---

### 1. Python reference — read end-to-end (files traced)
Source of truth: `D:\Learning\docnest\docnest\`. Files read for this slice:

| Python file | What it defines | Ported in Slice 1? |
|---|---|---|
| `models.py` | Pydantic v2 models: `TableData`, `ImageRef`, `Section`, `KeyNumber`, `RawDocument`, `DocMeta`, `Document`, `Catalogue` | **Yes — records** |
| `exceptions.py` | `DOCNESTError` base + 9 subclasses | **Yes — exceptions** |
| `parsers/base.py` | `IParser` ABC: `parse(path) -> RawDocument`, `supports(path) -> bool`, `_make_doc_id` helper | **Interface only** |
| `embedder.py` (top) | `IEmbedder` ABC: `embed(texts) -> ndarray`, `dims`, `model_name`; `embed_in_batches` helper | **Interface only** |
| `providers/llm.py` (top) | `ILLMProvider` ABC: `complete(prompt, system, temperature, max_tokens) -> str`, `provider_name`, `model_name` | **Interface only** |
| `providers/storage.py` (top) | `IStorageBackend` ABC: `write_archive(entries, out) -> str`, `read_entry(archive, name) -> bytes`, `list_entries`, `backend_name` | **Interface only** |
| `providers/search.py` (top) | `ISearchProvider` ABC: `build_index(corpus)`, `get_scores(query_tokens) -> list[float]`, `backend_name` | **Interface only** |
| `normalizer.py` | §id assignment + token count + table width-normalisation (consumes `RawDocument` → `Document`) | **No (Slice 3) — informs `Section` fields** |
| `pipeline.py` | Orchestrator; shows how the interfaces compose | Read for context; not ported here |
| `library.py` | `LibraryEntry`/`LibraryManager` catalogue | Later slice |

### 2. Field-by-field domain mapping (Python → C#)
General rules:
- Pydantic `BaseModel` → `public sealed record` with init-only properties.
- `Optional[T]` → nullable (`string?`, `int?`, reference types nullable-enabled).
- `list[T]` with `default_factory` → `IReadOnlyList<T>` initialised to `[]` (never `null`).
- `dict` (`section_index: list[dict]`) → `IReadOnlyList<SectionIndexEntry>` (typed) — *decision:
  replace the loose `dict` with a typed record for safety; JSON shape identical.*
- `bytes` (`Section.embedding`) → `byte[]?`.
- Every property carries `[JsonPropertyName("<python_key>")]` to preserve the `.udf` contract.
- JSON: `System.Text.Json` with source generator; `DefaultIgnoreCondition` chosen so empty lists
  serialise as `[]` and `null` optionals are written as Python would (see open question Q3).

| Python model.field | C# property | JSON key | Notes |
|---|---|---|---|
| `TableData.table_id` | `TableId` | `table_id` | |
| `TableData.caption` | `Caption` (`string?`) | `caption` | |
| `TableData.headers` | `Headers` (`IReadOnlyList<string>`) | `headers` | |
| `TableData.rows` | `Rows` (`IReadOnlyList<IReadOnlyList<string>>`) | `rows` | row len == headers len enforced by normaliser (Slice 3), not the record |
| `ImageRef.image_id/alt/asset_path` | `ImageId/Alt/AssetPath` | same | |
| `Section.id` | `Id` | `id` | value like `§3.1` |
| `Section.title/level/text` | `Title/Level/Text` | same | `Level` 1..6 |
| `Section.tables/images` | `Tables/Images` | same | default `[]` |
| `Section.parent_id/children` | `ParentId/Children` | same | |
| `Section.token_count` | `TokenCount` | `token_count` | default 0 |
| `Section.summary/keywords` | `Summary/Keywords` | same | filled later stages |
| `Section.embedding` | `Embedding` (`byte[]?`) | `embedding` | quantised bytes |
| `Section.section_id` (property alias) | `SectionId => Id` (computed, `[JsonIgnore]`) | — | alias only, not serialised |
| `KeyNumber.label/value/unit/section` | `Label/Value/Unit/Section` | same | |
| `RawDocument.*` | record | same keys | pre-normalisation |
| `DocMeta.owner/department/tags/access_roles/version/last_updated` | record | same | `AccessRoles` default `["*"]`, `Version` default `"1.0"` |
| `Document.*` + `meta` | record | same | `Insights/KeyNumbers` default `[]` |
| `Catalogue.*` | record | same | `Language` default `"en"`, `Quantization` default `"float16"`, `EmbeddingDims` default 0 |

### 3. Wrapper interface mapping (idiomatic .NET)
All interfaces live in `DocNest.Abstractions`. I/O- or inference-bound calls become `async`
and take a `CancellationToken` (NFR: honour cancellation). Pure CPU contracts stay sync.

| Python ABC | C# interface | Signature decisions |
|---|---|---|
| `IParser.parse/supports` | `IParser` | `Task<RawDocument> ParseAsync(string path, CancellationToken ct = default)`; `bool Supports(string path)` |
| `IEmbedder.embed/dims/model_name` | `IEmbedder` | `Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)`; `int Dims { get; }`; `string ModelName { get; }`. *`ndarray` → `IReadOnlyList<float[]>` (jagged); batching is a helper, mirroring `embed_in_batches`.* |
| `ILLMProvider.complete/...` | `ILlmProvider` | `Task<string> CompleteAsync(string prompt, string system = "", double temperature = 0.1, int maxTokens = 512, CancellationToken ct = default)`; `string ProviderName/ModelName { get; }` |
| `IStorageBackend.write_archive/read_entry/list_entries` | `IStorageBackend` | `Task<string> WriteArchiveAsync(IReadOnlyDictionary<string, byte[]> entries, string outputPath, CancellationToken ct = default)`; `Task<byte[]> ReadEntryAsync(string archivePath, string name, CancellationToken ct = default)`; `Task<IReadOnlyList<string>> ListEntriesAsync(string archivePath, CancellationToken ct = default)`; `string BackendName { get; }`. *Python's `str|bytes` entries collapse to `byte[]` (callers UTF-8 encode).* |
| `ISearchProvider.build_index/get_scores` | `ISearchProvider` | `void BuildIndex(IReadOnlyList<IReadOnlyList<string>> corpus)`; `IReadOnlyList<double> GetScores(IReadOnlyList<string> queryTokens)`; `string BackendName { get; }` |

`_make_doc_id` (slug from filename, CamelCase/digit-boundary aware) → a static helper
`DocId.FromPath(string)` in core (pure, unit-testable) — used by parser implementations later.

### 4. Exception hierarchy
`DocNestException : Exception` (base). Subtypes (mirror Python, `Error`→`Exception`):
`ParseException`, `UnsupportedFormatException`, `EmbedException`, `IntelligenceException`,
`UdfWriteException`, `UdfReadException`, `SizeLimitException`, `ConnectorException`,
`QuantizationException`. Each gets the standard three ctors + message parity.

### 5. Project / assembly layout (additive; nothing to break — greenfield)
```
DocNest.sln
Directory.Packages.props        # central pinning (CPM)
Directory.Build.props           # LangVersion, Nullable=enable, TFM net8.0, analyzers
src/
  DocNest.Abstractions/         # interfaces + exceptions (no deps)
  DocNest.Core/                 # records + JsonSerializerContext + DocId helper
tests/
  DocNest.Core.Tests/           # xUnit + FluentAssertions
```
*Decision to ratify in Phase 2 (ADR-0001): split `Abstractions` from `Core`, or keep one
`DocNest` assembly? Leaning two assemblies so extension authors reference only `Abstractions`.*

### 6. Constraints, dependencies, backward-compat surface
- **TFM:** `net8.0` (LTS) — broadest reach incl. ASP.NET/Azure Functions. (SDK 10 installed; multi-target later if needed.)
- **Dependencies this slice:** **zero runtime deps**; test-only `xunit`, `FluentAssertions`,
  `Microsoft.NET.Test.Sdk`, centrally pinned.
- **Backward-compat surface:** the public API is *new* (no existing consumers), so the only
  compatibility contract that bites is the **`.udf` JSON schema** — guarded by AC2/AC3 fixtures
  captured from the Python implementation. `UDF_VERSION` constant mirrored from Python.

### 7. Open questions (resolve at GATE 0 / Phase 2)
- Q1: NuGet package id — `DocNest` (preferred) vs `DocNest.Ai` (matches `docnest-ai`)? Confirm availability.
- Q2: Two assemblies (`Abstractions`+`Core`) vs one `DocNest`? (Recommend two.)
- Q3: `null` optional serialisation — does Python emit `"caption": null` or omit the key? Must
  capture a real Python `.udf` fixture to match exactly (drives `JsonIgnoreCondition`).
- Q4: `IEmbedder` return type — `IReadOnlyList<float[]>` vs `float[,]` vs `ReadOnlyMemory<float>[]`?
  (Recommend `IReadOnlyList<float[]>` for simplicity; revisit if ONNX slice needs contiguous buffers.)
