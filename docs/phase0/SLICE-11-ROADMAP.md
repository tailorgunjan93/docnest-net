# SLICE-11 — Roadmap

Ordered steps, gates. Owner signs off every gate. **No merge to `main`, no NuGet publish** (owner-triggered).

1. **GATE 0 — Phase 0** (this set: BA / DEV / QA / IMPACT-DESIGN / ROADMAP) + **ADR-0013** (reranker).
2. **Phase 3 — tests first** — `CrossEncoderRerankerTests` (real-model `[SkippableFact]`); engine
   regression tests (Layer-3 refusal→Layer-4; enumeration skips Layer-0 key-number; simple key-number
   still Layer-0). Fail first / pin behaviour.
3. **Phase 4 — implement** — `IReranker` + `OnnxCrossEncoderReranker` + `CrossEncoderModel` +
   `WordPieceTokenizer.EncodePair`; `HybridRetriever` rerank pass; `DocNestQueryEngine` answer-quality
   fixes; eval wiring. `dotnet build` clean.
4. **Phase 5 — full suite + eval** — entire xUnit suite green; re-run the eval (Cerebras narrator +
   qwen2.5 judge); record before/after.
5. **GATE — green.** **Phase 7 — git:** commit on the temp branch; **stop** (owner decides merge).

## Risk / impact
Risk **Low–Medium**, Impact **Medium** (PDF accuracy ↑; more LLM escalation; a second opt-in model).
Shipped API / `.udf` / RRF math untouched; graceful degrade without the reranker.

## Outcome / RCA (Phase 5)
- **Result (Cerebras gpt-oss-120b narrator + qwen2.5 judge + dense + rerank):**
  - **Phase 2 (PDFs): 5.1 → 7.1/10, hit-rate 47% → 73%.** Per-file: constitutional 8.6/100%,
    attention 8.2/100%, ipcc 7.8, gpt3 6.0, bis 6.0, llama2 6.2.
  - **Phase 1 (generated): 7.6/10, 83%** — no regression.
  - **Overall ≈ 7.4/10, ~80% hit** (composed from the two complete same-config phase runs; see note).
- **Biggest movers (reranker):** gpt3 3.4→6.0, bis 3.6→6.0, attention 6.2→8.2 — the cross-encoder put the
  answer-bearing section in front of the narrator. **Complex-gate** killed the `corpora: 2`-style Layer-0
  misfires; **refusal→Layer-4** + **1500-token budget** removed empty/refusal answers.
- **Residual gap to the Python honest 8.5:** 3 zeros remain — exhaustive-list completeness
  (llama2 "all parameter sizes" / "reward models", gpt3 "all corpora") + qwen judge strictness. These are
  answer-coverage/judge issues, not retrieval. **9.55 is a qwen-judge artifact, not a reproducible target.**
- **Measurement note:** the laptop slept across the multi-day session, killing four long runs mid-Phase-2.
  Each phase was measured complete under the identical final config; the overall is their question-weighted
  combination (exactly what the eval's "Overall" row computes). A single uninterrupted both-phase run
  (machine kept awake) would confirm it.

## Follow-ups (not done here)
- Answer-coverage verification for exhaustive-list questions (re-ask for missing items).
- A stronger / steadier narrator (Cerebras rate-limits; Groq refused less).
- Wire the reranker into the CLI `query` path.
