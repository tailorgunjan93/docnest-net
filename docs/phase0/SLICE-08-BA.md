# SLICE-08 — BA / Functional: Query-engine accuracy parity

## Why
The accuracy eval (same 10 files / 88 questions / 5 formats as the Python reference,
`eval/cases.json` ← `D:\Learning\docnest\eval\rag_accuracy_eval.py`) scores the .NET engine at
**5.1/10 overall** (Phase 1 generated 6.4, Phase 2 PDFs 2.6) with gpt-oss-120b via Groq — versus
the Python reference's documented **8.5/10 / 89%** on the same model. The parsers are sound (all
10 files ingest cleanly); the gap is in the **query/answer path**: ~80 of 88 questions are answered
at **0 tokens** because the engine terminates at the deterministic layers and **never escalates to
the LLM** (only ~1k tokens used across all 88 Qs). Two defects:

- **D-A — no escalation gate.** `DocNestQueryEngine` returns the first non-empty Layer-1 extractive
  snippet unconditionally; the Python engine gates Layer 1 on a confidence threshold and otherwise
  falls through to the LLM (Layers 2–4).
- **D-B — Layer-0 key-number misfire on PDFs.** Spurious answers such as `"Llama: 4 (source: §23.3)"`,
  `"GPT-: 10"`, `"Fiscal pressure points: 22"` are returned at confidence 1.0, terminating before
  retrieval. Llama 2 + Constitutional AI PDFs score 0%.

## What (scope)
Restore Python-parity layer escalation so low-confidence questions reach the LLM, and stop the
Layer-0 key-number misfires — without touching the RRF production retriever's ranking, the public
API, or the `.udf` contract.

## Acceptance criteria
- **AC1** — When top-retrieval confidence is low, the engine escalates to LLM Layers 2–4 instead of
  returning a 0-token extractive snippet. (Observable: PDF/multi-part questions show Layer ≥ 2 and
  non-zero tokens when a provider is supplied.)
- **AC2** — Layer 0 returns a key-number answer only on a genuine, unambiguous label match; no
  spurious single-token-label answers on PDF prose. The four academic PDFs no longer emit
  `"<Name>: <n>"`-style junk.
- **AC3** — Re-running the eval with gpt-oss-120b materially closes the gap: **Phase 2 (PDF)
  hit-rate ≥ 60%** and **overall ≥ 8.0/10** (the agreed bar; the Python ref is 8.5).
- **AC4 — no regression** — Phase-1 deterministic number questions that already score ≥ 7 stay ≥ 7;
  the full xUnit suite stays green; public API, `QueryResult` shape, and `.udf`/`UDF_VERSION`
  unchanged. Deterministic-only mode (no provider) still returns gracefully.

## Non-goals
- Changing the RRF retriever's ranking order or the `.udf` schema.
- PDF table / OCR / scanned-doc hardening (separate hardening track).
- Bit-for-bit equality with Python — parity is behavioural (escalation + no misfire), not identical text.
