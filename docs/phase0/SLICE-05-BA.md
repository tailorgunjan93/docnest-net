# Phase 0.1 — BA / Functional Document
## Slice 5: Hybrid retrieval engine

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan · **Depends on:** Slices 1–4c (✅)

---

### WHY
Ingestion (Slices 1–4c) produces a `Document`. To make it useful, DocNest must **retrieve** the
right sections for a query — the heart of RAG. The Python `HybridRetriever` fuses three signals
(BM25, dense cosine, section graph) with RRF and caches the index to disk for ~1 ms warm queries.
This slice ports it: `retrieve(doc, query, k) → ranked sections`. It is the query side that the
later 5-layer answer engine (LLM) sits on top of.

### WHAT — exact functional behaviour
1. **`HybridRetriever`** with a persistent SQLite cache:
   - **Index build (once per doc, cached):** FTS5 rows (one per section, `porter ascii` tokenizer),
     section **embeddings** (via an injected `IEmbedder`), and a **section graph** (structural
     parent/child/sibling + semantic cosine edges ≥ 0.68).
   - **Cache invalidation:** a SHA-256 fingerprint of `(doc_id, section count, per-section id + text
     length + text prefix)`; any structural change → rebuild. `IsCached`, `Invalidate`, `BuildIndex`.
   - **Query:** **BM25** rank (FTS5), **dense cosine** rank (over stored embeddings), **RRF fusion**
     (`score += weight / (60 + rank)`, BM25 ×2.0, Dense ×1.5), then **1-hop graph expansion**
     (child α=0.15, sibling α=0.10, semantic α=0.12; **child→parent disabled**), then top-k.
2. **Graceful degradation:** with **no `IEmbedder`** configured, dense + semantic edges are skipped
   and retrieval runs on **BM25 + structural graph** (exactly as the Python path does when the
   embedding model is unavailable).

**Before vs after**
- *Before:* a `Document` can be ingested and written to `.udf` but not queried.
- *After:* `retriever.Retrieve(doc, "what was Q3 revenue?", k: 8)` → the most relevant sections, ranked.

**Acceptance criteria**
- AC1: BM25 ranking via FTS5 returns sections containing the query terms, best first.
- AC2: dense cosine ranking (with a supplied `IEmbedder`) ranks by semantic similarity.
- AC3: RRF fusion uses the Python weights/constants (RRF_K=60, BM25=2.0, Dense=1.5); the fused order
  matches the Python algorithm on the same inputs.
- AC4: 1-hop graph expansion boosts children/siblings/semantically-similar sections by the documented
  α; parent boosting from children is **not** applied.
- AC5: a second query on an unchanged doc hits the cache (no rebuild); a changed doc rebuilds.
- AC6: with no embedder, retrieval still returns sensible BM25+graph results (no crash).

### Non-goals (this slice)
- No **LLM answer engine** (the 5-layer Layer-0…4 query stack) — a later slice (needs the LLM, Slice 6).
- No **ANN/HNSW** index — dense search is **exact brute-force cosine** over stored vectors (small N;
  meets the ~1 ms NFR). HNSW is a later speed optimization if needed.
- The **real embedder is Slice 6** (ONNX MiniLM). This slice consumes the `IEmbedder` interface;
  tests use a deterministic fake embedder.

### HOW — scenarios
- *Keyword query:* "carbon budget" → FTS5 BM25 finds the section with those terms.
- *Semantic query (with embedder):* "emissions allowance" → dense cosine finds the "carbon budget"
  section even without exact term overlap; RRF unions both.
- *Graph:* a query hitting a parent also surfaces the specific child section (child α boost).
- *Cache:* first query builds the index; the second is warm (no rebuild).
- *Edge:* empty doc / no matches → empty result; query with only stop-words → falls back to all tokens.

### Traceability
Serves **Speed** (~1 ms warm), **Reliability** (hybrid recall), and **Cost** (BM25+graph need no LLM).
Dense quality arrives with the Slice-6 embedder; the interface seam is in place now.
