# Phase 0.2 — Dev / Technical Document
## Slice 7: CLI + NuGet packaging

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### 1. Python reference
`cli.py` (Typer + Rich): `convert`, `query`, `inspect`, `stats`, `view`, `library`. We port the core
three (`convert`, `query`, `info`); `view`/`library`/`stats` are deferred.

### 2. CLI wiring (composes existing slices — little new logic)
- **`convert`** = `new ParserFactory().Get(path).ParseAsync(path)` → `new DocNestPipeline().Process(raw)`
  → (optional `OnnxEmbedder` → `EmbeddingBlock`) → `new UdfWriter().WriteAsync(doc, out, …, embeddings)`.
  Folder input → iterate supported files (a simple loop; full library mode deferred).
- **`query`** = if `.udf`: `UdfReader.LoadAsync(path)` → `ToDocument()`; else parse+pipeline as above.
  Then `new HybridRetriever(cacheDir, embedder?)` + `new DocNestQueryEngine(retriever, llm?)`;
  `AnswerAsync(doc, question, allowLlm)`. Provider built from flags/env via a small `LlmProviderFactory`
  (openai-compatible / anthropic). Print `QueryResult` (answer, layer, tokens, citations).
- **`info`** = `UdfReader.LoadAsync(path)` → print `Manifest`/`Catalogue` fields.

### 3. CLI library decision (Q1)
- **Recommend `System.CommandLine`** (Microsoft, the standard .NET CLI parser) for sub-commands +
  options + help. Alternatives: `Spectre.Console.Cli` (nicer output, heavier), or a hand-rolled parser
  (zero dep, but reinvents help/parsing). One CLI-only dependency, not in any library package.

### 4. NuGet packaging
- **Per-assembly packages** (Q2 recommend): `DocNest.Abstractions`, `DocNest.Core`, `DocNest.Storage`,
  `DocNest.Parsers`, `DocNest.Retrieval`, `DocNest.Embeddings`, `DocNest.Query` — so a consumer takes only
  the deps they need (e.g. Core + Retrieval without ONNX). Optionally a `DocNest` meta-package referencing
  the common set. (A single fat package would force AngleSharp/OpenXML/PdfPig/OnnxRuntime/SQLite on everyone.)
- **Shared metadata** in `Directory.Build.props` for `src/**`: `Authors`, `Company`, `Product`,
  `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType=git`, `PackageLicenseExpression=MIT`,
  `PackageReadmeFile=README.md`, `PackageTags`, `Description` (per-project override), `IncludeSymbols`,
  `SymbolPackageFormat=snupkg`, a single `<Version>` property, `GenerateDocumentationFile=true` (XML docs).
  Mark test projects `<IsPackable>false</IsPackable>` (already set).
- **CLI tool:** `DocNest.Cli.csproj` `PackAsTool=true`, `ToolCommandName=docnest`, `PackageId=DocNest.Cli`.
- **Versioning:** central `<Version>0.1.0</Version>` (first public minor). Bump + CHANGELOG before any publish.

### 5. Publishing (Q3 — gated)
**This slice does NOT publish.** It only runs `dotnet pack` to produce `.nupkg`. The actual
`dotnet nuget push … --api-key …` to nuget.org is a **separate, explicit, owner-initiated** action
(outward-facing, irreversible; needs the owner's key). Documented as a release runbook, not executed here.

### 6. Layout
```
src/DocNest.Cli/Program.cs (+ Commands/ConvertCommand.cs, QueryCommand.cs, InfoCommand.cs, LlmProviderFactory.cs)
Directory.Build.props        # add shared package metadata (guarded to src/**, not tests)
tests/DocNest.Cli.Tests/…    # invoke command handlers on a fixture; assert outputs / exit codes
```

### 7. Backward-compat surface
- Additive: new `DocNest.Cli` + packaging metadata. No library code changes (CLI only composes). The
  public NuGet **API** of each library becomes a compatibility surface from the first publish onward.

### 8. Open questions (resolve GATE 0 / Phase 2)
- Q1: CLI library — **System.CommandLine** (recommend) vs Spectre.Console.Cli vs hand-rolled.
- Q2: packaging — **per-assembly packages** (+ optional `DocNest` meta-package) (recommend) vs one fat package.
- Q3: confirm publishing is **out of scope** this slice (pack only; owner pushes separately). (Recommend.)
- Q4: first version — `0.1.0`? (Recommend, matching "first usable public release".)
