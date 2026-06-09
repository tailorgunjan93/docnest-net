# ADR-0007 — Hybrid retrieval (SQLite FTS5 + brute-force cosine + RRF + graph)

- **Status:** Accepted (owner approved straight-through to GATE 5)
- **Date:** 2026-06-09
- **Context slice:** Slice 5 — hybrid retrieval
- **Builds on:** ADR-0001 (`IEmbedder` contract)

## Context
The Python `HybridRetriever` fuses FTS5-BM25, USearch-HNSW dense ANN, and a section graph via RRF, with
a persistent SQLite cache. The dense path needs sentence embeddings (the .NET embedder is Slice 6). We
port the engine now, consuming the `IEmbedder` interface and degrading gracefully without one.

## Decision
1. **SQLite via `Microsoft.Data.Sqlite`** (MIT); its default native provider bundles **FTS5** (BM25 +
   `porter ascii` tokenizer). All SQL lives behind an internal `SqliteRetrievalStore` (wrapper rule).
2. **Dense search = exact brute-force cosine** (zero extra dependency). Section embeddings (L2-normalised)
   are stored in SQLite; the query vector dots against them and sorts. Section counts are small, so this
   meets the ~1 ms warm-query NFR; **HNSW/ANN is deferred** as a later speed optimization.
3. **Embeddings via the injected `IEmbedder`** (Slice 1). Slice 5 ships **no built-in embedder**; the
   real ONNX one arrives in Slice 6. With **no embedder**, dense ranking and semantic edges are skipped —
   retrieval runs on **BM25 + structural graph** (matching the Python `model is None` path).
4. **RRF + graph math is pure** (`RrfFusion`, no SQLite) with the Python constants: `RRF_K=60`,
   BM25 weight 2.0, Dense 1.5; 1-hop graph expansion child 0.15 / sibling 0.10 / semantic 0.12, **child→
   parent disabled** (parent inflation). Ported verbatim and unit-tested in isolation.
5. **Cache key = SHA-256** of `(doc_id, section count, per-section id + text length + 200-char prefix)`;
   any change → rebuild. Internal only (no cross-ecosystem contract).
6. **Scope = retrieve ranked sections from an in-memory `Document`.** The 5-layer LLM answer engine and
   `.udf`-backed retrieval are later slices.

## Consequences
**Positive:** real hybrid retrieval with one MIT dependency; the fusion math is dep-free and exactly
testable; dense degrades cleanly without an embedder; brute-force is simpler and accurate (no ANN recall
loss) at section scale.
**Negative / cost:** brute-force is O(N·d) per query and O(N²) for semantic edges — fine for N≤~1000,
revisit with HNSW for very large docs; the index cache is a local SQLite file (not portable like `.udf`).
**Neutral:** FTS5 BM25 scoring follows SQLite's defaults (k1=1.2, b=0.75), as the Python path does.

## Alternatives considered
- *USearch/HNSW .NET binding now:* native binding + setup risk for a speed win we don't yet need. Deferred.
- *`System.Data.SQLite`:* heavier, FTS5 packaging less clean than Microsoft.Data.Sqlite's bundle. Rejected.
- *Merge raw BM25 + cosine scores:* scale-incompatible; RRF (rank-based) is the proven ensemble. Rejected.
- *Build retrieval over `.udf` now:* couples retrieval to the reader path; in-memory `Document` is simpler. Deferred.
