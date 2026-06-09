# Phase 0.4 — Roadmap: Slice 2 (`.udf` Read / Write)

> Consolidates the Slice-2 BA/Dev/QA into ordered steps. The program roadmap
> ([../ROADMAP.md](../ROADMAP.md)) is unchanged; this is Slice 2's Phase 1→7 sequence.
> **Reaching M1** (a `.udf` round-trips between Python and .NET on synthetic docs) needs Slices 1–3;
> Slice 2 delivers the archive + interop half of it.

## Ordered steps (what GATE 0 approves entering Phase 1 for)
1. **Phase 1 (Impact & risk):** additive (new `DocNest.Storage` assembly + `Core/Udf/` types). The
   live contract is the `.udf` schema → mitigation = golden Python fixture (E1) + key-parity (I3).
   Watch-item: non-ASCII encoding + bounded-memory streaming of `content.json`.
2. **Phase 2 (Design + ADR-0002):** resolve Dev Q1–Q4; decide wire-DTO home and the
   `ZipStorageBackend` assembly boundary; define the `Document ↔ DTO` mapping and the
   `catalogue+content` merge-by-§id rule; file-by-file code plan.
3. **Phase 3 (Test-first):**
   - **Capture the golden `sample.udf`** from Python `docnest` (tiny doc with table + non-ASCII +
     key numbers); commit under `tests/fixtures/`. *If the Python env is unavailable, hand-craft a
     spec-accurate fixture and flag for later replacement.*
   - Write U1–U6, I1–I4, E1 (and E2 guarded) — failing first.
4. **Phase 4 (Implement):** wire DTOs + `DocNestJson` additions; `ZipStorageBackend`; `UdfWriter`;
   `UdfReader`; `_sanitise_source` port — make tests pass.
5. **Phase 5 (Verify):** full suite (Slice 1 + Slice 2) green; E1 interop holds; no key drift.
6. **Phase 6:** any defect → regression + unit test then fix.
7. **Phase 7:** branch `slice/02-udf-io` → green → merge `main`; CHANGELOG; tag `v0.0.2-udf`
   (no NuGet publish — still deferred to Slice 7 / M4).

**Risk/impact expectation:** Low/Low — additive, no Slice-1 type changes; the single watch-item is
`.udf` schema/encoding fidelity, covered by fixtures + key-parity before any code.

## Decisions needed from owner at GATE 0
- Approve the four Slice-2 Phase 0 documents and the step order, **or** adjust.
- Direction on Dev Q2 (generate golden fixture from Python vs hand-craft) — affects Phase 3 setup.
- Optional: Dev Q3 (wire DTOs in `Core` vs new `DocNest.Udf` assembly) — can defer to ADR-0002.
