# SLICE-10 — Roadmap

Ordered steps, dependencies, gates. Owner signs off every gate. Eval-harness + test only; **no merge to
`main`, no NuGet publish** (owner-triggered).

1. **GATE 0 — Phase 0 understanding** (this set: BA / DEV / QA / IMPACT-DESIGN / ROADMAP). Confirm scope
   (dense embeddings into the eval retrieval path; Quantizer/`.udf` and CLI out of scope) + the
   DI/graceful-degrade design + the SLICE-10 label (SLICE-09 is the LLM-judge follow-up).
2. **ADR-0012** — record "activate the eval's dense retrieval path by injecting the existing
   `OnnxEmbedder` (opt-in provisioning); keep the Quantizer/`.udf` path out of retrieval." Applies
   ADR-0007 + ADR-0008. Depends on GATE 0.
3. **GATE 1/2 — impact + design** — recorded in [SLICE-10-IMPACT-DESIGN.md](SLICE-10-IMPACT-DESIGN.md):
   Risk=Low, Impact=Low; no shipped surface touched; DI at the composition root behind existing wrappers.
4. **Phase 3 — tests first (fail)** — add `tests/DocNest.Retrieval.Tests` → `DocNest.Embeddings`
   reference + `OnnxDenseRetrievalTests` (`[SkippableFact]`). Provision the model; run; confirm the test
   first **fails** for the right reason (no production wiring / asserting the BM25-only miss), then is
   made to pass by the real dense path.
5. **Phase 4 — implement** — add the `DocNest.Embeddings` reference to the eval csproj; add
   `TryBuildEmbedder` + inject the shared `OnnxEmbedder` into every `HybridRetriever` in `Program.cs`;
   report retrieval mode. `dotnet build` clean.
6. **Phase 5 — full suite + eval** — entire xUnit suite green (154 + the new test). Re-run the eval with
   gpt-oss-120b (Groq key in `D:\Learning\docnest\.env`) **and** confirm the BM25-only fallback still
   runs; record before/after in `eval/results/` and confirm AC1–AC4.
7. **Phase 6 — defects / RCA** — any escaped issue → regression test + root-cause note.
8. **GATE — green** — only all-green earns ✅.
9. **Phase 7 — git (owner-triggered)** — temp branch `slice-10-eval-dense-embeddings` → all green →
   **stop**. Owner decides merge to `main`. No version bump / NuGet publish (eval is not shipped).

## Risk / impact
Risk **Low**, Impact **Low** — opt-in harness wiring; default (model absent) path byte-for-byte
unchanged; shipped library / public API / `.udf` / NuGet untouched. Mitigated by: graceful degrade to
BM25-only, embedder behind existing wrappers, regression-first full suite, eval gate (AC3).

## Outcome / RCA (Phase 5–6)
_To be filled after implementation + the eval re-run._
