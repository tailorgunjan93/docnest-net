# Phase 0.2 — Dev / Technical Document
## Slice 5: Hybrid retrieval engine

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### 1. Python reference — read end-to-end (files traced)
`retrieval.py` `HybridRetriever`, fully:
- `_SCHEMA` (SQLite: `doc_hashes`, FTS5 `fts_sections` `tokenize='porter ascii'`, `embeddings` BLOB,
  `graph_edges` + index); `_doc_cache_key` (SHA-256 fingerprint); `_tokenise` / `_query_tokens`
  (`_STOP_WORDS`); `retrieve` (BM25 + dense → RRF → graph → top-k); `_build` / `_build_hnsw` /
  `_build_graph`; `_fts5_rank` (MATCH OR-join, ORDER BY rank, fallback on `OperationalError`);
  `_hnsw_rank` (dense cosine); `_graph_expand` (child/sibling/semantic, parent disabled); `stats`.
- Constants: `RRF_K=60`, `BM25_WEIGHT=2.0`, `DENSE_WEIGHT=1.5`, `GRAPH_CHILD/SIBLING/SEMANTIC_ALPHA =
  0.15/0.10/0.12`, `SEMANTIC_EDGE_THRESHOLD=0.68`.

### 2. Dependency + dense-backend decisions
- **`Microsoft.Data.Sqlite`** (MIT) for SQLite. Its default native provider
  (`SQLitePCLRaw.bundle_e_sqlite3`) **includes FTS5** — so `porter`/`ascii` tokenizer + BM25 `rank`
  work out of the box. Behind a small `SqliteRetrievalStore` wrapper (no raw SQL in the public API).
- **Dense ANN → exact brute-force cosine** (zero extra dep). Embeddings (L2-normalised) are stored in
  the `embeddings` table; at query time the query vector dots against all section vectors and sorts.
  Section counts are small (tens–hundreds) → ≪ 1 ms; HNSW/USearch deferred as a speed optimization.
- **Embeddings via the Slice-1 `IEmbedder`** (injected, optional). Slice 5 ships with **no built-in
  embedder**; the real ONNX one lands in Slice 6. Tests use a deterministic fake `IEmbedder`.

### 3. Design surface to add
- `DocNest.Retrieval` (new assembly) refs `Abstractions` + `Core` (+ `Microsoft.Data.Sqlite`).
  - `IRetriever` (new abstraction): `IReadOnlyList<RetrievalHit> Retrieve(Document doc, string query, int k = 8, …)`.
    `RetrievalHit` = `record(Section Section, double Score)`.
  - `HybridRetriever : IRetriever` — ctor `(string cacheDir, IEmbedder? embedder = null, int embedDim = 384)`.
  - `SqliteRetrievalStore` (internal) — opens/creates the DB, runs the schema, exposes typed
    upsert/query methods (FTS5 insert + MATCH, embeddings upsert/scan, edges insert/select, hash CRUD).
  - `RrfFusion` (internal static) — the fusion + graph-expand math (pure, unit-testable without SQLite).
  - `RetrievalTokenizer` (internal static) — `_tokenise` / `_query_tokens` + `_STOP_WORDS`.
- The retriever takes a `Document` (already normalised). It does **not** read `.udf` (that's the reader
  path, later); retrieval operates on the in-memory `Document` + the SQLite cache.

### 4. Mapping notes / gotchas
- **FTS5 MATCH safety:** OR-join keyword tokens; on a malformed MATCH (`SqliteException`), fall back to
  a punctuation-stripped query, else empty — mirrors the Python `OperationalError` handling. Escape/strip
  FTS5 special chars (`" * : ( )` etc.).
- **BM25 ordering:** FTS5 `ORDER BY rank` ASC (rank is negative BM25 → most-negative = best). Use the
  `rank` column (auto in FTS5) or `bm25(fts_sections)`.
- **Cache key:** port `_doc_cache_key` (SHA-256 over doc_id + section count (big-endian) + per-section
  id + text length + 200-char prefix). Internal only (no cross-ecosystem contract) but ported for fidelity.
- **Semantic edges:** O(N²) cosine over stored embeddings, threshold 0.68; skip when N>1000 (structural-only).
  Needs embeddings → only built when an embedder is present.
- **Concurrency/disposal:** the SQLite connection is owned by the retriever and `IDisposable`; WAL +
  `synchronous=NORMAL` pragmas as in Python.
- **Brute-force cosine vs FTS5 `rank`:** keep them as two separate ranked index lists fed into RRF
  (don't merge raw scores — RRF is rank-based).

### 5. Backward-compat surface
- Additive: new `DocNest.Retrieval` assembly + one MIT dependency. No Slice-1..4c changes; no `.udf`
  schema change (retrieval cache is a separate local DB, not part of `.udf`).

### 6. Open questions (resolve GATE 0 / Phase 2)
- Q1: dense backend — **brute-force cosine** now (recommend) vs an ANN library (USearch .NET binding).
- Q2: SQLite package — `Microsoft.Data.Sqlite` (recommend; FTS5 bundled) vs `System.Data.SQLite`.
- Q3: `IRetriever` shape — return `RetrievalHit` records; expose `BuildIndex`/`IsCached`/`Invalidate`/`Stats`?
- Q4: should Slice 5 also start the **reader/query** path (open a `.udf` and retrieve), or stay on the
  in-memory `Document` and leave `.udf`-backed retrieval to a later slice? (Recommend in-memory `Document` now.)
