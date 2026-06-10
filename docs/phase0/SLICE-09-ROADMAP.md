# SLICE-09 — Roadmap

Ordered steps, dependencies, gates. Owner signs off every gate. Eval-harness-only; **no merge to
`main`, no NuGet publish** (owner-triggered).

1. **GATE 0 — Phase 0 understanding** (this set: BA / DEV / QA / IMPACT-DESIGN / ROADMAP). Confirm
   scope + the Strategy+Factory design + Dev Q1–Q3 (slice id, test project, judge-model default).
   ← **awaiting go.**
2. **GATE 1/2 — impact + design** — recorded in [SLICE-09-IMPACT-DESIGN.md](SLICE-09-IMPACT-DESIGN.md):
   Risk=Low, Impact=Low; no shipped surface touched; Strategy+Factory behind `ILlmProvider`. No ADR
   needed (confirm). Depends on GATE 0.
3. **Phase 3 — tests first (fail)** — create `tests/DocNest.Eval.Tests` (+ add to `DocNest.sln`,
   + `InternalsVisibleTo`): `ParseScore` table, `LlmAnswerJudge` over a scripted provider, `JudgeFactory`
   selection, and the locked `LocalAnswerJudge` regression. Run; confirm they fail (types don't exist yet).
4. **Phase 4 — implement** — `IAnswerJudge`, `LocalAnswerJudge`, `LlmAnswerJudge` (+ pure `ParseScore`),
   `JudgeFactory`; wire into `Program.cs`. `dotnet build` clean; make the new tests pass.
5. **Phase 5 — full suite + eval** — entire xUnit suite green (153 + new tests). Owner runs the eval
   with the Groq key (`DOCNEST_JUDGE_API_KEY` + Groq base/model) and **without** it (default unchanged);
   record before/after in `eval/results/` and confirm AC1–AC5.
6. **Phase 6 — defects / RCA** — any escaped issue → regression test + root-cause note.
7. **GATE — green** — only all-green earns ✅.
8. **Phase 7 — git (owner-triggered)** — temp branch `slice-09-eval-llm-judge` → all green → **stop**.
   Owner decides merge to `main`. No version bump / NuGet publish (eval is not shipped).

## Risk / impact
Risk **Low**, Impact **Low** — opt-in eval tooling; default path byte-for-byte unchanged; shipped
library / public API / `.udf` / NuGet untouched. Mitigated by: unchanged `LocalJudge` default, LLM behind
existing wrappers, defined parse default, regression-first full suite, locked local-judge regression test.

## Outcome / RCA (Phase 5–6)
Implemented on branch `slice-09-eval-llm-judge` (uncommitted, owner to commit/merge).
- **Code:** `IAnswerJudge` + `JudgeVerdict`, `LocalAnswerJudge`, `LlmAnswerJudge` (pure `ParseScore`),
  `JudgeFactory`; `Program.cs` builds the judge once and reports `judge.Name` in the header.
  `InternalsVisibleTo("DocNest.Eval.Tests")`; eval + new test project added to `DocNest.sln`
  (the .NET 10 CLI auto-created an `eval` solution folder).
- **Tests:** +19 in `tests/DocNest.Eval.Tests`; confirmed failing-first (types absent), then green.
- **Full suite:** 173 pass / 5 skip / 0 fail — no regression. `dotnet build` clean (0 errors;
  pre-existing CA suggestions only).
- **AC status:** AC2 (default unchanged) + AC4 (resilience/parse default) + AC5 (no regression) verified
  by tests + suite. **AC1/AC3 (live LLM-judge grading + comparability numbers) pending the owner's eval
  re-run** with the Groq key — same provider plumbing already proven against Groq in Slice 8, so this is
  measurement, not correctness. The run overwrites `eval/results/report.md`, so the Slice-8 baseline
  should be copied aside first.
- **Phase 7:** not started — owner-triggered (no commit/merge/publish performed by the agent).
