# SLICE-10 — QA / User

"Working" = the eval retrieves with the **real dense embeddings** (not BM25-only), so the right section
reaches the LLM and answers improve — while an offline/CI run with no model still works on the BM25 path,
and nothing about the shipped library changes.

## Scenarios (positive)
- **Dense enabled** — model present in the cache (or `DOCNEST_DOWNLOAD_MODEL=1`), run `dotnet run` in
  `eval/DocNest.Eval`. Header reads *"retrieval: hybrid (BM25 + dense sentence-transformers/all-MiniLM-L6-v2)"*;
  each doc's index is built with section embeddings; the run completes and writes `eval/results/report.md`.
- **Semantically-related, lexically-disjoint question** — a question whose wording barely overlaps the
  answer section's surface tokens (e.g. paraphrase / synonym) now retrieves the correct section because
  the dense cosine + semantic-graph edges surface it where BM25 alone would not. (Headline fix; BA AC2.)
- **No Phase-1 regression** — generated-file (xlsx/docx/html/md) questions that already scored well stay
  ≥ their current level; structural/number questions are unaffected by adding the dense signal.

## Edge / negative
- **Model absent (default offline/CI)** — `MiniLmModel.IsPresent` false and `DOCNEST_DOWNLOAD_MODEL`
  unset → `embedder = null`; eval runs **exactly as today** (BM25 + structural graph), header reads
  *"hybrid (BM25-only — no embedder; set DOCNEST_DOWNLOAD_MODEL=1 …)"*, zero embedding work. No crash.
- **Embedder construction fails** (corrupt model, missing native ONNX runtime) → caught, `embedder =
  null`, a warning is printed, the eval continues on BM25-only rather than aborting the whole run.
- **Download fails / no network with `DOCNEST_DOWNLOAD_MODEL=1`** → surfaced as a clear error from
  `EnsureDownloadedAsync`; the run can be re-tried offline once provisioned, or proceeds BM25-only.
- **Empty document / no sections** → retriever returns `[]` as today (embedder path is never reached).
- **Special characters in query** → unchanged; FTS escaping already covered by
  `HybridRetrieverTests.Special_characters_in_query_do_not_throw`.

## Regression view
- Full xUnit suite green every cycle (regression-first; the suite only grows).
- **Existing tests stay green** — the FakeEmbedder dense test, the no-embedder test, RRF/graph tests,
  and the Quantizer tests are untouched (the `.udf`/Quantizer path is not modified).
- **New test** — `OnnxDenseRetrievalTests` (`[SkippableFact]`, real model): dense+hybrid ranks the right
  section first on a lexically-disjoint query; BM25-only does not. Skips with a clear reason when the
  model is absent (CI parity with `OnnxEmbedderTests`); provision once to run it.
- **Eval re-run** (gpt-oss-120b via Groq) — record before/after headline + per-PDF table in
  `eval/results/` and confirm AC3 (Phase-2 hit-rate ↑ vs 47%, overall ↑ vs 6.7/10, Phase-1 not regressed).

## Done means
BA AC1–AC4 hold, demonstrated by the new test + the eval re-run, full suite green, shipped library
untouched — with owner sign-off. **No merge to `main`, no NuGet publish** (owner-triggered).
