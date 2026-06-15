# SLICE-09 — Dev / Technical

## Code read (reference vs port)

### Python reference — `D:\Learning\docnest\eval\rag_accuracy_eval.py`
- **`_judge(llm, question, candidate, reference, is_ground_truth)`** (≈ line 2694) — the LLM-as-judge.
  - Builds a prompt: *"Score the CANDIDATE answer for factual accuracy against the {GROUND TRUTH |
    REFERENCE}."* When `is_ground_truth`, adds a **generosity clause**: be lenient with approximate
    numbers (`~1.1°C` ≈ `1.09°C`); award 8–9 for the core claim with minor omissions; reserve 6/4/0.
  - **Rubric**: `10=perfect, 9=trivial omission, 8=mostly correct minor gaps, 6=partial key facts missing,
    4=mostly wrong, 2=almost entirely wrong, 0=wrong/hallucinated.`
  - Demands a fixed format: `SCORE: <0-10>` / `REASONING: <one sentence>`.
  - **Robust parse loop**: per line, `startswith("SCORE")` → `re.search(r'\b([0-9]|10)\b', line)` (handles
    `"SCORE: 8"`, `"SCORE: 8/10"`, `"Score:8"`, `"score: 9 out of 10"`); `startswith("REASONING")` →
    text after the first `:`. **Default on no parse = `(5, "parse error")`.**
  - Retry via `_invoke_with_retry` (rate-limit handling).
- The reference uses **≥ 7 = hit** (`pass_rate`, line ≈ 1711) — identical to `LocalJudge`.

### .NET port — `eval/DocNest.Eval`
- **`LocalJudge.Score(question, candidate, reference) -> (int, string)`** — the zero-API heuristic; keep
  as-is. It is the fallback and the regression anchor.
- **`OpenAiCompatibleLlmProvider`** (`src/DocNest.Query`) — `CompleteAsync(prompt, system, temperature,
  maxTokens, ct)`; OpenAI-compatible `/chat/completions`; throws `IntelligenceException` on failure.
- **`RetryingLlmProvider`** (`eval/DocNest.Eval`) — decorator that retries `IntelligenceException`
  rate-limits (429 / `rate_limit`), honouring `"try again in Xs"`. **Reuse verbatim.**
- **`Program.cs`** — builds the answer-gen `llm` from `DOCNEST_LLM_API_KEY`; calls
  `LocalJudge.Score(qa.Q, result.Answer, qa.Truth)` per question (lines ~93–94); header line ~54 prints
  the active judge. These are the only call sites to change.
- **cases.json** — every case carries an authored `truth`. So in the .NET eval **every reference is
  ground truth** → the judge always uses the generosity clause (`isGroundTruth=true`). (Unlike Python
  Phase 2, there is no "Gemini baseline" reference path here.)
- Test scaffolding to reuse: `ScriptedLlm` / `FakeLlm : ILlmProvider` patterns in
  `tests/DocNest.Query.Tests/EscalationGateTests.cs`.

## What to build (file-by-file)
1. **`eval/DocNest.Eval/IAnswerJudge.cs`** (new) — Strategy interface:
   `Task<JudgeVerdict> ScoreAsync(string question, string candidate, string reference, CancellationToken ct = default)`
   where `JudgeVerdict` is a `readonly record struct (int Score, string Reason)`. Plus a `string Name { get; }`
   for the report header.
2. **`eval/DocNest.Eval/LocalAnswerJudge.cs`** (new) — wraps the existing static `LocalJudge`
   (`Task.FromResult`). `Name => "local (number ±6% + keyword + phrase overlap)"`. Zero-cost default.
3. **`eval/DocNest.Eval/LlmAnswerJudge.cs`** (new) — holds an `ILlmProvider`; builds the Python-parity
   prompt + rubric; calls `CompleteAsync`; parses via a **pure static `ParseScore(string) -> JudgeVerdict`**
   (faithful port of the Python loop, default `(5, "parse error")`). `Name => $"LLM ({provider}/{model})"`.
4. **`eval/DocNest.Eval/JudgeFactory.cs`** (new) — `Create()` reads `DOCNEST_JUDGE_API_KEY`
   (+ `DOCNEST_JUDGE_MODEL`, `DOCNEST_JUDGE_BASE_URL`); key present → `LlmAnswerJudge` over
   `RetryingLlmProvider(OpenAiCompatibleLlmProvider(...))`; else → `LocalAnswerJudge`. A second overload
   takes the raw values (no `Environment.*`) so it is unit-testable without touching real env.
5. **`eval/DocNest.Eval/Program.cs`** (edit) — construct `IAnswerJudge judge = JudgeFactory.Create();`
   once; replace the `LocalJudge.Score(...)` call with `await judge.ScoreAsync(...)`; update the header
   line to print `judge.Name` and (when LLM) note non-zero judge tokens are expected. ~6 lines changed.
6. **`eval/DocNest.Eval/DocNest.Eval.csproj`** (edit) — add `InternalsVisibleTo("DocNest.Eval.Tests")`.
7. **`tests/DocNest.Eval.Tests/`** (new xUnit project) — ProjectReference → `DocNest.Eval`; add to
   `DocNest.sln` so the full suite covers it. (eval stays standalone; the test project pulls it in by path.)

## Open questions for the owner (GATE 0 / 2)
- **Q1 — slice id.** Proposed `SLICE-09`. OK, or fold under a different label?
- **Q2 — test home.** Add a `tests/DocNest.Eval.Tests` project to `DocNest.sln` (matches the per-project
  test convention; requires `InternalsVisibleTo`). **Recommended.** Alternative: make `ParseScore`/factory
  `public` and skip the project — but every other component here has its own `.Tests`.
- **Q3 — judge model default.** Mirror the existing `DOCNEST_LLM_*` defaults: model `gpt-4o-mini`,
  base `https://api.openai.com/v1`. For Python parity on Groq you'd set `DOCNEST_JUDGE_MODEL=…` +
  `DOCNEST_JUDGE_BASE_URL=https://api.groq.com/openai/v1` (Groq key in `D:\Learning\docnest\.env`).

## Backward compatibility
- `eval/DocNest.Eval` is `IsPackable=false` and **not in `DocNest.sln`** → no shipped surface touched.
- Default path (no `DOCNEST_JUDGE_API_KEY`) is byte-for-byte the current local-judge behaviour.
- `LocalJudge.Score` signature/logic unchanged; new code sits behind the new `IAnswerJudge` wrapper.

## Complexity / NFR
- Judge call is O(1) per question (one LLM round-trip when enabled; pure string parse otherwise).
- No change to engine/retrieval latency or memory. Cost is opt-in tokens, only when the key is set.
- The judge LLM is **decoupled** from the answer LLM, so the deterministic-floor answer path can still
  run at 0 answer-tokens while being LLM-judged (or vice-versa).
