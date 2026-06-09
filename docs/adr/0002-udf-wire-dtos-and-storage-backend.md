# ADR-0002 — `.udf` wire DTOs, storage backend boundary, and wire JSON options

- **Status:** Accepted (pending GATE 2 owner sign-off)
- **Date:** 2026-06-09
- **Context slice:** Slice 2 — `.udf` read/write
- **Deciders:** Gunjan (owner)
- **Builds on:** [ADR-0001](0001-domain-records-and-wrapper-interfaces.md) (domain ≠ wire)

## Context
The `.udf` is the cross-ecosystem contract. Reading the Python `writer.py`/`storage.py`/`reader.py`
established the exact archive: `manifest.json`, `catalogue.json`, `content.json`, optional
`embeddings.bin`; compact JSON with `ensure_ascii=False`; Python `json.dumps` **writes `null`** for
`None`; the read path gates on `udf_version == "1.0"`; `content.sections` is a **map keyed by §id**.

## Decision
1. **Dedicated wire DTOs** (`ManifestDto`, `CatalogueDto`, `ContentDto`, `ContentSectionDto`,
   `TableDto`, `ImageDto`, `KeyNumberDto`, `SectionIndexDto`) model the on-disk schema exactly
   (`[JsonPropertyName]`), separate from the Slice-1 domain records (anti-corruption layer).
2. **Wire DTOs live in `DocNest.Core/Udf/`**; **`ZipStorageBackend` lives in a new `DocNest.Storage`
   assembly** so `System.IO.Compression` stays behind the `IStorageBackend` wrapper and out of `Core`.
3. **Separate `UdfJson` serialiser** for the wire format: `WriteIndented=false`,
   `DefaultIgnoreCondition = Never` (write `null`s — matches Python), and
   `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (raw non-ASCII — matches `ensure_ascii=False`).
   This intentionally differs from the domain `DocNestJson` (which omits nulls): **wire ≠ domain.**
4. **`UdfWriter`/`UdfReader` depend on `IStorageBackend`** (constructor-injected; default
   `ZipStorageBackend`). `UdfReader.LoadAsync` validates the version and returns a `UdfPackage`
   exposing the three DTOs plus `ToDocument()`.
5. **Reconstruction rule:** rebuild a `Document` by joining `catalogue.section_index` with
   `content.sections` on §id (catalogue → summary/keywords/hierarchy; content → text/tables/images;
   `embeddings.bin` row → `Section.Embedding` when present).
6. **`producer` = `"docnest-dotnet 0.1"`** (Python never reads it; safe to differ).
7. **Streaming I/O:** entries are (de)serialised via the ZIP entry stream to keep memory bounded.

## Consequences
**Positive:** the on-disk schema is fully owned by DTOs with exact-match JSON options → I3 key parity
and E1 interop are testable and stable; `System.IO.Compression` is isolated behind the wrapper;
domain stays persistence-ignorant; streaming honours the bounded-memory NFR.
**Negative / cost:** a second JSON configuration (`UdfJson`, write-nulls, relaxed encoder) and an
explicit Document↔DTO mapping layer to maintain; the golden fixture must be regenerated if the Python
schema ever changes.
**Neutral:** `DirectoryStorageBackend` is optional (debug) and may be deferred.

## Alternatives considered
- *Reuse domain records as wire DTOs:* impossible/loss-prone — `content`/`section_index`/`embeddings`
  split, and the domain omits nulls while the wire writes them. Rejected (consistent with ADR-0001).
- *One JSON context for domain + wire:* a context's null-handling/encoder are fixed; the two formats
  need different settings. Rejected → two contexts.
- *`ZipStorageBackend` inside `Core`:* would pull `System.IO.Compression` into the core assembly,
  violating the wrapper rule. Rejected → own `DocNest.Storage` assembly.
- *Match Python's exact compression levels as a contract:* unnecessary — any valid ZIP interoperates;
  tests assert semantic entry equality, not bytes. Levels mirrored only for parity, not asserted.
