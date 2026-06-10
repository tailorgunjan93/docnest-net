# Phase 0.3 — QA / User Document
## Slice 6b: LLM providers + answer engine

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### What "working" means
A user asks a question of a `Document` and gets an answer, the layer that produced it, and the token
cost — with most factual questions answered at **0 tokens** (Layers 0–1), matching the Python engine's
escalation behaviour. The OpenAI-compatible provider works against any compatible endpoint.

### Test plan (test-first — written in Phase 3, failing first)

**Unit — deterministic helpers (no LLM, always run)**
- U1 (`KeyNumberMatcher`): "what is the uptime?" matches `{Uptime: 99.9%}`; ambiguous (two values for the
  same core label) → no match; modifier-tolerant ("average response time" matches "Response time").
- U2 (`Extractive.BestSentences`): returns the question-relevant sentence(s); no overlap → empty (no fabrication).
- U3 (summary/insight keyword routing): "summarise this" → summary; "key takeaways" → insights bullets.

**Unit/Integration — `DocNestQueryEngine` (fake `ILlmProvider`)**
- U4 (Layer 0): a key-number question → `LayerUsed=0`, `TokensUsed=0`, the precomputed answer; the fake LLM is **never called**.
- U5 (Layer 1): a question answered by a section summary/sentence → `LayerUsed=1`, `TokensUsed=0`, citation = that §id.
- U6 (Layer 2+): a question with no deterministic answer + a fake LLM → `LayerUsed≥2`, the LLM answer,
  `TokensUsed>0`, citations set; confidence decreases with depth.
- U7 (`allowLlm=false`): stops after Layer 1 (empty/low-confidence result), LLM not called.

**Unit — `OpenAiCompatibleLlmProvider` (stubbed `HttpMessageHandler`, no network)**
- U8: posts `{model, messages, temperature, max_tokens}` to `{baseUrl}/chat/completions` with the bearer key;
  parses `choices[0].message.content`.
- U9: a non-2xx response / network error → `IntelligenceException`.

**Gated — real endpoint (`[SkippableFact]`)**
- E1: with `DOCNEST_LLM_API_KEY` (+ base URL/model) set, a real completion returns non-empty text; skip-with-reason otherwise.

### Fixtures
In-memory `Document`s with Summary/Insights/KeyNumbers and section text. A `FakeLlmProvider` returning a
canned answer (and recording whether it was called). A `StubHandler : HttpMessageHandler` returning a
fixed JSON body for provider unit tests.

### Edge / negative cases
- No LLM provider + no deterministic answer → empty result, `LayerUsed = -1`/0, no crash.
- A question matching multiple key numbers with different values → ambiguous → falls through (no guess).
- Very long document → Layer 4 prompt truncated to the cap; no unbounded prompt.
- Provider returns malformed JSON → `IntelligenceException` (not an unhandled crash).
- Non-ASCII question/answer round-trips.

### What constitutes a regression (regression-suite seeds)
- Layer 0 fabricating or guessing an ambiguous key number (must skip).
- The LLM being called when Layer 0/1 already answered (token waste) — the fake records calls.
- Wrong `LayerUsed`/`TokensUsed`/citations.
- Provider request shape drift (model/messages/auth) or reply-parse failure not mapped to `IntelligenceException`.
