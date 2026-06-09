# DocNest .NET — project instructions

## ⛔ STOP — read the Development Protocol before ANY change

**Before writing or editing even one line of code, read and follow
[docs/DEVELOPMENT_PROTOCOL.md](docs/DEVELOPMENT_PROTOCOL.md).** It is mandatory and gated, and
inherits the rules of the Python reference (`D:\Learning\docnest\DEVELOPMENT_PROTOCOL.md`).
Read it alongside the **[Charter / North Star](docs/CHARTER.md)** and the **[ADRs](docs/adr/)**.

**Before any task starts:** it must pass the **Definition of Ready** and the owner (Gunjan)
must give an explicit **go**. **The owner signs off every phase gate** — nothing advances
without it. Every change is checked against the NFR budgets (bounded memory, ~1 ms warm query,
local-first core path, deps centrally pinned + wrapped) and must not regress the Charter
Success Metrics — above all the **`.udf` cross-ecosystem compatibility** KPI.

Quick gist (full rules + gates in the protocol doc):
0. **Understand first (Phase 0)** — BA + Dev + QA documents, then a roadmap. Nothing proceeds until all 4 exist.
1. **Plan code before writing** — what code, where, why.
2. **Impact & risk map** — proceed only if Risk=Low & Impact=Low; never break the `.udf` schema / `UDF_VERSION` / public API.
3. **DSA + architecture pass** — state complexity; justify SOLID + pattern; every external module behind a wrapper.
4. **Test first** — unit + integration + functional + e2e (incl. Python `.udf` round-trip); fail first, then pass.
5. **Run the FULL suite every cycle** — regression-first; the regression suite only grows.
6. **Defects** — found-in-dev → regression + unit test then fix; escaped → also root-cause analysis.
7. **Git** — never push to `main`; temp branch → all green → merge → bump version + CHANGELOG → NuGet.
8. Only **all-green** earns the ✅ green mark.

**Mission:** an idiomatic .NET DocNest that reaches `.udf` parity with the Python engine,
slice by slice, without ever breaking the cross-ecosystem `.udf` contract.

## Repo facts
- Python reference being ported: `D:\Learning\docnest` (`docnest-ai` on PyPI, v0.7.0).
- Target: NuGet package `DocNest` (id TBC — Dev Q1). Motto: Secure · Fast · Reliable · Cost-Effective.
- TFM: `net8.0` (LTS). Tests: xUnit. Central package pinning via `Directory.Packages.props`.
