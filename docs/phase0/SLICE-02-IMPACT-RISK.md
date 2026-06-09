# Phase 1 — Impact & Risk Analysis
## Slice 2: `.udf` Read / Write

**Status:** Phase 1 (presented with GATE 2) · **Owner:** Gunjan
**GATE 0:** ✅ signed off. Golden fixture = **generate from Python `docnest`**.

---

### Blast radius
Additive. No Slice-1 type changes expected.

| Area | Change | Breaking? |
|---|---|---|
| `DocNest.Storage` (new assembly) | `ZipStorageBackend` (+ optional `DirectoryStorageBackend`) | No — new |
| `DocNest.Core/Udf/` (new) | wire DTOs, `UdfWriter`, `UdfReader`, `UdfJson` context, source-sanitise | No — new |
| `DocNest.Core` `DocNestJson` | unchanged (wire DTOs get a **separate** `UdfJson` context) | No |
| Test projects + `tests/fixtures/` | new tests + golden `.udf` | No — new |
| **`.udf` JSON schema + `UDF_VERSION`** | the live contract this slice implements | **Watch-item** |

### Watch-items & mitigations
1. **Non-ASCII fidelity (`§`, accents).** Python writes `ensure_ascii=False` (raw UTF-8). Mitigation:
   the `UdfJson` options use `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`; E1/round-trip tests assert
   `§` survives both directions.
2. **Null-vs-omit key parity.** Python `json.dumps` **writes `null`** for `None` (e.g. `unit`,
   `caption`, `parent_id`). To pass I3 (exact key parity vs the golden fixture), the `UdfJson` context
   uses `DefaultIgnoreCondition = Never` (write nulls) — *opposite* to the Slice-1 domain context
   (`WhenWritingNull`). This is why the wire DTOs own their serialiser options (ADR-0002).
3. **Bounded memory on large `content.json`.** Mitigation: read/write via the ZIP entry **stream**
   (`JsonSerializer.Serialize/DeserializeAsync(stream, …)`), not a buffered full-string round-trip.
4. **Golden-fixture generation env.** Python is **not on PATH in the dev shell** (`python`/`py`/venv
   not found), though the Python `docnest` exists at `D:\Learning\docnest` (has `dist/`). **Phase-3
   risk.** Mitigations, in order: (a) owner points me to their Python / runs the one-shot generator
   script I provide; (b) fall back to a hand-crafted spec-accurate fixture, **flagged** for
   replacement once a real one is available. Either way E1 stays a committed static fixture.

### Backward-compatibility plan
- `UDF_VERSION = "1.0"` is the read gate (already `Udf.Version`). Any future bump → ADR + version note.
- The golden Python fixture (E1) + key-parity (I3) are the regression guards on the schema.
- Public API additions only; no Slice-1 signatures change.

### Risk / Impact classification
- **Impact = Low.** Additive assemblies/types; no existing-consumer breakage.
- **Risk = Low** (after mitigations). The only non-trivial unknowns are encoding/null fidelity
  (covered by the golden fixture + tests before code) and the fixture-generation env (a Phase-3
  logistics item, not a design risk). Bounded-memory handled by streaming.

**GATE 1 (proposed):** impact map written; **Risk = Low, Impact = Low**; backward-compat plan stated;
the encoding/null/streaming watch-items have concrete mitigations; the fixture-env item is flagged for
Phase 3. → proceed to Phase 2.
