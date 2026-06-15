# SLICE-09 — BA / Functional: Optional LLM-as-judge for the accuracy eval

> Eval-harness-only slice. **No change to the shipped library, public API, `.udf`/`UDF_VERSION`,
> or NuGet package.** This is the follow-up explicitly opened in
> [SLICE-08-ROADMAP.md](SLICE-08-ROADMAP.md) §"AC3 not met" → *"add an LLM-judge to the eval."*

## Why
The .NET eval (`eval/DocNest.Eval`) scores each answer with **`LocalJudge`** — a number/keyword/phrase
overlap heuristic ported from the Python `_local_judge`. It is **stricter than the Python reference
eval's LLM-as-judge** (Gemini / gpt-oss). Concretely, when the ground-truth answer contains a number,
a correct-but-number-less answer is penalised heavily:

- Truth `"November 2015"`, candidate `"November."` → `numRatio=0` (truth has the year `2015`,
  candidate has no digits), `kwRatio=0.5` → `combined = 0.5·0 + 0.3·0.5 + 0.2·0 = 0.15` → **5/10 (a miss)**.
  An LLM judge with the Python rubric awards **8–9** ("captures the core factual claim").

Because of this, the .NET eval's **6.7/10** (Slice 8) is **not directly comparable** to the Python
reference's **8.5/10** on the same model: part of the gap is the *judge*, not the engine. Slice 8's RCA
named two out-of-scope causes for the residual gap — BM25-only retrieval **and** the stricter local
judge. This slice removes the second confound so the two evals can be compared apples-to-apples.

## What (scope)
Add an **optional LLM-as-judge** to `eval/DocNest.Eval` that grades each answer against the
ground-truth on a **0–10 scale with a short rubric**, matching the Python eval's `_judge` approach.

- Reuse the existing `OpenAiCompatibleLlmProvider` (in `DocNest.Query`) wrapped by the eval's
  rate-limit-resilient `RetryingLlmProvider`.
- **Gate on an env var** (`DOCNEST_JUDGE_API_KEY`). When absent, fall back to `LocalJudge`.
- Keep **`LocalJudge` as the zero-cost default** — no behaviour change when no judge key is set.
- The judge LLM is **independent** of the answer-generation LLM (`DOCNEST_LLM_API_KEY`): either, both,
  or neither may be configured.

## Acceptance criteria
- **AC1** — With `DOCNEST_JUDGE_API_KEY` set, every answer is graded by the LLM judge on 0–10 using the
  Python-parity rubric; the report header states the active judge (provider + model).
- **AC2** — With `DOCNEST_JUDGE_API_KEY` **unset**, the eval behaves **exactly as today**: `LocalJudge`
  scores everything, header reads "judge: local …", **zero judge tokens**. (Default unchanged.)
- **AC3** — The LLM judge correctly grades the known local-judge failure mode: a correct number-less
  answer whose ground truth contains a number (e.g. `"November."` vs `"November 2015"`) scores **≥ 7**,
  where `LocalJudge` scores < 7. (Demonstrated by a unit test on the response-parsing + a documented
  eval re-run, not a live network assertion.)
- **AC4** — The judge is resilient to the Groq free-tier 429s (reuses `RetryingLlmProvider`) and to
  malformed model output (unparseable response → defined default, never a crash).
- **AC5 — no regression** — the full xUnit suite stays green; `LocalJudge`'s scoring is unchanged
  (locked by a regression test); the shipped library / public API / `.udf` / NuGet are untouched.

## Non-goals
- Changing `LocalJudge`'s heuristic or its thresholds.
- Changing the engine, retriever, embeddings, or any shipped `src/` code.
- A NuGet release (eval is `IsPackable=false`, not in `DocNest.sln`); **no merge to `main`, no publish** —
  owner-triggered only.
- Bit-for-bit equality with the Python judge — parity is *behavioural* (same rubric, same 0–10 scale,
  same ≥7 hit bar), not identical wording.
- Wiring dense embeddings into the eval retrieval path (the *other* Slice-8 follow-up — separate slice).
