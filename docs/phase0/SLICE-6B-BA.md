# Phase 0.1 — BA / Functional Document
## Slice 6b: LLM providers + 5-layer answer engine

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan · **Depends on:** Slices 1–6a (✅)

---

### WHY
Retrieval (Slice 5) finds the right *sections*; this slice turns them into *answers* — the generation
side of RAG. It ports the Python 5-layer query stack that answers **~70 % of questions with 0 LLM
tokens** (deterministic + extractive layers) and only escalates to an LLM when needed — the Charter
**Cost** KPI. It also adds the **`ILLMProvider`** implementation so the LLM layers actually work.

### WHAT — exact functional behaviour
1. **`DocNestQueryEngine`** — `Answer(Document doc, string question, …) → QueryResult`:
   - **Layer 0 (0 tokens):** pre-computed intelligence — summary keywords → `Document.Summary`;
     insight keywords → `Insights`; key-number lookup → match a `KeyNumber` whose core label tokens are
     all in the question (modifier/word-order tolerant; ambiguous → skip).
   - **Layer 1 (0 tokens):** hybrid retrieve (Slice 5 `IRetriever`); if the top section is confident,
     return its summary, else the **best question-relevant sentence(s)** extracted from its text.
   - **Layer 2 (~300 tokens):** single-section LLM answer (prose cap + budget-rendered tables).
   - **Layer 3 (~900 tokens):** multi-section synthesis (top 2–3 sections).
   - **Layer 4 (~4000 tokens):** full-document fallback.
   - `QueryResult` = `{ Answer, Citations, LayerUsed, TokensUsed, Confidence }` (word-count token estimate).
2. **`OpenAiCompatibleLlmProvider : ILlmProvider`** — HTTP `/chat/completions` (configurable base URL +
   model + API key) covering OpenAI, Groq, Together, OpenRouter, local OpenAI-compatible servers, etc.

**Before vs after**
- *Before:* DocNest retrieves sections but can't answer questions.
- *After:* `engine.Answer(doc, "what was Q3 revenue?")` → an answer + the layer used + token cost; most
  factual questions answered at 0 tokens.

**Acceptance criteria**
- AC1: Layer 0 returns the summary/insights/key-number answer for the matching question shapes, 0 tokens.
- AC2: Layer 1 returns a section summary or extracted best-sentence at 0 tokens when a confident hit exists.
- AC3: with no/low-confidence deterministic answer and an `ILlmProvider`, Layers 2–4 escalate and return
  an LLM answer with the right `LayerUsed` and a token estimate; with `allowLlm=false`, it stops after Layer 1.
- AC4: `OpenAiCompatibleLlmProvider.CompleteAsync` posts the correct request and parses the reply; auth/
  network failure → `IntelligenceException`.
- AC5: citations reference the section(s) used; confidence decreases with layer depth.

### Non-goals (this slice)
- No new retrieval (reuse Slice 5); no streaming token output; no agentic/multi-turn chat.
- Provider coverage limited to **OpenAI-compatible** HTTP this slice (Anthropic/others can follow).
- No prompt-tuning beyond porting the Python templates.

### HOW — scenarios
- *Key number:* "what is the uptime?" → Layer 0 returns "Uptime: 99.9% …" (0 tokens).
- *Summary:* "what is this about?" → Layer 0 returns the document summary.
- *Specific fact in one section:* retrieve → Layer 1 best-sentence (0 tokens) or, if needed, Layer 2 LLM.
- *Cross-section:* "compare X and Y" → Layer 3 synthesis.
- *Edge:* no LLM provider + no deterministic answer → returns an empty/low-confidence result (no crash).

### Traceability
Serves **Cost** (0-token majority), **Reliability** (escalation), **Privacy** (deterministic layers local;
LLM opt-in). Completes the RAG loop: ingest → `.udf` → retrieve → **answer**.
