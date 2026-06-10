# SLICE-08 — Impact / Risk + Design (Phase 1–2)

## Impact map
| Area | Change | Backward-compat |
|------|--------|-----------------|
| `src/DocNest.Query/DocNestQueryEngine.cs` | Add an absolute top-candidate **confidence**; gate Layer 1 on it; escalate to LLM below it. | `AnswerAsync` signature + `QueryResult` shape unchanged. |
| `src/DocNest.Query/Confidence.cs` (new) | Wrapped helper: bounded [0,1] query-term recall over the top section (title + keywords + text). | Internal; new file. |
| `src/DocNest.Core/Intelligence/KeyNumberExtractor.cs` | Add a noise gate: skip `count` numbers that are name/version (`Llama 2`) or structural references (`Figure 4`, `Section 23.3`, `Page 7`). | Pure tightening; `.udf` content can only lose junk, never schema. |
| `tests/…Query.Tests` / `…Core.Tests` | +escalation-gate tests, +key-number-misfire test. | Additive. |

**No change** to: public API, `UDF_VERSION`/`.udf` schema, the RRF retriever ranking, the CLI.

**Risk = Low. Impact = Low–Medium** (query behaviour: low-confidence questions now reach the LLM).
Mitigations: RRF ranking untouched; deterministic-only path preserved; regression-first; eval gate (AC3).

## Design

### Escalation confidence (Option 3)
`Confidence.Of(question, section)` → `[0,1]`:
- Query content tokens = `TokenRe`(question), len > 2, minus `QueryConstants.Fillers`.
- Section term set = tokens of `Title` + `Keywords` + `Text` (lower-cased, len > 2).
- `confidence = |query ∩ sectionTerms| / |query|` (query-term **recall** — bounded, scale-stable;
  mirrors the intent of Python's `0.6·BM25 + 0.4·Jaccard` confidence without RRF's relative scale).
- Complexity O(section tokens); no allocation beyond two small sets. No effect on retrieval latency.

`AnswerAsync` flow (Layer 0 unchanged, first):
```
Layer 0 (precomputed) → if hit, return
hits = retrieve(); if none → (LLM full-doc if allowed, else empty -1)
conf = Confidence.Of(q, hits[0].Section)
if conf >= L1_THRESHOLD:           → Layer 1 (summary / extractive, 0 tokens)
elif allowLlm && llm != null:
    if conf >= L2_THRESHOLD:       → Layer 2 (single-section LLM)
    elif hits >= 2:                → Layer 3 (multi-section LLM)
    else:                          → Layer 4 (full-doc LLM)
else:                              → empty -1   (deterministic-only, low confidence)
```
`L1_THRESHOLD` / `L2_THRESHOLD` are consts **calibrated on the eval** in Phase 5 (start 0.6 / 0.25,
tune so Phase-1 number questions stay Layer 0/1 and complex/PDF questions escalate).

### Key-number noise gate (D-C)
In `KeyNumberExtractor.Extract`, after the existing identifier/acronym/year skips, additionally skip a
`count`-kind number when:
- the **bound label** (case-sensitive source token immediately before the number) is a single
  **Capitalized proper noun** directly abutting the number → name/version (`Llama 2`, `GPT 3`), or
- the immediately-preceding word is a **structural reference**:
  `{figure, fig, table, section, sec, chapter, page, eq, equation, appendix, part, step, item, line,
  note, footnote, ref, no, version, v}`.
This kills the observed `"Llama: 4"`, `"GPT-: 10"`, `"§23.3"`-style Layer-0 answers without touching
legitimate labelled metrics (`uptime`, `margin`, `Total engineers: 24` — colon-bound, not name+number).

## Validation
- Unit tests reproduce both defects on representative text (deterministic, no network).
- Eval re-run (deterministic floor + gpt-oss-120b) confirms AC3 on the real files.
