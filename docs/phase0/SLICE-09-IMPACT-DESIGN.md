# SLICE-09 — Impact / Risk + Design (Phase 1–2)

## Impact map
| Area | Change | Backward-compat |
|------|--------|-----------------|
| `eval/DocNest.Eval/IAnswerJudge.cs` (new) | Strategy interface + `JudgeVerdict` record struct. | Internal to eval; new file. |
| `eval/DocNest.Eval/LocalAnswerJudge.cs` (new) | Wraps existing `LocalJudge` (zero-cost default). | Delegates to unchanged `LocalJudge`. |
| `eval/DocNest.Eval/LlmAnswerJudge.cs` (new) | Python-parity prompt+rubric over `ILlmProvider`; pure `ParseScore`. | New file; opt-in only. |
| `eval/DocNest.Eval/JudgeFactory.cs` (new) | Selects judge from `DOCNEST_JUDGE_API_KEY` (+model/baseurl). | New file. |
| `eval/DocNest.Eval/Program.cs` (edit) | Build judge once; `await judge.ScoreAsync(...)`; header prints `judge.Name`. | ~6 lines; default output unchanged. |
| `eval/DocNest.Eval/DocNest.Eval.csproj` (edit) | `InternalsVisibleTo("DocNest.Eval.Tests")`. | Additive. |
| `tests/DocNest.Eval.Tests/` (new) + `DocNest.sln` (edit) | xUnit project; add to solution. | Additive. |

**No change** to: any `src/` (shipped library), public API, `UDF_VERSION` / `.udf` schema, the engine /
retriever / embeddings, the CLI, or the NuGet package (`IsPackable=false`).

**Risk = Low. Impact = Low.** The only runtime-behaviour change is *opt-in* (requires a new env var);
the default path is byte-for-byte today's local-judge output. Worst case of a bug is a wrong *eval
score number in a non-shipped harness*, caught by the new tests and the locked local-judge regression.

Mitigations: default = unchanged `LocalJudge`; LLM access stays behind the existing `ILlmProvider`
wrapper + `RetryingLlmProvider`; parse has a defined default; full suite regression-first.

## Design

### Pattern — Strategy + Factory (SOLID)
- **Strategy** (`IAnswerJudge`) lets the eval pick local vs LLM judging without `if`-laddering at the
  call site → **Open/Closed** (add a future judge as a new impl), **Dependency-Inversion** (`Program`
  depends on the interface, not a concrete judge).
- **Single-Responsibility**: `LocalAnswerJudge` = heuristic; `LlmAnswerJudge` = LLM grading + parse;
  `JudgeFactory` = selection/wiring. `LocalJudge` (the heuristic itself) is untouched.
- **Wrapper rule**: the external LLM is already behind `ILlmProvider`; `LlmAnswerJudge` is the eval-side
  wrapper that turns a provider into a judge. No raw HTTP added.

### `IAnswerJudge`
```csharp
internal readonly record struct JudgeVerdict(int Score, string Reason);

internal interface IAnswerJudge
{
    string Name { get; }                                 // for the report header
    Task<JudgeVerdict> ScoreAsync(string question, string candidate,
                                  string reference, CancellationToken ct = default);
}
```

### `LlmAnswerJudge` — prompt + rubric (Python-parity)
- System: empty (reference uses a single user prompt). Temperature low (0.0–0.1), `maxTokens` small (~128).
- Prompt mirrors `_judge` with `is_ground_truth=true` (all `cases.json` references are authored truth):
  *"Score the CANDIDATE answer for factual accuracy against the GROUND TRUTH … be generous with
  approximate numbers … award 8–9 for the core claim with minor omissions … "* + the 10/9/8/6/4/2/0
  rubric + `Respond EXACTLY: SCORE: <0-10> / REASONING: <one sentence>`.
- **`static JudgeVerdict ParseScore(string response)`** — faithful port of the Python loop:
  split lines; a line upper-starting `SCORE` → first `\b([0-9]|10)\b` match, clamp `[0..10]`; a line
  upper-starting `REASONING` → text after first `:`. **No score parsed ⇒ `(5, "parse error")`.**
  Pure, deterministic, the unit-test seam (no network).

### `JudgeFactory`
```
Create()                          → Create(Environment("DOCNEST_JUDGE_API_KEY"),
                                            Environment("DOCNEST_JUDGE_MODEL")    ?? "gpt-4o-mini",
                                            Environment("DOCNEST_JUDGE_BASE_URL") ?? "https://api.openai.com/v1")
Create(apiKey, model, baseUrl)    → string.IsNullOrEmpty(apiKey)
                                       ? new LocalAnswerJudge()
                                       : new LlmAnswerJudge(new RetryingLlmProvider(
                                             new OpenAiCompatibleLlmProvider(apiKey, model, baseUrl)))
```
Defaults intentionally mirror the existing `DOCNEST_LLM_*` wiring in `Program.cs` for consistency.

### `Program.cs` (minimal edit)
- Once, near the existing `apiKey`/`llm` block: `var judge = JudgeFactory.Create();`
- Header: replace the hard-coded *"judge: local …"* string with `judge.Name`; when LLM, drop the
  *"0 LLM tokens"* implication for the judge line.
- In the per-question loop: `var (score, _) = await judge.ScoreAsync(qa.Q, result.Answer, qa.Truth);`
  (was `LocalJudge.Score(...)`). `RunPhase` is already `async`.

## Complexity / NFR
- Per question: O(1) — one LLM round-trip when enabled (ret* on 429), else a pure string scan.
- No effect on the engine's ~1 ms warm-query budget or memory (judge runs after answering, in the harness).
- Cost: opt-in tokens only when `DOCNEST_JUDGE_API_KEY` is set; default run spends **0 judge tokens**.

## ADR?
No new ADR required — this is eval tooling, introduces no shipped-API or `.udf` decision. The Strategy
choice is recorded here. (Confirm at GATE 2; an ADR can be added if the owner prefers.)

## Validation
- Offline unit tests cover `ParseScore`, `LlmAnswerJudge` (scripted provider), `JudgeFactory` selection,
  and the locked `LocalAnswerJudge` regression — no network in CI.
- Owner-run eval re-run with the Groq key confirms AC1/AC3 on the real files; before/after in `eval/results/`.
