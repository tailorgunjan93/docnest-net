# ADR-0011 — Query escalation confidence (Layer-1/2 gating)

- Status: Accepted (SLICE-08)
- Date: 2026-06-10
- Supersedes/relates: ADR-0009 (answer engine), ADR-0007 (hybrid retrieval)

## Context
The accuracy eval (identical 88-question set to the Python reference) showed the .NET engine answering
~80/88 questions at 0 tokens and **never escalating to the LLM**, scoring 5.1/10 vs the Python
reference's 8.5/10. Root cause: `DocNestQueryEngine` returned the first Layer-1 extractive snippet
**unconditionally**. The Python `reader.py` gates Layer 1 on `top_score ≥ 0.35` and Layer 2 on
`≥ 0.15`, escalating otherwise — but that `top_score` is a `0.6·BM25 + 0.4·semantic` **weighted sum**,
whereas the .NET retriever exposes only **RRF** scores (`weight/(60+rank+1)` ≈ 0.05 at the top), which
are a rank-fusion value, not an absolute confidence. Copying `0.35/0.15` onto RRF would push every
question below threshold and escalate everything, destroying the 0-token property.

## Decision
Keep **RRF for ranking** (ADR-0007 unchanged). Introduce a separate, bounded **absolute confidence**
for the **top candidate only**, used solely for the Layer-1/2 escalation decision:
`confidence = |query content tokens ∩ section(title+keywords+text) tokens| / |query content tokens|`
∈ [0,1]. Gate Layer 1 on `confidence ≥ L1_THRESHOLD`; below it (with a provider) escalate to the LLM
layers; with no provider, return the deterministic empty result (parity with Python `allow_llm=False`).

Separately, tighten `KeyNumberExtractor` to drop `count` numbers that are names/versions (`Llama 2`)
or structural references (`Figure 4`, `Section 23.3`), which were misfiring as confident Layer-0
answers on PDFs.

## Consequences
- Faithful to Python's escalation *intent* with a scale-stable signal; RRF ranking and the `.udf`
  contract are untouched; `AnswerAsync`/`QueryResult` public surface unchanged.
- Low-confidence questions now cost LLM tokens (by design). Thresholds are calibrated on the eval and
  locked by tests; they can be revisited if a future dense-embedding path adds cosine to the confidence.
- Alternatives rejected: (1) recalibrate a single RRF cutoff — RRF isn't an absolute confidence, fragile;
  (2) port `0.6·BM25+0.4·semantic` wholesale — re-introduces an unbounded scale and a second scorer.
