# ADR-0001 — Domain as records, wrapper interfaces, and domain/wire separation

- **Status:** Accepted (pending GATE 2 owner sign-off)
- **Date:** 2026-06-09
- **Context slice:** Slice 1 — Core domain + contracts
- **Deciders:** Gunjan (owner)

## Context
DocNest is being ported from Python (`docnest-ai` v0.7.0) to an idiomatic .NET library. The Python
domain is Pydantic v2 models; the extension points are ABCs (`IParser`, `IEmbedder`, `ILLMProvider`,
`IStorageBackend`, `ISearchProvider`). Reading `docnest/writer.py` showed the `.udf` on-disk format
is **hand-built** (manifest/catalogue/content dicts + `embeddings.bin`) and is **not** a dump of the
domain models — `section_index` is a projection of `Section`, heavy text lives in `content.json`, and
embeddings live in a binary blob. The `.udf` schema + `UDF_VERSION` is the cross-ecosystem contract
that must stay byte-compatible with Python.

## Decision
1. **Domain as immutable `record` types**, nullable-reference-types enabled, collections exposed as
   `IReadOnlyList<T>` defaulting to `[]` (never `null`). The records are **data contracts** and live
   in `DocNest.Abstractions` (corrected during Phase 4 — see note below).
2. **Five wrapper interfaces** in `DocNest.Abstractions`; I/O- and inference-bound contracts are
   `async` with `CancellationToken`; `IEmbedder` returns `IReadOnlyList<float[]>`.
3. **Two assemblies:** `DocNest.Abstractions` = data-contract records + interfaces + exceptions +
   the `Udf.Version` constant (zero **package** deps; `System.Text.Json` attributes are BCL).
   `DocNest.Core` = serialisation (`DocNestJson` source-gen context), helpers (`DocId`), and the
   future engine; references `Abstractions`. **Extension authors reference only `Abstractions`** and
   get both the data types and the interfaces they implement.

   > **Phase 4 correction (2026-06-09):** the original wording placed records in `Core`. Because
   > `IParser` (in `Abstractions`) returns `RawDocument`, records must be visible to `Abstractions`;
   > putting them in `Core` would create an assembly cycle. Moving records into `Abstractions`
   > resolves it and better serves the "extension authors reference only `Abstractions`" goal.
4. **Package id / namespace `DocNest`.**
5. **Domain ≠ wire format.** Domain records are persistence-ignorant. The `.udf` wire DTOs
   (`ManifestDto`/`CatalogueDto`/`ContentDto`/…) with exact `[JsonPropertyName]` are introduced in
   **Slice 2** behind `IStorageBackend`, and validated against golden fixtures captured from the
   Python build. The interop fidelity tests therefore live in Slice 2, not Slice 1.
6. **`System.Text.Json` source-generated** serialisation (`DocNestJson` context); explicit
   `[JsonPropertyName]` snake_case to lock keys; AOT-friendly, reflection-free.
7. **TFM `net8.0`** (LTS); central package management via `Directory.Packages.props`.

## Consequences
**Positive:** clean separation of concerns; a `.udf` schema change never ripples into the domain;
extension authors get a tiny dependency surface; reflection-free JSON aids startup/AOT; immutability
makes the domain thread-safe and test-friendly.
**Negative / cost:** an explicit domain↔wire mapping layer must be written in Slice 2 (more code than
reusing models as DTOs), and `[JsonPropertyName]` must be kept in lockstep with the Python keys —
mitigated by the Slice-2 golden-fixture regression tests.
**Neutral:** `net8.0` chosen over `net10.0` for reach despite SDK 10 being installed; multi-targeting
can be added later without breaking consumers.

## Alternatives considered
- *Single assembly:* simpler, but forces extension authors to depend on all of Core. Rejected.
- *Reuse domain records as `.udf` wire DTOs (as Python loosely does):* couples the domain to the disk
  format; `section_index`/`content`/`embeddings.bin` split makes a 1:1 mapping impossible anyway. Rejected.
- *Reflection-based `JsonSerializer`:* simpler attributes-only setup but slower, alloc-heavy, not
  AOT-safe. Rejected in favour of the source generator.
- *Mutable classes:* familiar but loses value-equality and thread-safety; records are idiomatic here.
