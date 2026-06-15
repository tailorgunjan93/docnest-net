# SLICE-09 — QA / User

"Working" = the eval can be run with an LLM-as-judge that grades answers on the same 0–10 rubric as the
Python reference, so the two evals are comparable — while the zero-cost local judge stays the default and
nothing about the shipped library changes.

## Scenarios (positive)
- **Judge enabled** — set `DOCNEST_JUDGE_API_KEY` (+ optional `DOCNEST_JUDGE_MODEL` /
  `DOCNEST_JUDGE_BASE_URL`), run `dotnet run` in `eval/DocNest.Eval`. Header reads
  *"judge: LLM (openai-compatible/<model>)"*; each row's Score comes from the LLM; the run completes
  and writes `eval/results/report.md`.
- **Number-less but correct** — ground truth `"November 2015"`, answer `"November."` → LLM judge ≥ 7
  (where `LocalJudge` gives 5). This is the headline comparability fix (BA AC3).
- **Approximate number** — truth `"1.09°C"`, answer `"about 1.1°C"` → LLM judge 8–9 (generosity clause).
- **Judge disabled (default)** — `DOCNEST_JUDGE_API_KEY` unset → identical to today: local judge,
  header *"judge: local …"*, zero judge tokens.

## Edge / negative
- **Malformed judge output** — model returns no `SCORE:` line → `ParseScore` returns the defined default
  `(5, "parse error")`; the run continues (no crash). Mirrors Python.
- **`SCORE: 8/10` / `Score:8` / `score: 9 out of 10`** — all parse to the integer (8, 8, 9).
- **Out-of-range / junk** — `SCORE: 99` or `SCORE: banana` → no digit in `[0..10]` captured → default 5.
- **Rate-limit (Groq free tier 429)** — handled by the reused `RetryingLlmProvider` (honours
  *"try again in Xs"*, else exponential backoff); no lost questions.
- **Judge key set but provider unreachable / auth fails** — `IntelligenceException` surfaces (run fails
  loudly) rather than silently scoring 0; the user fixes the key/URL and re-runs.
- **Answer LLM off + judge LLM on** — deterministic-floor answers (0 answer-tokens) graded by the LLM
  judge: both env vars are independent and both states work.

## Regression view
- Full xUnit suite green every cycle (regression-first; the suite only grows).
- **New tests** (`tests/DocNest.Eval.Tests`, all offline via a scripted/fake `ILlmProvider`):
  - `ParseScore` table: `"SCORE: 8\nREASONING: x"`→8; `"SCORE: 8/10"`→8; `"score: 9 out of 10"`→9;
    `"Score:7"`→7; no-score / junk → `(5, "parse error")`; reasoning extracted after first `:`.
  - `LlmAnswerJudge.ScoreAsync` with a scripted provider returning `"SCORE: 9 …"` → verdict 9; asserts
    the provider was called once and the prompt embeds question + candidate + reference + the rubric.
  - `JudgeFactory.Create(value)` → `LocalAnswerJudge` when key blank/null; `LlmAnswerJudge` when set
    (assert type + `Name`), without reading real environment.
  - **Regression anchor** — `LocalAnswerJudge.ScoreAsync` reproduces `LocalJudge.Score` exactly for
    representative inputs, and the `"November."` vs `"November 2015"` case scores **< 7 locally** (locks
    the documented gap the LLM judge is meant to close).
- **Eval re-run (manual, owner-run with the Groq key)** — record before/after headline in
  `eval/results/` (local-judge 6.7/10 vs LLM-judge on the same answers) and note the new comparability.

## Done means
BA AC1–AC5 hold, demonstrated by the new offline tests + a documented eval re-run, full suite green,
shipped library untouched — with owner sign-off. No merge to `main`, no NuGet publish (owner-triggered).
