# ADR-0009 — 5-layer answer engine + LLM providers

- **Status:** Accepted (owner approved straight-through to GATE 5)
- **Date:** 2026-06-10
- **Context slice:** Slice 6b — LLM + answer engine
- **Builds on:** ADR-0001 (`ILLMProvider`), ADR-0007 (`IRetriever`)

## Context
Retrieval finds sections; the Python `reader.py` turns them into answers via a 5-layer stack that
answers most questions at 0 LLM tokens and escalates only when needed. We port that engine and add
`ILlmProvider` HTTP implementations. The owner chose **OpenAI-compatible + Anthropic** providers.

## Decision
1. **`DocNestQueryEngine`** in a new `DocNest.Query` assembly, depending only on Abstractions
   (`IRetriever`, `ILlmProvider`, domain). Layers: **0** deterministic (summary/insight keyword routing +
   `KeyNumberMatcher`), **1** extractive (top retrieved section's summary, else `Extractive.BestSentences`),
   **2/3/4** LLM (single section / top-3 synthesis / full doc) with the ported prompt templates. Token cost
   = word count of prompt + answer; confidence decreases with depth. `QueryResult` added to Abstractions.
2. **Escalation is availability-driven, not the Python absolute thresholds.** Our `IRetriever` returns RRF
   scores (not 0–1), so Layer 0/1 fire when they produce a usable answer; otherwise escalate to the LLM
   (when `allowLlm` and a provider exists). Thresholds are tunable constants, documented here.
3. **Two providers** (`ILlmProvider`): `OpenAiCompatibleLlmProvider` (POST `/chat/completions`,
   configurable base URL/model/key — covers OpenAI/Groq/Together/OpenRouter/local) and
   `AnthropicLlmProvider` (POST `/v1/messages`, `x-api-key` + `anthropic-version`). HTTP via BCL
   `System.Net.Http.Json` — **zero new package dependencies**; source-gen JSON DTOs.
4. **Tested without network:** deterministic layers + both providers via a stubbed `HttpMessageHandler`;
   a real-endpoint completion is `[SkippableFact]` gated on an API-key env var.

## Consequences
**Positive:** completes the RAG loop (ingest → `.udf` → retrieve → answer); 0-token majority preserved
(Cost KPI); providers behind the `ILlmProvider` wrapper; no new deps; deterministic layers fully covered.
**Negative / cost:** escalation thresholds are heuristic and may need tuning against a question set (the
accuracy KPI is measured later vs the Python reference); two providers = more request/parse code.
**Neutral:** prompt templates are direct ports; richer prompt engineering is out of scope.

## Alternatives considered
- *Port LangChain's 14 providers:* not idiomatic .NET; OpenAI-compatible + Anthropic cover the vast majority. Rejected.
- *An LLM SDK (Azure.AI.OpenAI / Anthropic.SDK):* extra deps; raw HTTP is small and dependency-free here. Rejected.
- *Reuse the Python 0.35/0.15 thresholds on RRF scores:* scale-mismatched; availability-driven is correct. Rejected.
