# Phase 1 + Phase 2 — Impact/Risk & Design
## Slice 5: Hybrid retrieval engine

**Status:** Phases 1–2 (owner approved straight-through to GATE 5) · **Owner:** Gunjan
**ADR:** [ADR-0007](../adr/0007-hybrid-retrieval.md) · **Decisions:** brute-force cosine; `Microsoft.Data.Sqlite`; in-memory `Document`.

---

## Phase 1 — Impact & Risk
**Blast radius:** new `DocNest.Retrieval` assembly + `Microsoft.Data.Sqlite`. No Slice-1..4c changes;
no `.udf` change (the retrieval cache is a separate local SQLite DB).

**Watch-items / mitigations:**
1. **FTS5 availability** — confirmed bundled in `SQLitePCLRaw.bundle_e_sqlite3` (the default native
   provider Microsoft.Data.Sqlite uses). Mitigation: a smoke test creates an fts5 table + BM25 query.
2. **FTS5 MATCH safety** — special chars throw. Mitigation: OR-join keyword tokens; catch
   `SqliteException` → punctuation-stripped retry → empty (mirrors Python `OperationalError`).
3. **RRF / graph parity** — mitigation: pure `RrfFusion` (no SQLite) unit-tested against exact expected scores.
4. **Cache correctness** — SHA-256 fingerprint; mitigation: hit/rebuild integration tests.
5. **No-embedder degraded path** — mitigation: an explicit test (BM25 + structural graph only, no crash).
**Risk Med → Low** by isolating `SqliteRetrievalStore` + pure `RrfFusion`/`RetrievalTokenizer`.

## Phase 2 — Design
**DSA:** FTS5 BM25 = C-level, ~sub-ms; dense = brute-force cosine O(N·d) (N small → ≪1ms); RRF O(pool);
graph expand O(edges); semantic edge build O(N²·d) capped at N≤1000. Memory O(N·d) for the vectors.

**SOLID / patterns:** `HybridRetriever : IRetriever` (Strategy); `IEmbedder` injected (DIP); SQLite behind
`SqliteRetrievalStore` (wrapper rule — no raw SQL in the public API); pure `RrfFusion`/`RetrievalTokenizer`
(SRP, testable in isolation). RRF fusion = rank-based ensemble.

**Schema (ported):** `doc_hashes(doc_id PK, hash, n_secs, built_at)`; `fts_sections` fts5(`doc_id`
UNINDEXED, `sec_idx` UNINDEXED, `sec_id` UNINDEXED, `title`, `text`, tokenize='porter ascii');
`embeddings(doc_id, sec_idx, vec BLOB, PK(doc_id,sec_idx))`; `graph_edges(doc_id, from_idx, to_idx,
edge_type, weight)` + index. PRAGMA WAL + synchronous=NORMAL.

**Code plan (signatures):**
- `DocNest.Abstractions/IRetriever.cs` — `Task<IReadOnlyList<RetrievalHit>> RetrieveAsync(Document doc,
  string query, int k = 8, CancellationToken ct = default)`; `record RetrievalHit(Section Section, double Score)`.
- `DocNest.Retrieval/RetrievalTokenizer.cs` (internal) — `Tokenize`, `QueryTokens`, `StopWords`.
- `DocNest.Retrieval/RrfFusion.cs` (internal static) — `Fuse(bm25, dense)`; `GraphExpand(scores, edges)`
  (child 0.15 / sibling 0.10 / semantic 0.12; parent → no boost); constants `RrfK=60`, `Bm25=2.0`, `Dense=1.5`.
- `DocNest.Retrieval/SqliteRetrievalStore.cs` (internal, `IDisposable`) — schema + typed hash/FTS/embedding/edge ops + BM25 MATCH (with fallback).
- `DocNest.Retrieval/HybridRetriever.cs` — `IRetriever` + `BuildIndexAsync`, `IsCached`, `Invalidate`,
  `Stats`; ctor `(string cacheDir, IEmbedder? embedder = null)`. Embeddings L2-normalised on store; query normalised; cosine = dot.

**Resolved open questions:** Q1 → brute-force cosine; Q2 → `Microsoft.Data.Sqlite`; Q3 → `RetrievalHit`
record + `BuildIndex`/`IsCached`/`Invalidate`/`Stats` on the concrete type; Q4 → in-memory `Document` only.
