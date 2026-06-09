# Phase 0.4 — Roadmap: Slice 3 (Pipeline + Normaliser)

> Consolidates the Slice-3 BA/Dev/QA into ordered steps. Completing this slice reaches **milestone
> M1**: a `.udf` round-trips between Python and .NET on synthetic documents (Slices 1–3), with §ids,
> hierarchy, and deterministic intelligence all matching Python. Program roadmap
> ([../ROADMAP.md](../ROADMAP.md)) unchanged.

## Ordered steps (what GATE 0 approves entering Phase 1 for)
1. **Phase 1 (Impact & risk):** additive (new `Pipeline`/`Intelligence` types in `DocNest.Core`); no
   `.udf`/Slice-1/2 changes. Watch-items: regex/`\b`/`\d` parity with Python; immutable rebuild
   correctness for the child-link two-pass. Risk/Impact expected Low/Low.
2. **Phase 2 (Design + ADR-0003):** resolve Dev Q1–Q4 (pipeline surface, namespaces, extractor
   interfaces, token_count/split semantics); the two-pass immutable normalisation design; the
   `[GeneratedRegex]` set; file-by-file code plan.
3. **Phase 3 (Test-first):** write U1–U14, I1–I3 from the parity case tables — failing first. Where a
   Python env is available, add a guarded cross-check of the extractor outputs (optional, like Slice-2 E2).
4. **Phase 4 (Implement):** `SectionNormaliser`, `KeyNumberExtractor`, `KeywordExtractor`,
   `DocNestPipeline` — make tests pass; reuse the §id/regex logic faithfully from the Python source.
5. **Phase 5 (Verify):** full suite (Slices 1–3) green; the Slice-2 round-trip still holds after
   normalisation; no §id/token drift.
6. **Phase 6:** any defect → regression + unit test then fix.
7. **Phase 7:** branch `slice/03-pipeline` → green → merge `main`; CHANGELOG; tag `v0.0.3-pipeline`
   (no NuGet publish — still Slice 7 / M4).

**Risk/impact expectation:** Low/Low — additive, pure-CPU, no external deps; the watch-item is
Python-parity of §ids + extractor regex, covered by case-table tests before any code.

## Decisions needed from owner at GATE 0
- Approve the four Slice-3 Phase 0 documents and step order, **or** adjust.
- Optional steer on Dev Q1 (pipeline surface) and Q3 (extractor interfaces vs static helpers) — can
  also defer to ADR-0003 in Phase 2.
