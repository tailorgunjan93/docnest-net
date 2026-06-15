# SLICE-10 — Dev / Technical

## Code read (what already exists)
- **`HybridRetriever`** ([src/DocNest.Retrieval/HybridRetriever.cs](../../src/DocNest.Retrieval/HybridRetriever.cs)) —
  ctor `HybridRetriever(string cacheDir = ".docnest_cache", IEmbedder? embedder = null)`. On
  `BuildIndexAsync` it embeds `"{Title} {Text}"` per section, L2-normalises, stores the float32 bytes in
  SQLite, and builds semantic-graph edges (cosine ≥ 0.68). `DenseRankAsync` embeds the query and
  cosine-ranks the stored vectors. **With `embedder == null` both are skipped** → BM25 + structural
  graph only (mirrors the Python `model is None` path). Proven today by
  `HybridRetrieverTests.Dense_signal_contributes_with_embedder` (FakeEmbedder) and `.Works_without_embedder`.
- **`OnnxEmbedder`** ([src/DocNest.Embeddings/OnnxEmbedder.cs](../../src/DocNest.Embeddings/OnnxEmbedder.cs)) —
  `IEmbedder` over ONNX Runtime + WordPiece; 384-dim, mean-pooled, L2-normalised. Ctor
  `(modelPath, vocabPath, maxLength = 256, batchSize = 32)`. `IDisposable` (ONNX session). Reusable and
  thread-tolerant per call (each `EmbedBatch` builds its own tensors); reuse **one** instance.
- **`MiniLmModel`** ([src/DocNest.Embeddings/MiniLmModel.cs](../../src/DocNest.Embeddings/MiniLmModel.cs)) —
  `IsPresent(cacheDir)`, `Paths(cacheDir)`, `EnsureDownloadedAsync(cacheDir)` (opt-in ~90 MB HF download
  of `onnx/model.onnx` + `vocab.txt`). **This is the provisioning wrapper; no new wrapper needed.**
- **Eval** ([eval/DocNest.Eval/Program.cs](../../eval/DocNest.Eval/Program.cs)) — per doc:
  `ParserFactory` → `DocNestPipeline.Process` → `using var retriever = new HybridRetriever(cache)`
  (line 88, **no embedder**) → `new DocNestQueryEngine(retriever, llm)`. The eval csproj does **not**
  reference `DocNest.Embeddings`.

## Root cause
Exactly one missing wire: the eval never passes an `IEmbedder`, so the retriever's dense + semantic
signals are off. The library is correct and tested; the harness is under-wired (faithful to ADR-0007's
graceful-degrade design, which the eval has been silently relying on).

## Why not the Quantizer / `Section.Embedding`
The retriever computes and caches its **own** full-precision vectors in SQLite
([HybridRetriever.cs:242 `ToBytes`](../../src/DocNest.Retrieval/HybridRetriever.cs)); it never reads
`Section.Embedding`. The `Quantizer` + `Section.Embedding` + `embeddings.bin` are the **`.udf`
cross-ecosystem storage contract** (ADR-0008), a different code path. Wiring the Quantizer would not
change eval retrieval, so it is deliberately excluded — faithful to ADR-0007's "dense via the injected
`IEmbedder`."

## Plan (file-by-file)
1. **`eval/DocNest.Eval/DocNest.Eval.csproj`** — add `<ProjectReference>` to `DocNest.Embeddings`.
2. **`eval/DocNest.Eval/Program.cs`** —
   - Resolve a model cache dir: `DOCNEST_MINILM_CACHE` env, else `<repo>/artifacts/minilm-cache`.
   - Build **one shared** `OnnxEmbedder?` at startup via a small local helper:
     - if `MiniLmModel.IsPresent(cache)` → build from `MiniLmModel.Paths(cache)`;
     - else if `DOCNEST_DOWNLOAD_MODEL=1` → `await EnsureDownloadedAsync(cache)` then build;
     - else → `null` (BM25-only) and print a one-line notice (how to enable).
     - any embedder-construction failure → `null` + warning (never crash the eval).
   - Pass `embedder` into **every** `new HybridRetriever(cacheDir, embedder)` (one shared instance,
     reused across all docs — the ONNX session is expensive to create).
   - Header: report retrieval mode — `hybrid (BM25 + dense {model})` vs `hybrid (BM25-only — no embedder)`.
   - `Dispose()` the shared embedder after both phases (it is `IDisposable`).
3. **`tests/DocNest.Retrieval.Tests/DocNest.Retrieval.Tests.csproj`** — add `<ProjectReference>` to
   `DocNest.Embeddings` (for the new integration test).
4. **`tests/DocNest.Retrieval.Tests/OnnxDenseRetrievalTests.cs`** (new) — `[SkippableFact]` gated on
   `MiniLmModel.IsPresent`: real `OnnxEmbedder` + `HybridRetriever` on a small doc where the query is
   semantically related but lexically disjoint from the right section; assert the dense+hybrid retriever
   ranks the correct section first, and that a BM25-only `HybridRetriever` on the same doc/query does
   **not** (demonstrating the dense path's value). Cache dir convention =
   `Path.Combine(AppContext.BaseDirectory, "minilm-cache")` (matches `OnnxEmbedderTests`).

No `src/` changes. No new wrapper (MiniLmModel + OnnxEmbedder + IEmbedder already are the wrappers).

## Backward compatibility
- Shipping library (`DocNest.*`) **untouched** — no API, `.udf`, `UDF_VERSION`, or RRF-ranking change.
- The eval is a standalone harness (`IsPackable=false`, not in `DocNest.sln`); changing it cannot
  regress the NuGet surface.
- Model provisioning stays opt-in: model absent → existing BM25-only behaviour, clean report (AC4).

## Complexity / NFR
- **Cold index build:** + one ONNX forward pass per section (mean-pool, L2) — O(N·tokens), batched (32),
  max-len 256, on `Task.Run`; cached in SQLite so paid **once** per doc. Semantic-edge build is O(N²·d)
  for N ≤ 1000 (already in the retriever, ADR-0007).
- **Warm query:** dense rank is a cosine over precomputed vectors (O(N·384)); the ~1 ms warm-query NFR is
  unaffected. Query embedding is one short forward pass.
- **Local-first:** model runs locally (no cloud); the only network is the opt-in one-time HF download.
- **Memory:** 384×4 B per section in SQLite; bounded.
