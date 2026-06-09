# Phase 0.2 — Dev / Technical Document
## Slice 2: `.udf` Read / Write

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### 1. Python reference — read end-to-end (files traced)
| Python file | What it defines | Relevance |
|---|---|---|
| `writer.py` `UDFWriter` | Builds `manifest`/`catalogue`/`content` dicts + `embeddings.bin`; `_sanitise_source`; `UDF_VERSION="1.0"`; compact JSON `separators=(',',':')`, `ensure_ascii=False` | **The write contract** |
| `providers/storage.py` `ZipStorageBackend` | ZIP entries; DEFLATE L9 for JSON/text, L1 for binary, STORED for images; `read_entry`/`list_entries`/`read_json` | **The archive contract** |
| `reader.py` `UDFIndex.load` | `list_entries` → require `manifest.json`; check `udf_version`; `read_json` of manifest/catalogue/content; `quantization`/`embedding_dims` from manifest; `embeddings.bin` lazy | **The read contract** |

### 2. Exact `.udf` wire shapes (the DTOs to model)
All keys are `snake_case`; compact; non-ASCII preserved.

**`manifest.json`**: `udf_version, doc_id, title, source_format, created_at (ISO-8601 UTC),
embedding_model, embedding_dims, quantization, section_count, intelligence (bool),
embedding_format ("binary"), owner, department, tags, access_roles, version, last_updated,
producer ("docnest-ai 1.0")`. *.NET sets `producer` to e.g. `"docnest-dotnet 0.1"` — **open
question Q1** (does Python care? It does not read `producer`; safe to differ).*

**`catalogue.json`**: `doc_id, title, source, language ("en"), summary, insights, owner,
department, tags, access_roles, version, last_updated, key_numbers:[{label,value,unit,section}],
section_index:[{id,title,level,parent_id,children,summary,keywords,token_count}],
embedding_model, embedding_dims, quantization`.

**`content.json`**: `{doc_id, sections:{ "<§id>": {title, level, text,
tables:[{table_id,caption,headers,rows}], images:[{image_id,alt,asset_path}] } }}`.

**`embeddings.bin`**: raw concatenation of per-section quantised vectors, row-major,
`(n_sections × stride)` bytes, `stride = dims × bytes_per_element`. float16 ⇒ 2 bytes/elt.
Sections with no embedding contribute a zero block. *Slice 2: write only when supplied; otherwise
omit the entry and set `embedding_dims:0`.*

### 3. Design surface to add (idiomatic .NET, all behind wrappers)
- **Wire DTOs** in a new `DocNest.Core/Udf/` area: `ManifestDto`, `CatalogueDto`, `ContentDto`,
  `ContentSectionDto`, `TableDto`, `ImageDto`, `KeyNumberDto`, `SectionIndexDto` — `record`s with
  exact `[JsonPropertyName]`, added to `DocNestJson` source-gen context. (Distinct from the Slice-1
  domain records, per ADR-0001.)
- **`ZipStorageBackend : IStorageBackend`** in `DocNest.Storage` (new assembly, refs `Abstractions`)
  — wraps `System.IO.Compression.ZipArchive`. `WriteArchiveAsync` (DEFLATE for text, Stored/light
  for binary/images), `ReadEntryAsync`, `ListEntriesAsync`. `DirectoryStorageBackend` optional (debug).
- **`UdfWriter`** (`DocNest.Core/Udf/`) — maps `Document` → the three DTOs (+ optional embeddings
  bytes) → `IStorageBackend.WriteArchiveAsync`. Ports `_sanitise_source` (basename-only by default).
- **`UdfReader`** — `IStorageBackend.ListEntries`/`ReadEntry` → validate version → deserialise DTOs →
  reconstruct `Document` (and expose raw DTOs for retrieval later).
- **JSON**: a dedicated serialiser options instance with `Encoder =
  JavaScriptEncoder.UnsafeRelaxedJsonEscaping` so `§` and other non-ASCII are emitted raw (match
  Python `ensure_ascii=False`); `WriteIndented=false` (compact). *This differs from the Slice-1
  domain context, which is fine — the wire DTOs own their own options.*

### 4. Mapping notes / gotchas
- `content.json.sections` is a **map keyed by §id**, not a list → `Dictionary<string, ContentSectionDto>`
  (preserve insertion order with the source generator; STJ keeps dictionary order).
- `manifest.created_at` → `DateTimeOffset.UtcNow` ISO-8601. Round-trip need not preserve it.
- `_sanitise_source`: keep URLs (contain `://`) verbatim; else basename via split on `/` and `\`.
- Compression level is **not** part of the interop contract (any valid ZIP works); we mirror Python's
  choices for parity but tests must not assert exact bytes — assert *semantic* equality of entries.
- Embeddings stride uses the same quantization vocabulary (`float16` default) — full quantizer is
  Slice 6; Slice 2 only needs `bytes_per_element` for the float16/float32 cases to read/write a blob.

### 5. New assembly / layout
```
src/DocNest.Storage/         # ZipStorageBackend (+ Directory), refs Abstractions
src/DocNest.Core/Udf/        # wire DTOs, UdfWriter, UdfReader, source-sanitise
tests/DocNest.Storage.Tests/ # ZIP backend unit tests
tests/DocNest.Core.Tests/Udf/# DTO + writer/reader + interop fixture tests
tests/fixtures/              # golden Python-produced sample.udf (+ a tiny doc)
```
*Decision to ratify (ADR-0002): wire DTOs in `Core` vs a new `DocNest.Udf` assembly; whether
`ZipStorageBackend` is its own assembly (recommended: yes — keeps `System.IO.Compression` behind the
`IStorageBackend` wrapper, isolated from `Core`).*

### 6. Backward-compat surface
- **`UDF_VERSION` = "1.0"** (already in `Udf.Version`) — the gate value on read.
- The `.udf` JSON schema is the contract; the golden fixtures are the regression guard.
- Public API additions only (new types); no Slice-1 type changes expected.

### 7. Open questions (resolve GATE 0 / Phase 2)
- Q1: `producer` string for .NET (`"docnest-dotnet x.y"`?) — cosmetic; Python ignores it.
- Q2: Generate the golden fixture by running Python `docnest` locally (needs its deps) vs hand-craft a
  minimal spec-accurate `.udf`. Prefer **generate from Python** for true fidelity; fall back to
  hand-crafted if Python env unavailable.
- Q3: Wire DTOs in `DocNest.Core` vs new `DocNest.Udf` assembly (ADR-0002).
- Q4: Do we reconstruct `Document` from `catalogue+content` (lossy: `section_index` has no text;
  text is in `content`) — confirm the merge rule (join by §id: catalogue gives summary/keywords,
  content gives text/tables/images).
