# Phase 0.4 — Roadmap: Slice 7 (CLI + NuGet packaging)

> Consolidates the Slice-7 BA/Dev/QA. The CLI **composes** existing slices (little new logic); packaging
> is mostly csproj metadata. **Publishing to nuget.org is explicitly out of scope** (pack only; the owner
> pushes separately with their key). Completing Slice 7 = a usable tool + packable libraries → first release.

## Ordered steps (what GATE 0 approves entering Phase 1 for)
1. **Phase 1 (Impact & risk):** new `DocNest.Cli` (+ one CLI parser dep) + shared package metadata in
   `Directory.Build.props` (guarded to `src/**`). Watch-items: CLI dep not leaking into library packages;
   test projects staying non-packable; **no accidental publish**. Risk Low.
2. **Phase 2 (Design + ADR-0010):** resolve Q1–Q4 (CLI parser; per-assembly packages; publish gated; version
   `0.1.0`); the command wiring; the packaging metadata; the release runbook (manual push). File-by-file plan.
3. **Phase 3 (Test-first):** I1–I6 (command handlers on a fixture; provider factory) + P1–P2 (pack metadata),
   failing first.
4. **Phase 4 (Implement):** `Program` + `ConvertCommand`/`QueryCommand`/`InfoCommand`/`LlmProviderFactory`;
   package metadata; `PackAsTool` for the CLI.
5. **Phase 5 (Verify):** full suite green; `dotnet pack -c Release` produces `.nupkg` for every library +
   the tool with correct metadata; `docnest --help`/`convert`/`query`/`info` work on the fixture.
6. **Phase 6:** defects → regression + unit tests then fix.
7. **Phase 7 (git + release):** branch `slice/07-cli-packaging` → green → merge `main`; bump `<Version>` to
   `0.1.0` + CHANGELOG; tag `v0.1.0`. **Publishing the packages to nuget.org is a separate, explicit,
   owner-initiated action afterward** (this slice stops at producing artifacts + a runbook).

**Risk/impact expectation:** Low — additive, the CLI composes existing tested pieces; packaging is metadata;
the only outward-facing action (publish) is deliberately deferred to an explicit owner step.

## Decisions needed from owner at GATE 0
- Approve the four Phase 0 documents, **and** confirm Q3: **packaging only this slice; nuget.org publish is a
  separate owner-triggered step** (with your API key).
- Q1 (CLI parser = System.CommandLine?) and Q2 (per-assembly packages + meta-package?) — or defer to ADR-0010.
