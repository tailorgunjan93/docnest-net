# ADR-0013 — Cross-encoder reranker (ms-marco-MiniLM-L-6-v2)

- **Status:** Accepted (owner approved — "build the reranker")
- **Date:** 2026-06-12
- **Context slice:** Slice 11 — cross-encoder reranking
- **Builds on:** ADR-0007 (hybrid retrieval / RRF), ADR-0008 (`OnnxEmbedder` + `MiniLmModel` + WordPiece)

## Context
The multi-format eval shows structured docs at 7.6–9.1/10 but **PDFs at ~5.5/10**, dominated by
**retrieval misses**: the section that actually contains the answer is often *not* ranked first (or not in
the top-k), so the narrator answers from the wrong context or refuses. RRF fusion (BM25 2.0 + dense 1.5)
is a coarse rank ensemble; it cannot judge true query↔passage relevance. The Python reference closes this
with a **cross-encoder reranker** (`cross-encoder/ms-marco-MiniLM-L-6-v2`, the `CE` term in CLAUDE.md) —
a precision re-scoring step over the top candidates. This ports it to .NET.

## Decision
1. **New `IReranker` wrapper** (`DocNest.Abstractions`): `ScoreAsync(query, passages) → IReadOnlyList<double>`
   (one relevance score per passage; higher = more relevant) + `ModelName`. Strategy pattern — swap the
   reranker without touching the retriever.
2. **`OnnxCrossEncoderReranker`** in `DocNest.Embeddings`, behind `IReranker`, using the existing
   `Microsoft.ML.OnnxRuntime` + the **same WordPiece vocab** as the bi-encoder (identical bert-base
   `vocab.txt`). The tokenizer gains a **pair encoder** (`[CLS] query [SEP] passage [SEP]` with
   `token_type_ids` 0/1); the model emits a single logit per pair = the relevance score. Batched, max-len 320.
3. **Model handling = file paths + opt-in download** (mirrors ADR-0008): a `CrossEncoderModel` static with
   `IsPresent`/`Paths`/`EnsureDownloadedAsync` for the ~91 MB ONNX + vocab. Not committed; tests are
   `[SkippableFact]` gated on model presence.
4. **Integration in `HybridRetriever`** (opt-in `IReranker?` ctor arg): after RRF + graph, take the top
   `RerankPool` (12) candidates and **re-score with the CE, return top-k by CE score**. With no reranker,
   behaviour is exactly today's (RRF order). The reranker only *reorders* the hybrid pool — recall still
   comes from BM25+dense+graph, precision from the CE.
5. **Confidence gate unaffected:** `DocNestQueryEngine` gates escalation on `Confidence.Of(question,
   section)` (query-term recall over the section), *not* the hit score — so replacing the hit score with
   the CE score does not perturb the Layer-1/2 escalation logic.

## Consequences
**Positive:** the right section reaches the narrator far more often → fewer refusals / wrong-section
answers on PDFs; faithful to the Python design; ONNX stays behind the wrapper; degrades cleanly to RRF
when the model is absent.
**Negative / cost:** a second ~91 MB opt-in model; + one CE forward pass per candidate (≤12) per query —
CPU cost on the ingest/query path (mitigated: only the top-12 pool, batched, max-len 320). Real-inference
tests only run when the model is provisioned (else skip).
**Neutral:** max-len 320 truncates very long sections for *scoring* only (not for the answer context).

## Alternatives considered
- *Tune RRF weights instead:* rank fusion can't model true relevance; it already underperforms on PDFs. Rejected.
- *Bi-encoder rerank (reuse MiniLM):* that's the dense signal already fused; a cross-encoder is strictly
  more discriminative for precision. Rejected.
- *Rerank all sections:* O(N) CE passes — wasteful; the hybrid pool already has high recall. Rejected (top-12).
- *Bundle the model:* ~91 MB per consumer; opt-in download is leaner (consistent with ADR-0008). Rejected.
