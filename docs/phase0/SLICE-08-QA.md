# SLICE-08 — QA / User

"Working" = the engine answers complex questions as well as the Python reference does, by using the
LLM when the deterministic layers aren't confident, and never inventing a key-number answer.

## Scenarios (positive)
- Complex / multi-part PDF question (e.g. "list ALL Llama 2 parameter sizes") → escalates to LLM
  (Layer ≥ 2, non-zero tokens) and returns a synthesized answer.
- Simple exact number question already answered well (xlsx "highest annual revenue") → stays Layer 0/1,
  0 tokens, still correct (no regression).
- Genuine, unambiguous key number ("estimated serviceable obtainable market") → Layer 0 still answers.

## Edge / negative
- No LLM provider configured + low confidence → returns the deterministic `-1`/empty result (graceful;
  the eval's "deterministic floor" still runs).
- Ambiguous key number (same label, different values) → no answer at Layer 0 (already handled by matcher);
  must also not misfire on single-token PDF labels (D-B/D-C).
- Empty document / no hits → no crash; returns empty result.
- Provider error/timeout → surfaces as today (IntelligenceException), not a silent wrong answer.

## Regression view
- Full xUnit suite green every cycle (regression-first; the suite only grows).
- Add: escalation-gate tests (low confidence → LLM via a fake provider; high confidence → 0 tokens) and
  a key-number-misfire test (the four academic PDFs / representative prose → no `"<Name>: <n>"` answer).
- Re-run the multi-format eval (gpt-oss-120b) and record before/after in `eval/results/`.

## Done means
AC1–AC4 in the BA doc all hold, demonstrated by the new tests + the eval re-run, with owner sign-off.
