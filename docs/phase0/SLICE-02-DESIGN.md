# Phase 2 — Design (DSA + Architecture)
## Slice 2: `.udf` Read / Write

**Status:** Phase 2 (awaiting GATE 2) · **Owner:** Gunjan · **ADR:** [ADR-0002](../adr/0002-udf-wire-dtos-and-storage-backend.md)

---

### 1. DSA pass (efficiency + memory)
| Element | Time | Space | Why optimal |
|---|---|---|---|
| ZIP write/read | O(total bytes) | **O(entry stream buffer)** | stream each entry via `ZipArchiveEntry.Open()`; never hold two full copies |
| JSON (de)serialise | O(payload) | O(payload) | source-gen `UdfJson` context; `…Async(Stream)` avoids a giant intermediate string |
| `content.sections` map | O(sections) | O(sections) | `Dictionary<string,ContentSectionDto>` keyed by §id (insertion-ordered) |
| `embeddings.bin` read | O(n×dims) | O(n×dims) | `byte[]` block, `MemoryMarshal`/`BinaryPrimitives` for float16↔float32 |

**Bounded-memory NFR:** entries are streamed; the only inherently large buffers are `content.json`
and `embeddings.bin`, both O(document) and unavoidable — peak stays proportional to one entry, not
the whole archive twice.

### 2. Architect pass — SOLID
- **SRP:** `ZipStorageBackend` = archive bytes only; `UdfWriter` = Document→DTO→entries; `UdfReader`
  = entries→DTO→Document; DTOs = wire shape only.
- **OCP:** new storage formats (dir, S3) implement `IStorageBackend` without touching writer/reader.
- **LSP:** `UdfWriter`/`UdfReader` work against any `IStorageBackend` (proved by a fake/dir backend test).
- **ISP:** `IStorageBackend` stays the 4-member contract from Slice 1 — unchanged.
- **DIP:** `UdfWriter`/`UdfReader` depend on `IStorageBackend` (ctor-injected; default `ZipStorageBackend`).

### 3. Patterns
- **Strategy** — `IStorageBackend` (zip/dir/…).
- **Builder** — `UdfWriter` assembles the DTO set then emits entries.
- **Repository/Gateway** — `UdfReader` loads + reconstructs.
- **DTO / Anti-Corruption Layer** — wire DTOs isolate the on-disk schema from the domain (ADR-0001/0002).

### 4. Wrapper boundaries
- `System.IO.Compression` lives **only** in `DocNest.Storage`, behind `IStorageBackend`.
- `System.Text.Json` for the wire format lives in `DocNest.Core/Udf/` behind `UdfWriter`/`UdfReader`
  + the `UdfJson` context.

### 5. JSON strategy for the wire format (decision)
- New `UdfJson : JsonSerializerContext` with `[JsonSourceGenerationOptions(WriteIndented=false,
  DefaultIgnoreCondition = Never)]` and **options.Encoder = `UnsafeRelaxedJsonEscaping`** applied at
  use-site (the encoder isn't a source-gen-options field, so set it on the `JsonSerializerOptions`
  the context wraps / via a configured `JsonSerializerOptions` carrying the context's resolver).
- Writes `null`s (matches Python `json.dumps`); compact; raw non-ASCII. This is deliberately
  different from the domain `DocNestJson` (which omits nulls) — wire ≠ domain.

### 6. Document ↔ wire mapping + reconstruction rule
- **Write:** `Document` → `ManifestDto` (version, counts, flattened `Meta`, `producer="docnest-dotnet 0.1"`,
  `created_at=DateTimeOffset.UtcNow`), `CatalogueDto` (header + flattened `Meta` + `key_numbers` +
  `section_index` projected from each `Section`: id/title/level/parent_id/children/summary(""→ from
  `Summary`)/keywords/token_count), `ContentDto` (`sections[§id] = {title,level,text,tables,images}`).
  `embeddings.bin` only when any `Section.Embedding` is non-null.
- **Source sanitise:** port `_sanitise_source` — URLs (`://`) verbatim; else basename (split `/` and `\`).
- **Read/reconstruct `Document`:** join `catalogue.section_index` ⟕ `content.sections` by §id —
  catalogue gives summary/keywords/parent_id/children, content gives text/tables/images; `Embedding`
  from `embeddings.bin` row when present. Manifest gives format/meta.

### 7. Code plan — file by file (signatures)
**`DocNest.Storage`** (refs `Abstractions`; `PackageReference` none — `System.IO.Compression` is BCL)
- `ZipStorageBackend.cs` — implements `IStorageBackend`; DEFLATE for `.json`/text, `Stored` for
  `.bin`, `Stored` for precompressed image exts; `BackendName => "zip"`.
- `DirectoryStorageBackend.cs` (optional, debug) — `BackendName => "dir"`.

**`DocNest.Core/Udf/`** (refs `Abstractions`)
- `Wire/ManifestDto.cs`, `CatalogueDto.cs`, `ContentDto.cs`, `ContentSectionDto.cs`, `TableDto.cs`,
  `ImageDto.cs`, `KeyNumberDto.cs`, `SectionIndexDto.cs` — `record`s, exact `[JsonPropertyName]`.
- `UdfJson.cs` — `JsonSerializerContext` over the DTOs (Never + relaxed encoder at use-site).
- `UdfWriter.cs` — `Task<string> WriteAsync(Document doc, string outputPath, bool includeSourcePath=false, IReadOnlyList<byte[]>? embeddings=null, CancellationToken ct=default)`; ctor takes `IStorageBackend? storage=null`.
- `UdfReader.cs` — `static Task<UdfPackage> LoadAsync(string udfPath, IStorageBackend? storage=null, CancellationToken ct=default)`; validates `udf_version`. `UdfPackage` exposes `Manifest`, `Catalogue`, `Content`, and `ToDocument()`.
- `SourceSanitiser.cs` — `static string Sanitise(string source, bool keepFull=false)`.

**Tests**
- `tests/DocNest.Storage.Tests/ZipStorageBackendTests.cs` (U4–U6).
- `tests/DocNest.Core.Tests/Udf/` — DTO key tests (U1–U3), writer/reader round-trip + version gate
  (I1–I4), interop E1 (golden) + E2 (guarded Python).
- `tests/fixtures/sample.udf` (+ source doc + a generator script `tools/make_fixture.py`).

### 8. Resolved open questions
- Q1 `producer` → `"docnest-dotnet 0.1"` (Python ignores it). Q3 → wire DTOs in `DocNest.Core/Udf`,
  `ZipStorageBackend` in its **own** `DocNest.Storage` assembly. Q4 → reconstruct by §id join (above).
  Q2 (fixture) → generate from Python; **Phase-3 logistics flagged** (Python not on dev PATH).

**GATE 2 (proposed):** complexity stated; SOLID/patterns justified; wrapper boundaries defined;
file-by-file plan with signatures; ADR-0002 recorded. → proceed to Phase 3 (test-first).
