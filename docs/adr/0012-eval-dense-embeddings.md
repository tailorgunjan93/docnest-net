# ADR-0012 — Activate the eval's dense retrieval path (inject the ONNX embedder)

- **Status:** Accepted (owner approved straight-through to GATE 5)
- **Date:** 2026-06-10
- **Context slice:** Slice 10 — dense embeddings in the eval retrieval path
- **Builds on:** ADR-0007 (hybrid retrieval consumes an injected `IEmbedder`), ADR-0008 (`OnnxEmbedder` +
  `MiniLmModel` model handling = file paths + opt-in download)

## Context
The multi-format accuracy eval (`eval/DocNest.Eval`) scores 6.7/10 vs the Python reference's 8.5/10. The
Slice 8 RCA identified the dominant remaining cause: the eval builds `HybridRetriever` **without an
`IEmbedder`**, so per ADR-0007 it degrades to **BM25 + structural graph** — the dense cosine and
semantic-graph signals are off. On PDF prose where the question and answer section share little surface
vocabulary, the right section misses the top-k and Layer-3 LLM answers come back empty. The real
`OnnxEmbedder` (MiniLM, 384-dim) and its `MiniLmModel` provisioning already exist (Slice 6a) but were
never wired into the eval.

## Decision
1. **Inject one shared `OnnxEmbedder` into the eval's `HybridRetriever`s** at the composition root
   (`Program.cs`). This is pure dependency injection — the retriever's dense logic is unchanged; we
   change only *which* `IEmbedder` is supplied (was `null`). The eval csproj gains a `ProjectReference`
   to `DocNest.Embeddings`.
2. **Provisioning is opt-in (ADR-0008 preserved).** Model resolved from `DOCNEST_MINILM_CACHE` else
   `<repo>/artifacts/minilm-cache`. If present → use it; else if `DOCNEST_DOWNLOAD_MODEL=1` →
   `EnsureDownloadedAsync`; else → `embedder = null` and the eval runs the existing BM25-only path with a
   one-line notice. Any embedder-construction failure degrades to BM25-only (never crashes the run).
3. **One instance, reused across all docs/queries.** The ONNX `InferenceSession` is the expensive object;
   create it once, dispose after the run.
4. **The Quantizer / `Section.Embedding` / `.udf` `embeddings.bin` stay out of the retrieval path.** The
   `HybridRetriever` caches its **own** full-precision (float32) vectors in SQLite and never reads
   `Section.Embedding`; the Quantizer defines the cross-ecosystem `.udf` byte layout (ADR-0008), a
   separate path. Wiring it would not change eval retrieval, so it is deliberately excluded here.
5. **Scope is the harness + one regression test.** No `src/` (shipped library), public API, `.udf`,
   `UDF_VERSION`, RRF-ranking, or NuGet change. A `[SkippableFact]` (`OnnxDenseRetrievalTests`) pins the
   real-model dense-vs-BM25 behaviour, gated on `MiniLmModel.IsPresent` (CI parity with `OnnxEmbedderTests`).

## Consequences
**Positive:** the eval finally measures the *real* hybrid retriever (BM25 + dense + semantic graph),
closing the dominant Slice-8 gap; the change is confined to a non-shipped harness behind existing
wrappers, so the NuGet surface and `.udf` contract are untouched; the new test guards the dense path.
**Negative / cost:** the eval now needs the ~90 MB model provisioned to exercise dense (one-time,
opt-in); a cold eval run is slower (per-section ONNX inference, cached in SQLite); the harness pulls in
the ONNX native runtime. Offline/CI runs without the model fall back to BM25-only.
**Neutral:** the residual gap to Python's 8.5 attributable to the eval's stricter local judge is
unaffected — that is the separate Slice-9 (LLM-as-judge) follow-up.

## Alternatives considered
- *Wire embeddings into `DocNestPipeline` / `.udf` via the Quantizer instead:* does not affect eval
  retrieval (the retriever ignores `Section.Embedding`), broadens blast radius into the shipped library
  and `.udf` write path. Rejected for this slice (tracked follow-up).
- *Also wire the embedder into the CLI `query` command:* same gap, but a separate concern that widens
  scope and risk. Deferred (tracked follow-up).
- *Bundle/commit the model so the eval always runs dense:* ~90 MB in the repo; ADR-0008 already chose
  opt-in download. Rejected.
- *Recompute embeddings per doc with a fresh embedder:* re-creates the costly ONNX session 10×. Rejected
  in favour of one shared instance.
