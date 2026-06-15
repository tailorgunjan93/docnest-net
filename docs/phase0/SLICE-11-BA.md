# SLICE-11 — BA / Functional: Retrieval precision (cross-encoder reranker) + answer quality

> Follows the Slice-8 RCA + the Slice-9 (LLM judge) / Slice-10 (dense) follow-ups. With dense retrieval
> and the LLM-as-judge in place, the eval exposed two remaining failure clusters: **retrieval misses on
> PDFs** (the right section not ranked into the narrator's context) and **answer-quality misfires**
> (Layer-0/1 short-circuits, refusals, truncated reasoning). This slice closes both.

## Why
With dense + LLM-judge + Cerebras narrator, the multi-format eval sat at **~6.7–6.9 overall**, PDFs
**~5.5/10 / 53% hit**. Decomposition of the failures:
- **Retrieval misses (dominant on PDFs):** RRF fuses BM25 + dense by *rank*, which cannot judge true
  query↔passage relevance. The answer-bearing section often ranked below lexical distractors, so the
  narrator answered from the wrong context or refused.
- **Answer-quality misfires:** (a) Layer-0 key-number short-circuits on list questions ("corpora: 2"),
  (b) Layer-1 extractive returned wrong snippets for explanatory questions, (c) the 512-token answer
  budget let reasoning models (gpt-oss) emit empty answers, (d) no fallback when Layer-3 refused.

## What (scope)
1. **Cross-encoder reranker** (`ms-marco-MiniLM-L-6-v2`, ADR-0013): re-score the top hybrid candidates by
   true relevance so the right section reaches the narrator.
2. **Answer-engine quality fixes** (`DocNestQueryEngine`): bigger Layer-3 context + refusal→Layer-4
   broad fallback + 1500-token answer budget; **complex-question gate** (enumeration/explanation
   questions skip Layer-0/1 and use the reranked LLM path); **enumeration hint** (list questions instruct
   the narrator to emit every item).

## Acceptance criteria
- **AC1 — reranker works:** the cross-encoder scores a relevant passage above distractors and ranks the
  answer-bearing passage first (real-model tests).
- **AC2 — PDFs materially improve:** Phase-2 PDF avg ↑ from ~5.5 and hit-rate ↑ from ~53% (eval).
- **AC3 — no structured-doc regression:** Phase-1 (generated) avg stays ≥ 7.5.
- **AC4 — no Layer-0/1 misfires on list/explanation questions; graceful degrade** when the reranker
  model is absent (RRF order); full xUnit suite green; public API / `.udf` / `UDF_VERSION` unchanged.

## Outcome (met)
PDFs **5.1→7.1/10, hit 47%→73%**; Phase-1 **7.6/83%** (no regression); **overall ~7.4/10, ~80% hit**.
AC1–AC4 met. The residual gap to the Python reference's honest 8.5 is exhaustive-list completeness +
judge strictness (diminishing returns) — see [SLICE-11-ROADMAP.md](SLICE-11-ROADMAP.md) RCA.

## Non-goals
- Reaching 9.55 (a qwen-judge artifact; honest gpt-oss ceiling ≈ 8.5).
- A stronger narrator / answer-coverage verification (future).
- HNSW/ANN; changing the `.udf` contract or the public API.
