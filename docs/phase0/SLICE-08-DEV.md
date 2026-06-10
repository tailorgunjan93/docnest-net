# SLICE-08 — Dev / Technical

## Code read (reference vs port)
- **Python `reader.py`** — `query()` escalates through 5 layers gated by absolute confidence:
  - `_L1_SCORE_THRESHOLD = 0.35` — return Layer-1 extractive only if `top_score ≥ 0.35`.
  - `_L2_SCORE_THRESHOLD = 0.15` — do Layer-2 single-section LLM only if `top_score ≥ 0.15`; else Layer 3/4.
  - `top_score` comes from `reader._hybrid_search`: **`0.6·BM25 + 0.4·semantic`** (BM25 raw/unbounded;
    semantic = cosine or Jaccard, 0–1). This is a *separate, weighted-sum confidence scorer*, distinct
    from the production RRF retriever.
- **.NET `DocNestQueryEngine.AnswerAsync`** ([src/DocNest.Query/DocNestQueryEngine.cs](../../src/DocNest.Query/DocNestQueryEngine.cs)) —
  takes `hits[0]` from the RRF `HybridRetriever` and returns the Layer-1 extractive **with no score gate**.
  `RetrievalHit.Score` is **RRF**: `Σ weight/(RrfK + rank + 1)` with `RrfK=60, Bm25=2.0, Dense=1.5`
  → top score ≈ `2.0/61 + 1.5/61 ≈ 0.057`. RRF score is a *rank-fusion* value, **not** an absolute
  confidence comparable to Python's weighted sum.

## Root causes
- **D-A** — the `0.35 / 0.15` escalation gates were never ported into the engine.
- **D-B (scale)** — even if ported, `0.35 / 0.15` cannot be applied to RRF scores (~0.05): every
  question would fall below threshold and escalate, destroying the 0-token property. The escalation
  signal must be an absolute confidence, which RRF is not.
- **D-C (key numbers)** — Python `extract_key_numbers` ([D:\Learning\docnest\docnest\key_numbers.py])
  gates extraction (`_IDENTIFIER_PREFIX`, `_acronym_prefixed`, bare-year skip, non-empty bound label);
  the matcher (`_match_key_number`) is faithfully ported as `KeyNumberMatcher`. The misfire therefore
  lives in the **extraction**/binding (Core `KeyNumberExtractor`) or a subtle gate gap, to be pinned
  with a failing test against the four academic PDFs.

## Design options for the escalation-confidence signal
1. **Recalibrate one RRF cutoff** — cheapest; but RRF isn't an absolute confidence, so a fixed cut is
   fragile across docs/section counts. Rejected as primary.
2. **Port `reader._hybrid_search`** — compute a normalized `0.6·BM25 + 0.4·semantic` confidence for the
   top candidate and gate on `0.35 / 0.15`. Most faithful; needs BM25 + cosine for the top hit.
3. **(Recommended) Confidence-for-escalation only** — keep RRF for *ranking*; add a small, wrapped
   normalized lexical+semantic confidence for the **top candidate only**, used solely for the Layer-1/2
   escalation decision. Faithful to Python's gating, minimal blast radius, RRF ranking untouched.

## Backward compatibility
- `AnswerAsync` signature, `QueryResult` shape, `.udf`/`UDF_VERSION`, and all public APIs unchanged.
- New scoring lives behind existing interfaces (wrapper rule). Deterministic-only path (no provider)
  preserved: low confidence + no provider still returns the `-1`/empty result.

## Complexity / NFR
- Added work is O(1) per query over the top candidate (a single normalized score); no change to the
  ~1 ms warm-query budget for the retrieval step. Memory unchanged.
