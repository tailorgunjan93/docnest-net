# Phase 0.3 — QA / User Document
## Slice 1: Core Domain Model + Wrapper Contracts

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

> **Phase 1 refinement (post-GATE 0):** reading `writer.py` revealed the `.udf` wire format is a
> hand-built projection, not a domain dump. The interop golden-fixture tests **I1 and I2 move to
> Slice 2** (where the wire DTOs they validate are introduced). Slice 1 keeps U1–U6, I3–I4, F1–F2,
> plus a record self-round-trip test. See [Impact & Risk](SLICE-01-IMPACT-RISK.md) and
> [ADR-0001](../adr/0001-domain-records-and-wrapper-interfaces.md).

---

### What "working" means to the consumer
A .NET developer (and every later DocNest slice) can reference the `DocNest` package, build
the domain objects, hand them to/receive them from the five interfaces, and serialise them to
JSON that the Python `docnest` reads without complaint. "Working" for Slice 1 is **shape +
serialisation fidelity + contract clarity** — there is no parsing/embedding behaviour to
exercise yet, so the test surface is the data contracts and the interface signatures.

### How a user will exercise it
1. `dotnet add package DocNest` (later); for now, `ProjectReference`.
2. `new Section(...)`, `new Document(...)`, `new Catalogue(...)`.
3. `JsonSerializer.Serialize(catalogue, DocNestJson.Default.Catalogue)`.
4. `JsonSerializer.Deserialize<Catalogue>(pythonCatalogueJson, ...)`.
5. Implement `IEmbedder`/`IParser`/etc. in their own code against the contracts.

### Test plan (test-first — these are written in Phase 3 and must fail first)

**Unit — domain records**
- U1: Each record constructs and exposes every Python field (one focused test per model).
- U2: Defaults match Python exactly — `DocMeta.AccessRoles == ["*"]`, `Version == "1.0"`,
  `Catalogue.Language == "en"`, `Quantization == "float16"`, `Section.TokenCount == 0`,
  collections default to empty (never `null`).
- U3: `Section.SectionId` equals `Section.Id` and is **not** serialised (`[JsonIgnore]`).
- U4: `DocId.FromPath` matches Python `_make_doc_id` on a table of cases:
  `GunjanTailor → gunjan-tailor`, `Report2024 → report-2024`, `my_file name → my-file-name`,
  leading/trailing separators stripped.

**Unit — exceptions**
- U5: Every specific exception is assignable to `DocNestException` (broad-catch works).
- U6: Each exception preserves message + inner exception.

**Integration / serialisation fidelity (the heart of this slice)**
- I1 (key parity): Serialising a fully-populated `Catalogue`/`Document`/`Section` produces JSON
  whose key set, at every level, **exactly equals** the keys in a fixture captured from Python.
  No extra keys, none missing, no camelCase leakage.
- I2 (round-trip from Python): Deserialise a real `catalogue.json` extracted from a Python-built
  `.udf`; assert field values; re-serialise; assert key-for-key equality with the original.
- I3 (forward-compat): JSON containing an unknown extra key deserialises without throwing.
- I4 (optional handling): A document with `caption`/`summary`/`embedding` absent deserialises to
  the correct null/default; serialising back matches Python's null-vs-omit behaviour (Dev Q3).

**Functional — contract usability**
- F1: A hand-written test double implementing `IEmbedder` returns `IReadOnlyList<float[]>` and is
  accepted anywhere the interface is expected (proves the contract is implementable).
- F2: Same for `IParser`, `IStorageBackend`, `ISearchProvider`, `ILlmProvider` (trivial fakes).

### Data variety / fixtures
- A captured `catalogue.json` from a Python-built `.udf` (small doc) — the **golden fixture**
  for I1/I2. *Action: generate via Python `docnest` during Phase 3 setup and commit under
  `tests/fixtures/`.*
- Synthetic edge models: empty document (no sections), section with multiple tables, table with
  zero headers, section with a quantised `embedding` byte blob.

### Edge / negative cases
- Empty `sections` list → serialises as `[]`, not `null`.
- `embedding` round-trips as base64-equivalent bytes without corruption.
- Deserialising malformed JSON throws a `JsonException` (not a crash) — callers can guard.
- Unicode in titles/§ids (`§3.1`, non-ASCII headings) survives round-trip (UTF-8).

### What would constitute a regression (seeds the regression suite)
- Any JSON key rename/case change on a domain property (breaks `.udf` interop) — **highest severity**.
- A default value drifting from Python.
- A collection property serialising as `null`.
- `SectionId` accidentally appearing in JSON.
- An interface signature change that silently breaks an existing implementor.

The golden-fixture key-parity test (I1) + the Python round-trip test (I2) become permanent
regression tests; they are the tripwire that protects the cross-ecosystem `.udf` contract for
every future slice.
