# SLICE-11 — Dev / Technical

## Code read & root causes
- **`HybridRetriever`** returns RRF-fused order (BM25 2.0 + dense 1.5 + graph). RRF is rank-based — it
  cannot model true query↔passage relevance, so the answer section frequently ranked below lexical
  distractors on PDF prose. **Fix:** a cross-encoder precision pass over the top candidates.
- **`DocNestQueryEngine`** (5-layer):
  - **Layer 0** (`Precomputed`) matched a key-number even for list questions → `corpora: 2` on
    "what are the training corpora".
  - **Layer 1** (`Extractive.BestSentences`) returned a single snippet for explanatory questions
    ("what does the BIS say about…") → wrong/0.
  - **Layers 2–4** called the LLM with `maxTokens` default **512** → gpt-oss reasoning consumed the
    budget → empty answers; Layer-3 context was only 3×600 chars; Layer-4 (full-doc) was unreachable
    (`hits.Count < 2`) so a Layer-3 refusal was returned as the final answer.

## What was built (file-by-file)
- **`DocNest.Abstractions/IReranker.cs`** (new) — `ScoreAsync(query, passages) → IReadOnlyList<double>`.
- **`DocNest.Embeddings/OnnxCrossEncoderReranker.cs`** (new) — ONNX ms-marco-MiniLM behind `IReranker`;
  pair-tokenizes `[CLS] q [SEP] p [SEP]` with `token_type_ids`, reads the single relevance logit. Batched, max-len 320.
- **`DocNest.Embeddings/WordPieceTokenizer.cs`** — added `EncodePair` (segment-id pair encoding).
- **`DocNest.Embeddings/CrossEncoderModel.cs`** (new) — opt-in ~91 MB download + paths (mirrors `MiniLmModel`).
- **`DocNest.Retrieval/HybridRetriever.cs`** — optional `IReranker` ctor arg; after RRF+graph, re-score the
  top `RerankPool=12` and return top-k by CE score (`RerankAsync`). Degrades to RRF when absent.
- **`DocNest.Query/DocNestQueryEngine.cs`** — Layer-3 context 600→1400 ×5 sections; `IsRefusalOrEmpty`
  → Layer-3 refusal escalates to a Layer-4 broad fallback over the top-8 retrieved sections; answer
  `maxTokens` 512→1500; `IsComplexQuestion` gate (enumeration/explanation skip Layer-0/1);
  `EnumerationHint` (list questions instruct the narrator to emit every item).
- **`eval/DocNest.Eval/Program.cs`** — opt-in reranker provisioning (`DOCNEST_MSMARCO_CACHE`,
  `DOCNEST_DOWNLOAD_MODEL=1`); injected into every `HybridRetriever`; header reports `+ rerank`.
- **Tests** — `CrossEncoderRerankerTests` (2 `[SkippableFact]`); `EscalationGateTests`
  (`Layer3_refusal_escalates_to_layer4_fallback`, `Enumeration_question_skips_layer0_keynumber…`,
  `Simple_keynumber_question_still_answers_at_layer0`).

## Backward compatibility
- Public API / `QueryResult` / `.udf` / `UDF_VERSION` unchanged. New reranker is behind `IReranker`;
  absent ⇒ today's RRF behaviour. The confidence gate uses section content, not the hit score, so
  replacing the hit score with the CE score does not perturb escalation.

## Complexity / NFR
- + one CE forward pass per candidate (≤12) per query — batched, max-len 320, opt-in. Warm-query budget
  unaffected for the no-reranker path. Second ~91 MB opt-in model (not committed).
