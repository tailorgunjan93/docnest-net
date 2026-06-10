# Phase 0.2 — Dev / Technical Document
## Slice 6b: LLM providers + answer engine

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### 1. Python reference — read end-to-end (files traced)
| Python | Logic | Port |
|---|---|---|
| `reader.py` `query` + Layers 0–4 | `get_precomputed` (summary/insights/key_numbers); `_hybrid_search`; `_best_sentences`; `_match_key_number`; `_call_llm_section/_multi/_full` (prompts + token = word counts); thresholds `_L1=0.35`, `_L2=0.15`; `_SUMMARY/_INSIGHT_KEYWORDS` | **`DocNestQueryEngine`** |
| `providers/llm.py` `ILLMProvider` + `get_llm_provider` | `complete(prompt, system, temperature, max_tokens)`; 14 LangChain backends | **`OpenAiCompatibleLlmProvider`** (HTTP) |

### 2. Design
- **`DocNestQueryEngine`** (new `DocNest.Query` assembly) — ctor `(IRetriever retriever, ILlmProvider? llm = null)`;
  `Task<QueryResult> AnswerAsync(Document doc, string question, bool allowLlm = true, CancellationToken ct = default)`.
  - **Layer 0** = `Precomputed(doc, q)`: keyword sets → `Document.Summary` / `Insights`; `KeyNumberMatcher.Match(q, doc.KeyNumbers)`
    (port `_match_key_number`: core label tokens all present; ambiguous values → null).
  - **Layer 1** = `await retriever.RetrieveAsync(doc, q)`; top hit → `Section.Summary` if set, else
    `Extractive.BestSentences(section.Text, q, n=2)` (port `_best_sentences`). 0 tokens.
  - **Layers 2–4** = LLM via `ILlmProvider` (single section / top-3 synthesis / full doc), ported prompts;
    `TokensUsed` = `WordCount(prompt) + WordCount(answer)`.
- **`QueryResult`** record `(string Answer, IReadOnlyList<string> Citations, int LayerUsed, int TokensUsed, double Confidence)` → Abstractions.
- **`OpenAiCompatibleLlmProvider : ILlmProvider`** (new, in `DocNest.Query` or its own assembly) —
  `HttpClient` POST `{baseUrl}/chat/completions` with `{model, messages:[{role:system},{role:user}], temperature, max_tokens}`;
  parse `choices[0].message.content`; non-2xx / network → `IntelligenceException`. `System.Text.Json` source-gen DTOs.

### 3. Threshold mapping (the one real nuance)
The Python reader uses its own 0–1 hybrid score with `_L1=0.35`/`_L2=0.15`. Our `IRetriever`
(HybridRetriever) returns **RRF** scores (not 0–1). **Decision (Q2):** drive escalation by *availability +
relative confidence*, not the Python absolute thresholds: Layer 1 fires when the top hit has a usable
summary/sentence; escalate to Layer 2+ when Layer 0/1 produce nothing usable (and `allowLlm`). Optionally
expose a normalized confidence (top RRF / Σ RRF). Documented in ADR-0009; thresholds are tunable constants.

### 4. Testing
- **Deterministic layers (0, 1)** — fully testable with a **fake `ILlmProvider`** (never called) and an
  in-memory `Document` (with Summary/Insights/KeyNumbers/Section text). Always run.
- **Layers 2–4** — a **fake `ILlmProvider`** returning a canned string verifies escalation + token/layer/citation wiring.
- **`OpenAiCompatibleLlmProvider`** — a **stubbed `HttpMessageHandler`** verifies request shape + reply
  parsing + error mapping (no network). A real-endpoint test is `[SkippableFact]` gated on an API-key env var.

### 5. Layout
```
src/DocNest.Query/DocNestQueryEngine.cs  + KeyNumberMatcher.cs + Extractive.cs
src/DocNest.Query/OpenAiCompatibleLlmProvider.cs (+ Json DTOs)
src/DocNest.Abstractions/QueryResult.cs
tests/DocNest.Query.Tests/…  (fakes + stubbed HttpMessageHandler; gated real-LLM test)
```

### 6. Backward-compat surface
- Additive: new `DocNest.Query` assembly + `QueryResult` in Abstractions. No Slice-1..6a changes; no `.udf` change.

### 7. Open questions (resolve GATE 0 / Phase 2)
- Q1: providers — **one OpenAI-compatible HTTP provider** this slice (covers many) vs also Anthropic? (Recommend OpenAI-compatible.)
- Q2: escalation thresholds vs RRF scores (above) — availability-driven + tunable constants (recommend).
- Q3: where does `OpenAiCompatibleLlmProvider` live — `DocNest.Query` vs a `DocNest.Llm` assembly? (Recommend `DocNest.Query` for now; split later if providers grow.)
