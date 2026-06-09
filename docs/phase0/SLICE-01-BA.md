# Phase 0.1 — BA / Functional Document
## Slice 1: Core Domain Model + Wrapper Contracts

**Status:** Phase 0 (awaiting GATE 0 sign-off) · **Owner:** Gunjan · **Risk/Impact (expected):** Low/Low

---

### WHY — the business/user problem and goal
DocNest exists as a Python library (`docnest-ai`). .NET/C# teams cannot consume it without a
Python runtime or a brittle subprocess bridge. The program goal is a native .NET DocNest on
NuGet. **This first slice lays the foundation every later slice depends on:** the data
contracts that flow between pipeline stages, and the wrapper interfaces that keep every
external library out of the core. Nothing else can be built correctly until these are right,
because they *are* the public shape of the library and the `.udf` schema.

We deliberately start here (zero external dependencies) so the idiomatic-.NET architecture is
proven and reviewable before any heavy ONNX/OpenXML/PDF work is committed to.

### WHAT — exact functional behaviour required
A consumer of the eventual library, and every later slice, must be able to:

1. **Represent a normalised document** with the same fields and meaning as the Python models:
   `TableData`, `ImageRef`, `Section`, `KeyNumber`, `RawDocument`, `DocMeta`, `Document`,
   `Catalogue`.
2. **Serialise/deserialise** those models to/from JSON using the **exact same key names** the
   Python implementation writes into a `.udf` (e.g. `doc_id`, `key_numbers`, `parent_id`,
   `section_index`, `last_updated`). This is the cross-ecosystem contract.
3. **Depend on stable abstractions**, not concretions, for the five extension points:
   `IParser`, `IEmbedder`, `ILlmProvider`, `IStorageBackend`, `ISearchProvider`.
4. **Catch DocNest errors** through a single base exception (`DocNestException`) with the same
   specific subtypes as Python (`ParseException`, `UnsupportedFormatException`, …).

**User-visible before vs after**
- *Before:* no .NET package exists; the domain shape lives only in Python.
- *After (this slice):* a `DocNest` assembly exposes the domain records, the five interfaces,
  and the exception hierarchy — compiling, documented, JSON-round-tripping, unit-tested. No
  parsing/embedding behaviour yet (those are later slices behind these interfaces).

**Acceptance criteria (functional)**
- AC1: Each domain record exists with fields 1:1 to the Python model (name + meaning + default).
- AC2: Serialising a `Catalogue`/`Document`/`Section` produces JSON whose keys exactly match the
  Python output keys (verified against a fixture captured from Python).
- AC3: Deserialising a Python-produced `catalogue.json` fixture yields an equal object
  (round-trip: deserialize → serialize → key-for-key match).
- AC4: The five wrapper interfaces exist with method signatures faithful to the Python ABCs,
  adapted idiomatically (async where I/O-bound; `IReadOnlyList<T>` over `list[T]`).
- AC5: `DocNestException` base + the eight specific subtypes exist and are catchable broadly.
- AC6: Defaults match Python (e.g. `access_roles` defaults to `["*"]`, `quantization` to
  `"float16"`, `version` to `"1.0"`, `language` to `"en"`).

### Non-goals (explicitly out of scope for Slice 1)
- No parser, embedder, LLM, storage, or search **implementations** — interfaces only.
- No `.udf` ZIP read/write (that is Slice 2).
- No pipeline/normaliser logic (Slice 3).
- No CLI, no NuGet publish yet.

### HOW — functional flow & scenarios (no implementation detail)
- *Scenario A — model author:* a later slice constructs a `Section` with a title, level, text,
  tables; reads it back; the fields behave exactly as the Python `Section` does (e.g.
  `SectionId` aliases `Id`).
- *Scenario B — interop:* a `.udf` `catalogue.json` produced by Python `docnest` is loaded into
  a .NET `Catalogue` and every field is populated correctly (no missing/renamed keys).
- *Scenario C — extension author:* a developer implements `IEmbedder` to plug in a custom model
  without referencing any concrete DocNest type beyond the contracts.
- *Edge cases to honour:* optional fields absent in JSON → defaults applied; unknown extra keys
  in JSON → ignored, not fatal (forward-compat); empty collections never serialise as `null`.

### Traceability to Charter
Serves **Compatibility** (the headline KPI for a port) directly, and sets up **Privacy**,
**Cost**, and **Speed** by making every heavy dependency injectable behind a wrapper.
