# Phase 0.3 — QA / User Document
## Slice 2: `.udf` Read / Write

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### What "working" means
A `.udf` written in .NET is a real, standard ZIP that Python `docnest` opens and reads correctly,
and .NET opens Python-produced `.udf` files and exposes the same data. "Working" = **archive
fidelity + JSON key fidelity + loss-free round-trip + true cross-runtime interop**.

### How a user exercises it
1. `var doc = ...; await new UdfWriter().WriteAsync(doc, "report.udf");`
2. `var pkg = await UdfReader.LoadAsync("report.udf");` → catalogue, content, manifest, `Document`.
3. In Python: `UDFIndex.load("report.udf")` succeeds on the .NET-produced file.

### Test plan (test-first — written in Phase 3, failing first)

**Unit — DTOs & serialisation**
- U1: Each wire DTO serialises with the exact Python key set (per the Dev shapes), compact, non-ASCII raw.
- U2: `content.json` sections serialise as a **map keyed by §id** (not an array).
- U3: `_sanitise_source` parity: basename for paths (both `/` and `\`), verbatim for URLs (`://`).

**Unit — ZipStorageBackend**
- U4: Write→list→read round-trips arbitrary entries (text + binary) byte-for-byte.
- U5: `ReadEntryAsync` of a missing entry → `UdfReadException`; corrupt/zip-missing → `UdfReadException`.
- U6: Entry written by .NET is readable by `System.IO.Compression` and is a valid ZIP (magic bytes `PK`).

**Integration — UdfWriter / UdfReader**
- I1: `Document → .udf → Document` round-trip is loss-free (sections, §ids, tables, images,
  key_numbers, meta, summary, insights).
- I2 (version gate): reading a `.udf` whose `manifest.udf_version != "1.0"` → `UdfReadException`;
  missing `manifest.json` → `UdfReadException`.
- I3 (entry/key parity): a .NET-written `.udf`’s `manifest`/`catalogue`/`content` key sets exactly
  match those in the **golden Python fixture** (the Slice-1 deferred I1/I2 land here).
- I4 (embeddings plumbing): write a `.udf` with supplied embedding bytes → `embeddings.bin` present,
  `embedding_dims`/`quantization` set; without → entry absent, `embedding_dims:0`. Read both back.

**End-to-end — cross-ecosystem (the headline)**
- E1 (read Python → .NET): load a committed Python-produced `sample.udf`; assert catalogue/content
  fields match expected. *Always runs (static fixture).*
- E2 (read .NET → Python): write a `.udf` in .NET, then a guarded test shells out to Python
  `docnest`/`UDFIndex.load` if available, asserting it opens and reports the right `section_count`.
  *Skipped with a clear message when no Python env — never a silent pass.*

### Data variety / fixtures
- **Golden `sample.udf`** produced by Python `docnest` from a tiny multi-section doc (with a table,
  a non-ASCII heading, key numbers) — committed under `tests/fixtures/`. The reference for I3/E1.
- Synthetic `Document`s: empty (no sections); nested hierarchy (§1 → §1.1 → §1.1.1); a section with
  two tables and an image; a section carrying a supplied float16 `embedding`.

### Edge / negative cases
- Empty document → valid `.udf` with `section_count:0`, empty `content.sections`.
- Non-ASCII (`§`, accented headings) survives both directions byte-correct.
- A `.udf` missing `content.json` or `catalogue.json` → `UdfReadException` (clear message).
- Tables whose row width ≠ header width are written verbatim (normalisation is Slice 3, not here).
- Very large `content.json` streamed, not fully buffered twice (bounded-memory NFR watch-item).

### What constitutes a regression (regression-suite seeds)
- Any change to an entry name, JSON key, or key casing in a wire DTO — **highest severity** (breaks interop).
- Version gate weakened (accepts wrong `udf_version`).
- Round-trip dropping tables/images/key_numbers/meta.
- Non-ASCII corrupted (`§` → `§` leaking as literal, or mojibake).
- `embeddings.bin` stride/byte-order drift vs Python.

E1 (read Python golden) + I3 (key parity) are the permanent interop tripwires protecting the
cross-ecosystem `.udf` contract from here on.
