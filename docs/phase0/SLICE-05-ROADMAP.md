# Phase 0.4 — Roadmap: Slice 5 (Hybrid retrieval engine)

> Consolidates the Slice-5 BA/Dev/QA. One MIT dependency (`Microsoft.Data.Sqlite`, FTS5 bundled).
> Dense quality depends on the Slice-6 embedder; this slice uses the `IEmbedder` interface and a fake
> embedder in tests. Program: ingestion (Slices 1–4c) ✅ → **retrieval (5)** → embeddings+LLM (6) → CLI+NuGet (7).

## Ordered steps (what GATE 0 approves entering Phase 1 for)
1. **Phase 1 (Impact & risk):** new `DocNest.Retrieval` assembly + `Microsoft.Data.Sqlite`. Watch-items:
   FTS5 availability in the bundled provider; FTS5 MATCH safety; RRF/graph parity; cache correctness;
   the no-embedder degraded path. Risk Med (SQLite/FTS5 integration) → Low by isolating
   `SqliteRetrievalStore` + pure `RrfFusion` and testing each first.
2. **Phase 2 (Design + ADR-0007):** resolve Q1–Q4 (brute-force cosine; `Microsoft.Data.Sqlite`;
   `IRetriever`/`RetrievalHit` shape; in-memory `Document` scope); the schema; the fusion + graph math;
   file-by-file plan.
3. **Phase 3 (Test-first):** U1–U6 (pure fusion/tokenizer) + I1–I6 (SQLite + fake embedder), failing first.
4. **Phase 4 (Implement):** `RetrievalTokenizer`, `RrfFusion`, `SqliteRetrievalStore`, `HybridRetriever`,
   `IRetriever`/`RetrievalHit`; wire BM25 + dense + RRF + graph + cache.
5. **Phase 5 (Verify):** full suite (Slices 1–5) green; fusion parity; cache hit/rebuild correct.
6. **Phase 6:** defects → regression + unit tests then fix (FTS5 quirks, cache edge cases).
7. **Phase 7:** branch `slice/05-retrieval` → green → merge `main`; CHANGELOG; tag `v0.0.7-retrieval`.

**Risk/impact expectation:** Med → Low — one mature MIT dependency; the pure fusion math is dep-free and
unit-tested; SQLite/FTS5 isolated behind a store wrapper; the dense path degrades cleanly without an embedder.

## Decisions needed from owner at GATE 0
- Approve the four Phase 0 documents, **and** Q1 (dense = brute-force cosine now vs an ANN library).
- Q4: keep retrieval on the in-memory `Document` this slice (recommend), with `.udf`-backed retrieval
  and the 5-layer LLM answer engine as later slices?
