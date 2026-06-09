# DocNest .NET — Development Protocol (MANDATORY)

> **READ THIS FIRST, BEFORE ANY CHANGE — even a one-word change.**
> This protocol is inherited verbatim in spirit from the Python reference
> (`D:\Learning\docnest\DEVELOPMENT_PROTOCOL.md`) and adapted only where the
> runtime differs (PyPI → NuGet, pytest → xUnit, pyproject → csproj). The gates,
> golden rules, and owner sign-off requirement are **identical and non-negotiable**.

## The Golden Rules (non-negotiable)
1. **Understand the requirement first** — three lenses (BA, Dev, QA), each its own document.
2. **Understand the code before you touch it** — including the Python reference being ported.
3. **Plan the code before writing it** — what code, where, and why.
4. **Tests come before the fix/feature** (test-first; tests fail first for the right reason).
5. **Run the FULL suite every cycle** (regression-first; never break what worked).
6. **Never push to `main` directly** — temp branch → prove green → merge → release.
7. **Every external module gets a wrapper** (no third-party calls scattered in core).
8. **Low risk + low impact + all green** is the only path to a "✅ green mark".
9. **The owner signs off every gate** — no phase advances without explicit approval.
10. **No task starts until it is Ready** (Definition of Ready).

## Definition of Ready (DoR) — gate BEFORE Phase 0
- [ ] Clear goal in one sentence, traceable to the [Charter](CHARTER.md).
- [ ] Owner assigned + priority agreed.
- [ ] Acceptance criteria drafted.
- [ ] Owner has given the explicit **go**.

## Non-Functional Requirements & Dependency Policy (.NET technical bar)
- **Memory:** bounded — streaming parse; peak RAM scales with chunk size, not file size.
- **Latency:** warm retrieval ~1 ms/query; ingestion predictable.
- **Privacy / local-first:** core path (parse → normalise → embed) runs with no mandatory
  network calls — local ONNX model. Cloud providers opt-in only.
- **Dependencies:** minimal + justified; **centrally pinned** in `Directory.Packages.props`
  (Central Package Management); prefer a BCL / zero-dep fallback; **every external module
  sits behind a DocNest wrapper interface.**
- **Compatibility:** never silently break the `.udf` archive layout, `UDF_VERSION`, the JSON
  schema, or the public NuGet API. Breaking changes require an ADR + version bump + migration note.

## Phases & Gates (each gate is owner-signed)
- **Phase 0 — Requirements understanding:** BA doc + Dev doc + QA doc + final roadmap.
  **GATE 0:** all four written and agreed.
- **Phase 1 — Impact & risk:** blast-radius map; proceed only if Risk=Low & Impact=Low;
  backward-compat (`.udf`, `UDF_VERSION`, public API) plan stated. **GATE 1.**
- **Phase 2 — Design (DSA + architecture):** complexity stated; SOLID + design pattern
  justified; wrapper boundaries defined; file-by-file code plan. Significant decisions →
  an [ADR](adr/). **GATE 2.**
- **Phase 3 — Test first:** unit + integration + functional + e2e (incl. cross-ecosystem
  `.udf` round-trip). Tests fail first for the right reason. **GATE 3.**
- **Phase 4 — Implement:** code exactly per the Phase 2 plan, behind wrappers. **GATE 4:**
  `dotnet build` succeeds; matches the plan.
- **Phase 5 — Verify:** run the **entire** suite every cycle; Charter Success Metrics held
  (no compatibility/accuracy/latency/memory regression). **GATE 5 → ✅ green mark.**
- **Phase 6 — Defect protocol:** found-in-dev → add regression + unit test, then fix;
  escaped → also root-cause analysis + the missing test layer.
- **Phase 7 — Git & release:** temp branch → all green → merge to `main` → bump version +
  `CHANGELOG.md` → publish NuGet package.

## Per-change checklist (paste into every task / PR)
- [ ] Phase 0.1: BA / functional document written
- [ ] Phase 0.2: Dev / technical document written (Python reference read end-to-end, files listed)
- [ ] Phase 0.3: QA / user document written (scenarios, edge/negative, regression view)
- [ ] Phase 0.4: Final roadmap written
- [ ] Phase 1: Impact map; Risk=Low, Impact=Low; backward-compat plan
- [ ] Phase 2: DSA complexity; SOLID/pattern justified; wrappers defined; code plan; ADR if significant
- [ ] Phase 3: Unit + integration + functional + e2e tests written and failing first
- [ ] Phase 4: Code implemented per plan; `dotnet build` clean
- [ ] Phase 5: FULL suite run this cycle — all green; metrics held
- [ ] Phase 6: Any bug → regression + unit tests added (+ RCA if escaped)
- [ ] Phase 7: Temp branch → merged to main on green → version + CHANGELOG → NuGet

## Definition of Done
Done only when: the three understanding documents + roadmap exist, full suite green,
regression suite extended, design/impact notes + ADRs recorded, merged from a temp branch
to `main`, and (if releasing) version bumped + CHANGELOG updated + NuGet published.
