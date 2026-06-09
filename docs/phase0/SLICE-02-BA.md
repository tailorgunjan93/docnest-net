# Phase 0.1 — BA / Functional Document
## Slice 2: `.udf` Read / Write (the cross-ecosystem contract)

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan · **Depends on:** Slice 1 (✅ merged)

---

### WHY
A `.udf` is DocNest's portable knowledge-base artifact and the **only contract that crosses
Python ↔ .NET**. Slice 1 gave us the in-memory domain; this slice makes that domain durable as a
`.udf` archive that the Python `docnest` can open, and lets .NET open Python-produced `.udf` files.
This is the headline KPI of the whole port (Charter: *Compatibility*). Until this slice is green,
"DocNest .NET" cannot interoperate with anything.

### WHAT — exact functional behaviour
1. **Write** a `Document` to a `.udf` ZIP containing the same entries Python writes:
   - `manifest.json` — version + embedding config + flattened `DocMeta` + `producer`.
   - `catalogue.json` — doc header + flattened `DocMeta` + `key_numbers` + `section_index`
     (the lightweight per-section projection).
   - `content.json` — `{doc_id, sections:{<§id>:{title,level,text,tables,images}}}`.
   - `embeddings.bin` — present only when embeddings exist (deferred; Slice 6 fills them). Slice 2
     writes a valid `.udf` **without** embeddings (the manifest reports `embedding_dims: 0`).
2. **Read** a `.udf` back: validate `udf_version == "1.0"`, parse the three JSON entries into typed
   objects, expose them (a `UdfPackage`/`UdfReader` surface) and reconstruct a `Document`.
3. **Round-trip**: a `Document` → `.udf` → `Document` preserves all section structure, tables,
   images, key numbers, and metadata.
4. **Cross-ecosystem**: a `.udf` written by .NET opens in Python `docnest`; a `.udf` written by
   Python opens in .NET — both produce equivalent catalogues/content.

**Before vs after**
- *Before:* domain records exist but cannot be persisted or shared.
- *After:* `UdfWriter.Write(document, path)` and `UdfReader.Load(path)` work; `.udf` files move
  freely between Python and .NET.

**Acceptance criteria**
- AC1: `.udf` written by .NET has exactly the entry set + JSON key set Python expects (validated
  against a golden Python-produced `.udf`).
- AC2: .NET reads a Python-produced golden `.udf` and yields the correct catalogue/content/manifest.
- AC3: Document → `.udf` → Document round-trip is loss-free for structure/tables/images/key-numbers/meta.
- AC4: `udf_version` mismatch is rejected with `UdfReadException`; missing `manifest.json` is rejected.
- AC5: ZIP compression mirrors Python (DEFLATE for JSON, light/stored for binary) — interop must not
  depend on compression level, but the archive must be a standard ZIP both runtimes read.
- AC6: JSON is compact (no indentation) and non-ASCII is preserved (e.g. `§` round-trips correctly).

### Non-goals (Slice 2)
- No embedding generation (Slice 6) — `embeddings.bin` write/read plumbing is defined but the vectors
  come later; Slice 2 handles the *absent* and *present-but-supplied* cases only.
- No retrieval / query engine (Slice 5).
- No parsers (Slice 4) — tests build `Document`s in-memory or load fixtures.
- No library (multi-doc) archive.

### HOW — scenarios
- *Author:* build a `Document`, `UdfWriter.WriteAsync(doc, "report.udf")` → a shareable file.
- *Interop out:* that `report.udf` opens in Python: `UDFIndex.load("report.udf")` succeeds.
- *Interop in:* a Python-built `sample.udf` (committed fixture) loads in .NET and exposes the same data.
- *Edge:* empty document (no sections); document with tables/images; non-ASCII headings; a `.udf`
  with `embeddings.bin` present (supplied bytes) and one without.

### Traceability
Directly serves the **Compatibility** KPI; upholds **Privacy** (local file I/O, no network) and
**Cost/Speed** (compact JSON, lazy content) from the Charter.
