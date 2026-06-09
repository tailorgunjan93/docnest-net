# DocNest .NET — Roadmap (Phase 0.4)

> Consolidates the BA/Dev/QA understanding into a sequenced plan. The program is a series of
> gated vertical slices; each slice runs the full Phase 1→7 cycle with owner sign-off.
> **Slice 1 (Core domain + contracts): ✅ shipped to `main` (32/32 green).**
> **Currently active: Slice 2 (`.udf` read/write), at GATE 0 — see [phase0/SLICE-02-ROADMAP.md](phase0/SLICE-02-ROADMAP.md).**

## Program slices (ordered, with dependencies)

| # | Slice | Depends on | Headline deliverable | Key external deps (behind wrappers) |
|---|---|---|---|---|
| 1 | **Core domain + contracts** | — | Records, 5 interfaces, exceptions, JSON context | none (BCL only) |
| 2 | `.udf` read/write | 1 | `ZipStorageBackend`, `UdfReader`/`UdfWriter`, manifest/catalogue/content; **cross-ecosystem round-trip** | `System.IO.Compression` |
| 3 | Pipeline + normaliser | 1,2 | `SectionNormaliser` (§id/hierarchy/token/table-norm), deterministic key-numbers + keywords, `DocNestPipeline` | none |
| 4 | Parsers (native .NET) | 1,3 | `IParser` impls: md, html, csv, docx, xlsx, pdf + `ParserFactory` | OpenXML SDK, a PDF lib (Phase-2 pick) |
| 5 | Retrieval | 1,2 | FTS5 + dense ANN + RRF fusion (+ optional ONNX rerank) | `Microsoft.Data.Sqlite` |
| 6 | Embeddings + intelligence | 1,3 | ONNX MiniLM `IEmbedder`, quantizer, LLM providers, section/doc enrichment | ONNX Runtime |
| 7 | CLI + packaging | all | `dotnet tool`, NuGet release, parity eval vs Python | `System.CommandLine` |

**Milestones**
- M1 = Slices 1–3 green → a `.udf` can be written/read in .NET and round-trips with Python (no parsing yet, synthetic docs).
- M2 = Slices 1–4 green → real `.docx`/`.pdf` → `.udf` openable in Python `docnest`.
- M3 = Slices 1–6 green → retrieval + embeddings; accuracy parity measured vs Python on shared suite.
- M4 = Slice 7 → first public NuGet `0.1.0`.

**Cross-cutting guardrails (every slice):** `.udf` JSON schema fidelity, bounded memory, ~1 ms
warm query, local-first core path, central package pinning, wrapper-only external access.

---

## Slice 1 — ordered steps (this is what GATE 0 approves entering Phase 1 for)

1. **Phase 1 (Impact & risk):** confirm greenfield ⇒ Risk/Impact = Low; the only live contract
   is the `.udf` JSON schema → mitigation = golden Python fixture tests.
2. **Phase 2 (Design + ADRs):** resolve Dev open questions Q1–Q4; write **ADR-0001** (domain as
   records + wrapper interfaces; one vs two assemblies; JSON strategy). File-by-file code plan.
3. **Phase 3 (Test-first):** scaffold solution + test project; capture the **golden
   `catalogue.json` fixture from Python**; write U1–U6, I1–I4, F1–F2 — all failing first.
4. **Phase 4 (Implement):** records, `DocNestJson` source-gen context, exceptions, 5 interfaces,
   `DocId.FromPath`, `UDF_VERSION` constant — make the tests pass.
5. **Phase 5 (Verify):** full xUnit suite green; key-parity + Python round-trip hold ⇒ ✅.
6. **Phase 6:** any defect → regression + unit test then fix.
7. **Phase 7:** temp branch `slice/01-core-domain` → green → merge `main`; no NuGet publish yet
   (first publish is Slice 7 / M4), but tag `v0.0.1-core`.

**Risk/impact expectation per step:** all Low — no external dependencies, no existing consumers;
the single watch-item is JSON key fidelity, covered by tests before any code.

## Decisions needed from owner at GATE 0
- Approve the four Phase 0 documents (BA/Dev/QA/roadmap) and the slice order above, **or** adjust.
- Direction on Dev Q1 (package id) and Q2 (one vs two assemblies) — can also defer to Phase 2/ADR-0001.
