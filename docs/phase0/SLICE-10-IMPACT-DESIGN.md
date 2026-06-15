# SLICE-10 — Impact / Risk + Design (Phase 1–2)

## Impact map
| Area | Change | Backward-compat |
|------|--------|-----------------|
| `eval/DocNest.Eval/DocNest.Eval.csproj` (edit) | `<ProjectReference>` → `DocNest.Embeddings` (pulls ONNX runtime into the harness). | Harness only; `IsPackable=false`. |
| `eval/DocNest.Eval/Program.cs` (edit) | Build one shared `OnnxEmbedder?` (opt-in provisioning); pass it into every `HybridRetriever`; report retrieval mode; dispose at end. | Default (model absent) = today's BM25-only output. |
| `tests/DocNest.Retrieval.Tests/DocNest.Retrieval.Tests.csproj` (edit) | `<ProjectReference>` → `DocNest.Embeddings`. | Additive (test project). |
| `tests/DocNest.Retrieval.Tests/OnnxDenseRetrievalTests.cs` (new) | `[SkippableFact]` real-model dense-vs-BM25 retrieval test. | Additive; skips without the model. |

**No change** to: any `src/` shipped library, public API, `UDF_VERSION` / `.udf` schema, the engine /
retriever / embedder **code**, the RRF ranking, the CLI, or the NuGet package.

**Risk = Low. Impact = Low.** The only behaviour change is in a non-shipped harness, and it is opt-in
(needs the model present or `DOCNEST_DOWNLOAD_MODEL=1`); the default path is today's BM25-only output.
Worst case of a bug is a wrong eval number / a slower eval run, caught by the suite + the eval re-run.

Mitigations: shipped library untouched; embedder behind the existing `IEmbedder` wrapper + `MiniLmModel`
provisioning wrapper; construction failures degrade to BM25-only (never crash); regression-first full
suite; the eval gate (AC3).

## Design

### Pattern — Dependency Injection + graceful degradation (SOLID)
- The eval already depends on the `IRetriever`/`HybridRetriever` and `IEmbedder` abstractions
  (Dependency-Inversion). This slice **injects** a concrete `OnnxEmbedder` at the composition root
  (`Program.cs`) instead of `null`. No new abstraction is invented; `OnnxEmbedder` (wrapper for ONNX
  Runtime) and `MiniLmModel` (provisioning) already satisfy the wrapper rule.
- **Single-Responsibility / Open-Closed**: the retriever's dense logic is unchanged and stays closed for
  modification; we change only *which* strategy object is supplied.

### Embedder provisioning helper (local to `Program.cs`)
```
static OnnxEmbedder? TryBuildEmbedder(out string mode):
    cache = env DOCNEST_MINILM_CACHE ?? <repo>/artifacts/minilm-cache
    try:
        if not MiniLmModel.IsPresent(cache):
            if env DOCNEST_DOWNLOAD_MODEL == "1":
                (model, vocab) = await MiniLmModel.EnsureDownloadedAsync(cache)
            else:
                mode = "BM25-only — no embedder (set DOCNEST_DOWNLOAD_MODEL=1 to fetch MiniLM)"
                return null
        else:
            (model, vocab) = MiniLmModel.Paths(cache)
        e = new OnnxEmbedder(model, vocab)
        mode = $"BM25 + dense {e.ModelName}"
        return e
    catch (ex):
        mode = $"BM25-only — embedder unavailable ({ex.GetType().Name})"
        return null
```
- One instance, created once, reused for all 10 docs and all 88 queries (ONNX `InferenceSession`
  construction is the expensive step; embedding calls are cheap by comparison). Disposed after Phase 2.
- `cacheDir` default lives under `artifacts/` so the ~90 MB model is provisioned once and reused across
  runs (and is git-ignored).

### `Program.cs` edits (minimal)
- Near the existing `apiKey`/`llm` block: `using var embedder = TryBuildEmbedder(out var retrievalMode);`
- Header: add a line `_Retrieval: hybrid ({retrievalMode})_`.
- In `RunPhase`: `using var retriever = new HybridRetriever(Path.Combine(work, $"cache_{name}"), embedder);`
  (the only functional change to the loop). `RunPhase` already captures outer variables.

### Test design (`OnnxDenseRetrievalTests`)
- Build a 4-section doc where the answer section is a **paraphrase** of the query that shares **no**
  query content-tokens (e.g. query "how do mammals keep a steady internal body temperature" vs section
  "Thermoregulation: warm-blooded creatures … shivering, sweating, vasodilation … hypothalamus …"),
  plus a **lexical distractor** that contains most query tokens but is semantically unrelated (e.g. car
  engine), plus two neutral off-topic sections.
- Assert the RCA failure + fix directly: with a no-embedder `HybridRetriever` the correct section is
  **absent** from the top-k (BM25 never matches it — zero token overlap); with a real-`OnnxEmbedder`
  `HybridRetriever` the correct section **is present** in the top-k (dense surfaces it) and ranks above
  the off-topic neutrals. Both retrievers use isolated temp cache dirs (mirrors `HybridRetrieverTests.Run`).
- **Why presence, not rank #1:** RRF weights BM25 (2.0) > dense (1.5), so the lexical distractor (a
  strong BM25 hit) can still rank first; the dense path's measurable, robust contribution is bringing
  the right section *into* the top-k that BM25 alone omits.
- `[SkippableFact]` + `Skip.IfNot(MiniLmModel.IsPresent(cache), …)`; cache dir = `DOCNEST_MINILM_CACHE`
  env else `AppContext.BaseDirectory/minilm-cache`. Runs when provisioned, skips cleanly otherwise.
  Deterministic given the model (MiniLM inference is deterministic, per `OnnxEmbedderTests`).

## ADR?
**Yes — a light ADR-0012** records the decision to activate the dense path in the eval by injecting the
existing `OnnxEmbedder` (opt-in provisioning), and the explicit choice to keep the Quantizer / `.udf`
path out of retrieval. It is an *application* of ADR-0007 (dense via `IEmbedder`) + ADR-0008 (model =
file paths + opt-in download), not a new architectural direction, so it is short.

## Validation
- New `[SkippableFact]` proves dense-vs-BM25 retrieval offline-when-provisioned, no network in CI.
- Eval re-run (gpt-oss-120b) confirms AC3 on the real files; before/after recorded in `eval/results/`.
