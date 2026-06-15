# SLICE-11 — Impact / Risk + Design

## Impact map
| Area | Change | Backward-compat |
|------|--------|-----------------|
| `DocNest.Abstractions/IReranker.cs` (new) | Reranker Strategy interface. | Additive. |
| `DocNest.Embeddings/OnnxCrossEncoderReranker.cs` (new) | ONNX cross-encoder behind `IReranker`. | Additive; ONNX behind wrapper. |
| `DocNest.Embeddings/WordPieceTokenizer.cs` | `EncodePair` (pair + segment ids). | Additive method. |
| `DocNest.Embeddings/CrossEncoderModel.cs` (new) | Opt-in model provisioning. | Additive. |
| `DocNest.Retrieval/HybridRetriever.cs` | Optional `IReranker` ctor arg; rerank top-12. | Default null ⇒ today's RRF order. |
| `DocNest.Query/DocNestQueryEngine.cs` | Bigger L3 context; refusal→L4 fallback; 1500-tok; complex-gate; enum-hint. | `AnswerAsync`/`QueryResult` shape unchanged. |
| `eval/DocNest.Eval/Program.cs` | Opt-in reranker wiring; header reports it. | Harness; default unchanged. |
| tests | +2 reranker, +3 engine regression tests. | Additive. |

**No change** to: public API, `UDF_VERSION` / `.udf` schema, the RRF math, the CLI. **Risk = Low–Medium**
(query behaviour: more LLM escalation on complex questions; a second opt-in model). Mitigations: reranker
behind `IReranker` with graceful degrade; engine changes behind the unchanged `AnswerAsync`; regression-first
full suite; eval gate.

## Design
- **Reranker (ADR-0013):** Strategy `IReranker`; `HybridRetriever` re-scores the top `RerankPool=12` RRF
  candidates and returns top-k by CE score. Recall stays with BM25+dense+graph; precision from the CE.
- **Refusal fallback:** `IsRefusalOrEmpty` checks the answer's opening for refusal markers; a Layer-3
  refusal/empty escalates to a Layer-4 broad fallback over the top-8 retrieved sections.
- **Complex-question gate:** `IsComplexQuestion` (markers: `all`, `list`, `every`, `what are`, `compare`,
  `how does/do`, `say about`, `explain`, `describe`, `discuss`, `differ`, `vs`) → skip Layer-0 key-number
  and Layer-1 extractive; route to the reranked LLM path.
- **Enumeration hint:** `EnumerationHint` appends "list EVERY matching item" to the L3/L4 prompt for
  `all`/`list`/`every`/`what are` questions.

## ADR
ADR-0013 records the cross-encoder reranker. The engine answer-quality fixes are tuning of the existing
5-layer design (no new public contract), recorded here + in the SLICE-11 docs.
