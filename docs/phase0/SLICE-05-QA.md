# Phase 0.3 — QA / User Document
## Slice 5: Hybrid retrieval engine

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### What "working" means
Given a `Document` and a query, the retriever returns the most relevant sections, ranked, fast, and
deterministically — matching the Python `HybridRetriever`'s fusion/graph behaviour on the same inputs.
Dense quality is exercised with a fake deterministic embedder (the real ONNX one arrives in Slice 6).

### Test plan (test-first — written in Phase 3, failing first)

**Unit — pure math (no SQLite): `RrfFusion`**
- U1: RRF combines two rank lists with weights 2.0/1.5 and K=60 → exact expected fused scores.
- U2: graph expansion boosts `child`/`sibling`/`semantic` targets by α×weight×seedScore; `parent`
  edges produce **no** boost (child→parent disabled).
- U3: a section in both BM25 and dense lists scores higher than one in only one list.

**Unit — `RetrievalTokenizer`**
- U4: `_tokenise` lowercases, strips non-`[a-z0-9-]`, drops length-1 tokens.
- U5: `_query_tokens` keyword set drops stop-words and length≤2; all-stop-word query → fallback to all tokens.

**Integration — `HybridRetriever` (real SQLite, fake embedder)**
- I1 (BM25): a 4-section doc; query with terms unique to section §3 → §3 ranks first.
- I2 (dense, fake embedder): a fake embedder mapping known texts to fixed vectors → a semantically
  "near" section ranks via the dense signal even without term overlap; RRF unions both.
- I3 (graph): a query hitting a parent surfaces the specific child (child α boost) above an unrelated section.
- I4 (cache): first `Retrieve` builds the index; `IsCached` true; a second `Retrieve` does not rebuild
  (assert via `Stats.built_at` unchanged / a build counter); changing the doc → `IsCached` false → rebuild.
- I5 (no embedder): a retriever with no `IEmbedder` returns BM25+structural-graph results, no crash, no
  embeddings/semantic edges written.
- I6 (FTS5 safety): a query with special characters (`"`, `:`, `*`) does not throw — falls back gracefully.

### Fixtures
In-memory `Document`s built in tests (sections with ids/titles/text, a small hierarchy). The fake
embedder is a deterministic function (e.g. hashed bag-of-words → unit vector) so dense ranks are
reproducible. SQLite uses a temp cache dir per test, deleted in teardown.

### Edge / negative cases
- Empty document (no sections) → empty result.
- `k` larger than section count → returns all sections.
- A query matching nothing → empty (BM25) but graph/dense may still surface neighbours; never crash.
- Unicode query/section text (`§`, accents) tokenizes and matches.
- Re-running on a large-ish doc (e.g. 200 sections) stays well under the latency budget.

### What constitutes a regression (regression-suite seeds)
- RRF weights/constant drift (RRF_K, BM25/Dense weights) — changes ranking.
- Graph expansion boosting a parent from its children (the explicitly-disabled, inflation-causing case).
- Cache not invalidating on a changed document (stale results) or rebuilding when unchanged (slow).
- FTS5 MATCH throwing on special characters instead of falling back.
- A crash when no embedder is configured (the degraded path must work).
