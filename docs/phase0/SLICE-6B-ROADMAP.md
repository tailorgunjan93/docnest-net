# Phase 0.4 — Roadmap: Slice 6b (LLM providers + answer engine)

> Consolidates the Slice-6b BA/Dev/QA. Completes the RAG loop (ingest → `.udf` → retrieve → **answer**).
> The deterministic layers (0–1) are fully testable; the LLM layers use a fake provider; a real provider
> test gates on an API key. Program: … 6a (embeddings) ✅ → **6b (LLM + answer engine)** → 7 (CLI + NuGet).

## Ordered steps (what GATE 0 approves entering Phase 1 for)
1. **Phase 1 (Impact & risk):** new `DocNest.Query` assembly + `QueryResult` in Abstractions. Watch-items:
   escalation-threshold mapping vs RRF scores; provider request/parse correctness; never calling the LLM
   when Layers 0–1 answer. Risk Low/Med → Low (deterministic layers + provider tested without network).
2. **Phase 2 (Design + ADR-0009):** resolve Q1–Q3 (OpenAI-compatible provider; availability-driven
   escalation; assembly placement); the layer flow; provider HTTP contract; file-by-file plan.
3. **Phase 3 (Test-first):** U1–U9 (fakes + stubbed `HttpMessageHandler`) + gated E1. Failing first.
4. **Phase 4 (Implement):** `KeyNumberMatcher`, `Extractive`, `DocNestQueryEngine`,
   `OpenAiCompatibleLlmProvider`, `QueryResult`.
5. **Phase 5 (Verify):** full suite green; layer routing + token/citation wiring correct; provider unit tests pass.
6. **Phase 6:** defects → regression + unit tests then fix (Layer-0 ambiguity, escalation bugs).
7. **Phase 7:** branch `slice/6b-llm` → green → merge `main`; CHANGELOG; tag `v0.0.9-query`. **RAG loop complete.**

**Risk/impact expectation:** Low/Med → Low — additive; the deterministic answer layers and the provider
are tested without any network; the real-LLM test is gated on an API key.

## Decisions needed from owner at GATE 0
- Approve the four Phase 0 documents **and** Q1 (one OpenAI-compatible HTTP provider this slice; Anthropic/others later?).
- Q2 (escalation thresholds vs RRF — availability-driven) and Q3 (provider assembly) can defer to ADR-0009.
