# ADR-0010 — CLI, NuGet packaging, and the deferred publish

- **Status:** Accepted (owner approved straight-through to GATE 5)
- **Date:** 2026-06-10
- **Context slice:** Slice 7 — CLI + packaging

## Context
The DocNest .NET engine is complete (Slices 1–6b). It needs a CLI for end users and NuGet packages for
developers — the path to a first public release. Publishing is outward-facing and irreversible.

## Decision
1. **`DocNest.Cli`** is a `dotnet tool` (`docnest`) with `convert`/`query`/`info`, built on
   **`System.CommandLine`** (referenced only by the CLI). The command **logic lives in testable static
   handlers** (`CliCommands`) that take plain params + `TextWriter` and return an exit code — System.CommandLine
   stays in `Program.cs` only. The CLI **composes** existing slices and adds no library dependency.
2. **Per-assembly NuGet packages** (`DocNest.Abstractions/Core/Storage/Parsers/Retrieval/Embeddings/Query`)
   so consumers pull only the dependencies they need (e.g. Core + Retrieval without ONNX/OpenXML/PdfPig).
   Shared metadata in `Directory.Build.props`; `GenerateDocumentationFile=true` with `NoWarn=CS1591`;
   `IncludeSymbols`/snupkg. First version **`0.1.0`**.
3. **Publishing to nuget.org is OUT OF SCOPE for this slice.** It runs `dotnet pack` to produce `.nupkg`
   artifacts and a release runbook only. The actual `dotnet nuget push … --api-key …` is a **separate,
   explicit, owner-initiated** action with the owner's key — never performed by the agent.

## Consequences
**Positive:** a usable tool + clean per-package consumption; the CLI is unit-testable without
System.CommandLine; the irreversible publish step is gated to a deliberate owner action (Secure / release discipline).
**Negative / cost:** `System.CommandLine` is still pre-release (beta) — pinned to the stable `SetHandler` API
(`2.0.0-beta4.22272.1`); many small packages to version together (one central `<Version>`).
**Neutral:** `view`/`library`/`stats` commands and embeddings-in-CLI are deferred.

## Alternatives considered
- *Spectre.Console.Cli:* nicer output, heavier dep; not needed for three commands. Rejected (owner chose System.CommandLine).
- *Hand-rolled arg parsing:* zero-dep but reinvents help/parsing/validation. Rejected.
- *One fat `DocNest` package:* forces AngleSharp/OpenXML/PdfPig/OnnxRuntime/SQLite on every consumer. Rejected → per-assembly.
- *Publish in this slice:* outward-facing + irreversible; must be an explicit owner action. Rejected.
