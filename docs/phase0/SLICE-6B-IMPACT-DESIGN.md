# Phase 1 + Phase 2 — Impact/Risk & Design
## Slice 6b: LLM providers + answer engine

**Status:** Phases 1–2 (owner approved straight-through to GATE 5) · **Owner:** Gunjan
**ADR:** [ADR-0009](../adr/0009-answer-engine-and-llm-providers.md) · **Providers:** OpenAI-compatible **+ Anthropic**.

---

## Phase 1 — Impact & Risk
**Blast radius:** new `DocNest.Query` assembly + `QueryResult` in Abstractions. No Slice-1..6a changes;
no `.udf` change. **Zero new package dependencies** (HTTP via BCL `System.Net.Http.Json`).

**Watch-items / mitigations:**
1. **Escalation vs RRF scores** — availability-driven (Layer 0/1 produce something usable → use it; else
   escalate), tunable constants; not the Python 0.35/0.15 absolute thresholds. Tested with fakes.
2. **Never call the LLM when Layers 0–1 answered** (token waste) — the fake provider records calls; asserted.
3. **Provider request/parse correctness + error mapping** — stubbed `HttpMessageHandler` unit tests (no network).
4. **Layer-0 ambiguity** (two values for one key-number label) → must skip (no guess) — unit-tested.
**Risk Low/Med → Low:** deterministic layers + both providers fully tested without network; real-LLM gated.

## Phase 2 — Design
**DSA:** Layer 0/1 O(sections·tokens); LLM layers bounded by prompt caps. Memory O(document).

**SOLID / patterns:** `DocNestQueryEngine` depends on `IRetriever` + `ILlmProvider` (DIP); providers are
`ILlmProvider` strategies; HTTP/JSON behind each provider. Deterministic helpers (`KeyNumberMatcher`,
`Extractive`) are pure/static (SRP, testable).

**Code plan (signatures):**
- `DocNest.Abstractions/QueryResult.cs` — `record QueryResult(string Answer, IReadOnlyList<string> Citations, int LayerUsed, int TokensUsed, double Confidence)`.
- `DocNest.Query/DocNestQueryEngine.cs` — ctor `(IRetriever retriever, ILlmProvider? llm = null)`;
  `Task<QueryResult> AnswerAsync(Document doc, string question, bool allowLlm = true, CancellationToken ct = default)`.
  Layer 0 `Precomputed` (summary/insight keyword routing + `KeyNumberMatcher`); Layer 1 retrieve → summary
  / `Extractive.BestSentences`; Layers 2/3/4 LLM (ported prompts; `TokensUsed = WordCount(prompt)+WordCount(answer)`).
- `DocNest.Query/KeyNumberMatcher.cs` (internal static) — `KeyNumber? Match(string q, IReadOnlyList<KeyNumber>)` (ports `_match_key_number`/`_kn_tokens`).
- `DocNest.Query/Extractive.cs` (internal static) — `string BestSentences(string text, string question, int n = 2)`.
- `DocNest.Query/OpenAiCompatibleLlmProvider.cs` — `ILlmProvider`; POST `{baseUrl}/chat/completions`.
- `DocNest.Query/AnthropicLlmProvider.cs` — `ILlmProvider`; POST `{baseUrl}/v1/messages` (`x-api-key`, `anthropic-version`).
- `DocNest.Query/LlmJson.cs` — source-gen DTOs/context (OpenAI + Anthropic), `WhenWritingNull`.

**Resolved open questions:** Q1 → OpenAI-compatible **+ Anthropic**; Q2 → availability-driven escalation
(tunable); Q3 → both providers in `DocNest.Query` (split later if providers grow).
