# Phase 0.3 — QA / User Document
## Slice 7: CLI + NuGet packaging

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### What "working" means
A user can install the `docnest` tool and convert/query/inspect documents from the terminal, and a
developer can `dotnet add package DocNest.*` and use the libraries. Packaging produces valid `.nupkg`
artifacts; publishing is a deliberate, separate owner action.

### Test plan (test-first — written in Phase 3, failing first)

**Integration — CLI command handlers (invoked in-process, temp files)**
- I1 (`convert`): `convert sample.md -o out.udf` exits 0 and writes a `.udf` that `UdfReader` can open;
  prints a "wrote … (N sections)" line.
- I2 (`query`, deterministic): on a `.udf` whose doc has a key number, `query out.udf "what is the uptime?"`
  prints the Layer-0 answer (layer 0, 0 tokens) with **no** LLM configured; exit 0.
- I3 (`query`, LLM): with a **fake/stubbed** provider wired via the factory, a non-deterministic question
  escalates and prints an LLM answer with the layer + token count.
- I4 (`info`): prints the catalogue title + section count for a `.udf`.
- I5 (errors): a missing file → exit ≠ 0 + a clear message (no stack dump); an unsupported format → clear error.

**Unit — `LlmProviderFactory`**
- I6: `--provider openai --model m --base-url u` (or env vars) → an `OpenAiCompatibleLlmProvider`;
  `--provider anthropic …` → an `AnthropicLlmProvider`; none → null (deterministic-only).

**Packaging**
- P1: `dotnet pack -c Release` produces a `.nupkg` for each `src/**` library and the CLI tool; test
  projects are not packed.
- P2: each package has the required metadata (id, version, authors, license expression, repository url,
  README) — assert by reading the produced `.nuspec`/package, or a `dotnet pack` that fails on missing metadata.
- P3 (local install smoke, manual/CI): `dotnet tool install --global --add-source <dir> DocNest.Cli` then
  `docnest --help` works. (Documented; may be a manual/skippable step.)

### Fixtures
A small committed `sample.md` (reuse the Slice-4 fixture) for `convert`/`query`/`info`. A fake/stub
`ILlmProvider` for I3. Temp output dirs cleaned up.

### Edge / negative cases
- `convert` a folder with mixed supported/unsupported files → converts the supported ones, reports skips.
- `query` a raw file (not `.udf`) → parses on the fly and answers.
- `--allow-llm` with no provider configured → deterministic-only, clear note that no LLM ran.
- Non-ASCII in answers/output renders correctly (UTF-8 console).
- A corrupt `.udf` → `UdfReadException` surfaced as a clean CLI error + non-zero exit.

### What constitutes a regression (regression-suite seeds)
- `convert`/`query`/`info` exit codes or core output changing unexpectedly.
- A test project becoming packable, or a library shipping without required metadata.
- The CLI taking a hard dependency that leaks into a library package.
- **Any accidental network publish** — publishing must remain a separate, explicit, owner-only action.
