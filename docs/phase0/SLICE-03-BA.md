# Phase 0.1 — BA / Functional Document
## Slice 3: Pipeline + Normaliser (structure + deterministic intelligence)

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan · **Depends on:** Slice 1 (✅), Slice 2 (✅)

---

### WHY
Parsers (Slice 4) yield a `RawDocument`: sections with text but **no §ids, no hierarchy, no
keywords, no key numbers**. This slice turns that into a fully-normalised `Document` — the unit
retrieval and `.udf` writing actually consume. Crucially it also fills **deterministic
intelligence** (key numbers + keywords) with **no LLM**, which is what makes DocNest's 0-token
retrieval layers work at all (Charter KPI: ~70% of queries at 0 LLM tokens; *Cost*, *Privacy*).

### WHAT — exact functional behaviour
1. **`SectionNormaliser`** — `RawDocument → Document`:
   - Assign hierarchical **§ids** (`§1`, `§1.1`, `§1.1.1`, …) using *compact depth* (a skipped
     level, e.g. H1→H3, still nests by one — `§1.1`, not `§1.0.1`).
   - Link **parent_id / children**; compute **token_count** (`words × 1.3`, truncated to int).
   - **Normalise table widths**: pad short rows / truncate long rows to the header count;
     zero-header tables are left untouched.
2. **`KeyNumberExtractor` (deterministic)** — extract labelled figures from section text
   (money / percent / duration / ratio / count) with a bound label, dropping noise (ordered-list
   markers, bare years 1900–2099, identifiers like `AZ-204` / `ISO 27001`). Populate
   `Document.KeyNumbers` (no-op if already populated; cap 64).
3. **`KeywordExtractor` (deterministic)** — per section, up to `k=8` salient keywords (title terms
   first, then frequency × a small length bonus, minus stopwords). Populate `Section.Keywords`
   (per-section no-op if already set).
4. **`DocNestPipeline`** — compose: (optional parse via injected `IParser`) → normalise →
   deterministic enrich → `Document`. The LLM-enrichment and embedding stages are pluggable and
   **deferred** (Slices 6); the pipeline runs fully without them.

**Before vs after**
- *Before:* a `RawDocument` (or a parser) with unstructured sections.
- *After:* `pipeline.Process(raw)` → a `Document` with §ids, hierarchy, token counts, normalised
  tables, key numbers, and section keywords — ready to write to `.udf` (Slice 2) and (later) retrieve.

**Acceptance criteria**
- AC1: §id assignment matches Python `SectionNormaliser` exactly (incl. compact depth on skipped levels).
- AC2: parent/child links and `token_count` match Python.
- AC3: table rows are padded/truncated to header width; zero-header tables unchanged.
- AC4: key-number extraction matches Python `extract_key_numbers` on a shared case table
  (units, year filter, identifier/acronym/list-marker filters, label binding, dedup).
- AC5: keyword extraction matches Python `extract_keywords` (stopwords, title priority, k limit).
- AC6: enrichment is idempotent — no-op when `KeyNumbers`/`Keywords` already populated.
- AC7: `Document → .udf → Document` (Slice 2) still round-trips after normalisation.

### Non-goals (Slice 3)
- No LLM intelligence (summaries, insights, per-section summaries) — Slice 6.
- No embeddings/quantization — Slice 6.
- No parsers — Slice 4 (tests use a fake `IParser` or build `RawDocument` directly).
- No retrieval/query — Slice 5.

### HOW — scenarios
- *Normalise:* a `RawDocument` with H1/H2/H3 headings → §ids `§1`, `§1.1`, `§1.1.1`; an H1→H3 jump → `§1.1`.
- *Deterministic intelligence:* a section "Revenue was **$142M**, up 23%." → key numbers
  `{Revenue:$142M (USD)}`, `{… 23%}`; keywords include `revenue`.
- *Idempotent:* a `Document` that already has key numbers / keywords is returned unchanged.
- *Edge:* empty section text; a heading-less document; merged-width tables; unicode headings.

### Traceability
Serves **Cost** + **Privacy** (0-token, fully-local deterministic intelligence) and **Reliability**
(structure before content). Upholds the §id contract that the `.udf` and retrieval depend on.
