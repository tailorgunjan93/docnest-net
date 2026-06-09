# Phase 1 — Impact & Risk Analysis
## Slice 1: Core Domain Model + Wrapper Contracts

**Status:** Phase 1 (awaiting GATE 1, presented together with GATE 2) · **Owner:** Gunjan
**GATE 0:** ✅ signed off. Package id **`DocNest`**, two assemblies (`Abstractions` + `Core`).

---

### Blast radius (every area this slice touches)
Greenfield repository — there is **no existing .NET consumer** to break. The slice adds new
assemblies only; it edits nothing pre-existing.

| Area | Change | Breaking? |
|---|---|---|
| `DocNest.Abstractions` (new) | 5 interfaces + exception hierarchy | No — new |
| `DocNest.Core` (new) | domain records + JSON context + `DocId` + `UDF_VERSION` | No — new |
| `DocNest.Core.Tests` (new) | xUnit suite | No — new |
| Solution / build props (new) | `.sln`, `Directory.Build.props`, `Directory.Packages.props` | No — new |
| **`.udf` JSON schema (live cross-ecosystem contract)** | none written this slice | **Watch-item** |

### The one real risk: the `.udf` contract — and a finding that refines this slice
**Finding (from reading `docnest/writer.py` end-to-end):** the `.udf` on-disk format is **not**
a serialisation of the domain models. `UDFWriter` hand-builds three dicts:
- `manifest.json` — version/embedding config + flattened `DocMeta` + `producer`.
- `catalogue.json` — doc header + flattened `DocMeta` + `key_numbers` + a **`section_index`**
  whose entries are `{id,title,level,parent_id,children,summary,keywords,token_count}` — i.e. a
  **projection** of `Section` (no `text`, no `tables`, no `embedding`).
- `content.json` — `{doc_id, sections:{<id>:{title,level,text,tables,images}}}` (the heavy text).
- `embeddings.bin` — raw quantised vectors (not in any JSON).

**Consequence:** the wire `.udf` schema is a **distinct contract** from the in-memory
`Section`/`Document`. Modelling the wire format belongs to **Slice 2** (`.udf` read/write) via
dedicated DTOs with exact `[JsonPropertyName]`, validated against golden Python fixtures there.

**Refinement to Slice 1 scope (carries into the QA test plan):**
- Slice 1 stays = **persistence-ignorant domain records + interfaces + exceptions** (+ `DocId`,
  `UDF_VERSION`). This is unchanged in substance from the GATE-0 BA doc.
- The interop **golden-fixture tests I1/I2** (validate JSON against a real Python `.udf`) **move
  to Slice 2**, where the wire DTOs they test are introduced. Slice 1 keeps lighter serialisation
  round-trip tests on the records themselves (self-consistency), since the records are not the
  authoritative wire format.
- `UDF_VERSION = "1.0"` constant is mirrored now (single source of truth for later slices).

This refinement *reduces* Slice 1 risk (it removes a dependency on capturing/validating the full
ZIP wire format before any code) and sharpens the Slice 2 boundary. It is recorded in **ADR-0001**.

### Backward-compatibility plan
- No public API exists yet → no API-compat concern this slice.
- The `.udf` schema is not written this slice → cannot be broken this slice. The guard rails
  (golden Python fixtures) are introduced with the DTOs in Slice 2, exactly where they bite.
- `UDF_VERSION` constant matches Python (`"1.0"`); any future bump is an ADR-gated event.

### Risk / Impact classification
- **Impact = Low.** New, additive assemblies; zero existing consumers; no behaviour wired yet.
- **Risk = Low.** Zero runtime dependencies; pure BCL; the only contract (wire `.udf`) is
  explicitly deferred to Slice 2 with its own gates. Mitigations: NRT enabled, analyzers on,
  records immutable, full xUnit suite from day one.

**GATE 1 result (proposed):** Impact map written; **Risk = Low, Impact = Low**; backward-compat
plan stated; the `.udf`-contract watch-item is mitigated by deferring wire DTOs to Slice 2.
→ Eligible to proceed to Phase 2 design. *(Presented for sign-off together with GATE 2.)*
