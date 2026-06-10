# Phase 0.1 — BA / Functional Document
## Slice 7: CLI + NuGet packaging

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan · **Depends on:** Slices 1–6b (✅)

---

### WHY
The engine is complete but only usable from code. This slice makes DocNest **consumable**: a `dotnet`
CLI tool for end users and **NuGet packages** for developers — the path to a first public release. It
composes the existing pieces (parse → normalise → `.udf`; `.udf` → retrieve → answer) into commands.

### WHAT — exact functional behaviour
1. **`DocNest.Cli`** (a `dotnet tool`, command `docnest`):
   - **`convert <source> [-o out.udf] [--fast] [--quantization …] [--owner/--tags/…]`** — parse a
     supported file (`md/html/csv/docx/xlsx/pdf`) → pipeline → `.udf`. With an embedder configured (a
     local MiniLM model path), embeddings are written; `--fast` skips them.
   - **`query <udf|file> <question> [--allow-llm] [--provider/--model/--base-url]`** — load/parse →
     retrieve → answer via the 5-layer engine; print the answer, the layer used, token cost, and
     citations. Deterministic layers work with no LLM; LLM layers use a provider from flags/env
     (`DOCNEST_LLM_API_KEY`/`_BASE_URL`/`_MODEL`).
   - **`info <udf>`** — print the catalogue summary (title, sections, key numbers, metadata).
2. **NuGet packaging** — each library assembly is packable with full metadata (id, version, authors,
   license = MIT, repository URL, README, tags); the CLI packs as a `dotnet tool`. `dotnet pack`
   produces `.nupkg`/`.snupkg` artifacts.

**Before vs after**
- *Before:* DocNest is only a set of class libraries used from code.
- *After:* `dotnet tool install -g DocNest.Cli` → `docnest convert report.pdf` → `docnest query report.udf "…"`;
  and `dotnet add package DocNest.Core` (etc.) for developers.

**Acceptance criteria**
- AC1: `convert` on each supported format produces a valid `.udf` that `info`/`query` can read.
- AC2: `query` returns a deterministic Layer-0/1 answer with **no** LLM configured; with a (stubbed/real)
  provider it escalates; output shows answer + layer + tokens + citations.
- AC3: `info` prints the catalogue summary for a `.udf`.
- AC4: `dotnet pack` succeeds for every library + the tool, with correct package metadata; the CLI
  installs and runs as a global tool (verified locally).
- AC5: missing file / unsupported format / bad `.udf` → a clear non-zero-exit error message (no stack dump).

### Non-goals (this slice)
- **No publish to nuget.org** — packaging only; the actual `dotnet nuget push` is a **separate, explicit,
  owner-triggered step** (needs the owner's API key; outward-facing and irreversible).
- No `view` (HTML viewer) or `library` (multi-doc) commands (later).
- No interactive/REPL mode.

### HOW — scenarios
- `docnest convert notes.md -o notes.udf` → "Wrote notes.udf (N sections)".
- `docnest query notes.udf "what is the uptime?"` → "Uptime: 99.9% … [Layer 0, 0 tokens]".
- `docnest info notes.udf` → catalogue table.
- *Edge:* `docnest query missing.udf "x"` → "Error: file not found" + exit 1.

### Traceability
Serves the Charter audience (developers + the `knovex` app) and the path to release. The publish step is
deliberately gated (Secure / deliberate outward action), consistent with the protocol's release discipline.
