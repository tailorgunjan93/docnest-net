# Phase 0.3 — QA / User Document
## Slice 3: Pipeline + Normaliser

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### What "working" means
Given a `RawDocument`, the pipeline yields a `Document` whose §ids, hierarchy, token counts, table
widths, key numbers, and keywords match what the Python engine produces for the same input — so the
two engines stay interchangeable and the 0-token retrieval layers have data to work with.

### Test plan (test-first — written in Phase 3, failing first)

**Unit — SectionNormaliser**
- U1 (basic §ids): H1,H2,H2,H1 → `§1`, `§1.1`, `§1.2`, `§2`.
- U2 (nesting): H1,H2,H3 → `§1`, `§1.1`, `§1.1.1`; parent/child links correct.
- U3 (compact depth on skipped level): H1,H3 → `§1`, `§1.1` (not `§1.0.1`).
- U4 (multiple roots + deep): a realistic tree; assert every id, parent_id, children.
- U5 (token_count): `int(words*1.3)` for several texts incl. empty (`0`) and multi-space.
- U6 (table width): rows shorter than headers are padded with `""`; longer are truncated;
  zero-header tables pass through unchanged.
- U7 (level clamp): levels <1 or >6 clamp to 1..6 like Python (`max(1,min(6,level))`).

**Unit — KeyNumberExtractor (parity case table)**
- U8: money `$142M`→USD; percent `23%`→%; duration `142ms`; ratio `8x`; count `1200`.
- U9 (filters): bare year `2024` (count, no unit) dropped; identifier `AZ-204` / `v2` dropped;
  acronym-prefixed `ISO 27001` dropped; ordered-list marker `1.` at line start dropped.
- U10 (label binding): `**Uptime:** 99.9%` → label "Uptime"; `Revenue: $30M` → "Revenue";
  trailing-noun fallback when no `Label:`.
- U11 (dedup + cap): duplicate (label,value) collapsed; ≤ 64 numbers.
- U12 (`parse_number`): `$1.2 billion`→1.2e9; `18,400`→18400; `5k`→5000.

**Unit — KeywordExtractor (parity)**
- U13: stopwords removed; title terms appear first; frequency orders the rest; ≤ k(=8); tokens
  match `[a-z0-9][a-z0-9\-]{2,}`.
- U14 (idempotence): `enrich` is a per-section no-op when keywords already set; `enrich_key_numbers`
  is a no-op when key numbers already set.

**Integration — pipeline end-to-end**
- I1: `RawDocument` (multi-section, with a table and figures) → `Process` → `Document` with §ids,
  hierarchy, token counts, key numbers, keywords — all asserted together.
- I2 (with fake parser): a fake `IParser` returning a `RawDocument` → `ProcessAsync(path)` → same result.
- I3 (round-trip with Slice 2): normalised `Document` → `.udf` → `Document` is loss-free
  (regression tie-in to Slice 2).

### Parity strategy
The extractor case tables (U8–U13) are derived from `key_numbers.py` / `keywords.py` behaviour and,
where a Python env is available, can be **cross-checked** by running the Python functions on the same
inputs (guarded, like Slice-2 E2). The committed expectations are the regression baseline regardless.

### Edge / negative cases
- Empty document (no sections) → empty `Document`, no errors.
- Section with empty text → `token_count 0`, no key numbers, empty keywords.
- Heading levels out of range (0, 9) → clamped 1..6.
- Unicode headings / figures (`€1,2 Mio`, non-breaking spaces) — documented behaviour; at minimum no crash.
- Table with ragged rows and a zero-header table in the same section.

### What constitutes a regression (regression-suite seeds)
- Any §id differing from Python for the same heading sequence — **highest severity** (breaks the
  cross-engine contract and the `.udf`).
- token_count formula drift.
- A key number that Python would drop (year/identifier) leaking through, or a real metric dropped.
- Keyword ordering/stopword drift.
- Enrichment overwriting already-populated key numbers/keywords (idempotence break).
