# Phase 1 + Phase 2 — Impact/Risk & Design
## Slice 7: CLI + NuGet packaging

**Status:** Phases 1–2 (owner approved straight-through to GATE 5) · **Owner:** Gunjan
**ADR:** [ADR-0010](../adr/0010-cli-and-packaging.md) · **Decisions:** System.CommandLine; per-assembly packages; **no publish**.

---

## Phase 1 — Impact & Risk
**Blast radius:** new `DocNest.Cli` (+ `System.CommandLine`, CLI-only) + shared package metadata in
`Directory.Build.props`. No library code changes (the CLI only composes). No `.udf` change.

**Watch-items / mitigations:**
1. **No accidental publish** — this slice runs `dotnet pack` only; `nuget push` is a separate owner step.
   Mitigation: no push command anywhere; documented runbook.
2. **CLI dep not leaking into libraries** — `System.CommandLine` is referenced only by `DocNest.Cli`.
3. **XML-doc generation vs `TreatWarningsAsErrors`** — enabling `GenerateDocumentationFile` could surface
   CS1591 on undocumented members. Mitigation: `NoWarn=CS1591` (ship the docs that exist; don't fail).
4. **Test projects must stay non-packable** — already `IsPackable=false`.
**Risk = Low.** The only outward action (publish) is deferred to an explicit owner step.

## Phase 2 — Design
**SOLID / patterns:** CLI logic lives in **testable static handlers** (`CliCommands.ConvertAsync/QueryAsync/InfoAsync`)
that take plain params + `TextWriter` and return an exit code — **no `System.CommandLine` in the handlers**.
`Program.cs` builds the command tree and delegates. `LlmProviderFactory` (Strategy) builds an `ILlmProvider`
from flags/env. The CLI composes Slices 2/3/4/5/6b; zero new library deps.

**Command wiring:**
- `convert` → `ParserFactory.Get(file).ParseAsync` → `DocNestPipeline.Process` → `UdfWriter.WriteAsync` (no
  embeddings in the CLI MVP → no ONNX model needed; `--fast` is the default behaviour).
- `query` → (`.udf` → `UdfReader.LoadAsync().ToDocument()` | file → parse+pipeline) → `HybridRetriever`
  (no embedder = BM25 + structural graph) + `DocNestQueryEngine.AnswerAsync` (LLM via `LlmProviderFactory`).
- `info` → `UdfReader.LoadAsync` → print manifest/catalogue fields.
- Errors → `DocNestException` → clean message + exit 1 (no stack dump).

**Packaging (Directory.Build.props, all `src/**`):** `<Version>0.1.0</Version>`, `Authors`, `Company`,
`Product=DocNest`, `Description` (default + per-project), `PackageProjectUrl`, `RepositoryUrl`,
`RepositoryType=git`, `PackageLicenseExpression=MIT`, `PackageReadmeFile=README.md` (packed `None`),
`PackageTags`, `GenerateDocumentationFile=true` + `NoWarn=CS1591`, `IncludeSymbols`,
`SymbolPackageFormat=snupkg`. Per-assembly packages (consumers take only needed deps). CLI: `PackAsTool=true`,
`ToolCommandName=docnest`, `PackageId=DocNest.Cli`.

**Code plan (signatures):**
- `DocNest.Cli/CliCommands.cs` — `static Task<int> ConvertAsync(string source, string? output, bool fast, string quantization, TextWriter outw, TextWriter errw, CancellationToken ct)`;
  `QueryAsync(string path, string question, bool allowLlm, ILlmProvider? llm, string cacheDir, TextWriter outw, TextWriter errw, CancellationToken ct)`;
  `InfoAsync(string udf, TextWriter outw, TextWriter errw, CancellationToken ct)`.
- `DocNest.Cli/LlmProviderFactory.cs` — `static ILlmProvider? Create(string? provider, string? model, string? apiKey, string? baseUrl)`.
- `DocNest.Cli/Program.cs` — `System.CommandLine` tree (`convert`/`query`/`info`) delegating to `CliCommands`.

**Resolved open questions:** Q1 → `System.CommandLine`; Q2 → per-assembly packages (+ optional meta-package later);
Q3 → **publish out of scope** (pack only); Q4 → version `0.1.0`.
