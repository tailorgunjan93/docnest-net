# SLICE-11 — QA / User

"Working" = on hard PDFs the engine retrieves the section that actually answers the question (via the
cross-encoder) and the narrator gives a complete answer, while structured-doc quality is preserved and
an offline run with no reranker model still works.

## Scenarios (positive)
- **Reranker enabled** — model provisioned; header reads `… + rerank (cross-encoder/ms-marco-MiniLM-L-6-v2)`.
  A question whose answer section ranked low under RRF (e.g. gpt3 "architecture vs GPT-2") is reranked to
  the top and answered (was a refusal → now 8/10).
- **Enumeration question** — "what are the training corpora", "list ALL parameter sizes" → skips the
  Layer-0 key-number / Layer-1 snippet, synthesises over reranked context, instructed to list every item.
- **Explanatory question** — "what does the BIS say about central bank policy" → LLM over reranked
  context instead of a wrong extractive snippet.
- **Structured docs unchanged** — simple key-number / table questions still answer at Layer 0/1 (no regression).

## Edge / negative
- **Reranker model absent** (offline/CI) → retrieval is dense+RRF only; eval header notes no reranker; no crash.
- **Reranker construction fails** → caught in the eval, continues without rerank.
- **Layer-3 refusal** → escalates to the Layer-4 broad fallback (top-8 sections) rather than returning the refusal.
- **Reasoning model burns tokens** → 1500-token budget leaves room for the answer after chain-of-thought.
- **Simple key-number question** → still answered at Layer 0 (gate only fires on enumeration/explanation markers).

## Regression view
- Full xUnit suite green every cycle. New: 2 reranker `[SkippableFact]`s + 3 engine regression tests.
- Eval re-run (Cerebras narrator + qwen2.5 judge): PDFs **5.1→7.1 / 47%→73%**, Phase-1 **7.6/83%**,
  overall **~7.4/80%** — recorded in `eval/results/`.

## Done means
AC1–AC4 hold, demonstrated by the new tests + the eval re-run, full suite green, shipped API/`.udf`
unchanged — with owner sign-off. **No merge to `main`, no NuGet publish** (owner-triggered).
