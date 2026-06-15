# Changelog

All notable changes to **DocNest .NET** are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/); the project adopts SemVer at its first NuGet release.

## [Unreleased]

### Added — Slice 9: optional LLM-as-judge for the accuracy eval (eval-harness only)
- **`eval/DocNest.Eval`** gains an optional **LLM-as-judge** that grades each answer 0–10 with the same
  prompt, rubric, and robust `SCORE`/`REASONING` parse as the Python reference eval's `_judge` — so the
  .NET and Python accuracy numbers become directly comparable. Gated on `DOCNEST_JUDGE_API_KEY`
  (+ `DOCNEST_JUDGE_MODEL` / `DOCNEST_JUDGE_BASE_URL`, defaulting to Groq `openai/gpt-oss-120b`); reuses
  the existing `OpenAiCompatibleLlmProvider` + rate-limit-resilient `RetryingLlmProvider`.
- **`LocalJudge` stays the zero-cost default** — with no judge key set, the eval behaves byte-for-byte as
  before (local number/keyword/phrase heuristic, 0 judge tokens). The judge LLM is independent of the
  answer-generation LLM (`DOCNEST_LLM_API_KEY`).
- Design: `IAnswerJudge` Strategy (`LocalAnswerJudge` / `LlmAnswerJudge`) selected by `JudgeFactory`; the
  external LLM stays behind `ILlmProvider`. **No change** to the shipped library, public API, `.udf`
  contract, or NuGet packages (eval is `IsPackable=false`).
- Tests: +19 (`ParseScore` variants/defaults, `LlmAnswerJudge` over a scripted provider, `JudgeFactory`
  gating, and a locked `LocalAnswerJudge`↔`LocalJudge` regression). Full suite green (173 pass).
  See `docs/phase0/SLICE-09-*`.

## [0.1.1] - 2026-06-10

### Fixed — Slice 8: query-engine accuracy parity (escalation + key-number misfire)
- **Escalation confidence (ADR-0011).** `DocNestQueryEngine` previously returned the first extractive
  snippet **unconditionally**, so ~80/88 eval questions were answered at 0 tokens and never reached the
  LLM. It now gates Layer 1 on an absolute, bounded **query-term-recall confidence** over the top section
  (RRF still does ranking — its rank-fusion score is not an absolute confidence). Below the threshold the
  engine escalates to the LLM layers; a Layer-2 "Not found" now falls through to multi-section synthesis
  instead of dead-ending.
- **Key-number noise gate.** `KeyNumberExtractor` no longer emits bare name/version or structural-reference
  counts (`"Llama 2"`, `"Figure 4"`, `"Section 23"`) that were misfiring as confident Layer-0 answers on PDFs.
- **Accuracy** on the Python-parity eval (same 10 files / 88 questions, gpt-oss-120b): overall **5.1 → 6.7/10**,
  Phase-1 hit-rate 53% → 72%, Phase-2 (PDF) hit-rate 20% → 47%. Public API and `.udf` contract unchanged.
- **Eval harness** (`eval/DocNest.Eval`) now runs the exact Python reference documents + question sets
  (`eval/cases.json`), with a rate-limit-resilient LLM provider. Tests: +6 (escalation gate, Layer-2→3
  fallback, key-number misfire). Full suite green (153 pass).
- Note: closing the remaining gap to the Python 8.5 needs follow-up slices — dense embeddings in retrieval
  (the .NET eval pipeline is currently BM25-only) and an LLM-as-judge option in the eval.

## [0.1.0] - 2026-06-10

First public release of **DocNest for .NET** — an idiomatic port of the Python `docnest` engine. The
complete RAG loop: **ingest (md/html/csv/docx/xlsx/pdf) → `.udf` → retrieve → answer**, plus a `docnest`
CLI and per-assembly NuGet packages. `.udf` files are cross-compatible with the Python implementation.

### Added — Slice 7: CLI + NuGet packaging
- **`DocNest.Cli`** — a `dotnet tool` (`docnest`) with `convert` (document → `.udf`), `query` (`.udf`/file +
  question → 5-layer answer), and `info` (catalogue summary). Logic in testable handlers; `System.CommandLine`
  confined to `Program.cs`. Composes the engine with no new library dependencies.
- **Per-assembly NuGet packages** with shared metadata (MIT, repository URL, README, symbols/snupkg, XML docs):
  `DocNest.Abstractions/Core/Storage/Parsers/Retrieval/Embeddings/Query` + the `DocNest.Cli` tool, at `0.1.0`.
- **Tests** — +3 (CLI `convert→info→query` round-trip, error exit codes, provider factory). ADR-0010.
- Publishing to nuget.org is a separate, explicit owner step (see the release runbook).

### Added — Slice 6b: LLM providers + 5-layer answer engine (RAG loop complete)
- **`DocNest.Query`** (new assembly) — `DocNestQueryEngine`: Layer 0 deterministic (key-number /
  summary / insight) → Layer 1 extractive (0 tokens) → Layers 2–4 LLM (single / multi / full doc), using
  the Slice-5 `IRetriever` + `ILlmProvider`; `KeyNumberMatcher` + `Extractive` (ported). `QueryResult` in Abstractions.
- **LLM providers** — `OpenAiCompatibleLlmProvider` (`/chat/completions`; OpenAI/Groq/Together/OpenRouter/
  local) and `AnthropicLlmProvider` (`/v1/messages`). HTTP via the BCL — **zero new package dependencies**.
- **Tests** — +12 (deterministic layers; engine layer-routing + token/citation wiring with a fake provider;
  both providers via a stubbed `HttpMessageHandler`; gated real-endpoint test). ADR-0009.
- **Completes the RAG loop:** ingest (6 formats) → `.udf` → retrieve → answer.

### Added — Slice 6a: embeddings (ONNX MiniLM) + quantizer
- **`DocNest.Core/Quantization`** — `Quantizer` (float32/float16/int8/binary), ported byte-for-byte from
  numpy (float16 IEEE-half LE, int8 scaled+truncated, binary MSB-first packbits) — the `.udf`
  `embeddings.bin` cross-ecosystem layout. `Stride`/`BytesPerElement`/`CosineSimilarity`. Zero dependency.
- **`DocNest.Embeddings`** (new assembly) — `OnnxEmbedder` (`IEmbedder`): WordPiece tokenize → ONNX
  Runtime inference → mean-pool with mask → L2-normalise → 384-dim, batched; `WordPieceTokenizer`
  (hand-rolled, zero extra dep); `MiniLmModel` (paths + opt-in Hugging Face download).
- **Dependency:** `Microsoft.ML.OnnxRuntime` (MIT) — embeddings only, behind the `OnnxEmbedder` wrapper.
- **Tests** — +16 Quantizer (always run, incl. byte-layout parity) + 2 gated ONNX real-inference tests
  (`[SkippableFact]`; skip until the ~90 MB model is provisioned). ADR-0008.
- The real embedder now plugs into the Slice-5 retriever and the Slice-2 `.udf` writer. LLM + answer engine = Slice 6b.

### Added — Slice 5: hybrid retrieval engine
- **`DocNest.Retrieval`** (new assembly) — `HybridRetriever` (`IRetriever`): SQLite FTS5 BM25 + dense
  cosine (over an injected `IEmbedder`) + RRF fusion (K=60, BM25 2.0, Dense 1.5) + 1-hop section-graph
  expansion (child 0.15 / sibling 0.10 / semantic 0.12; child→parent disabled) + SHA-256-fingerprint
  cache. Pure `RrfFusion`/`RetrievalTokenizer`; SQLite behind `SqliteRetrievalStore`.
- **Abstractions** — `IRetriever` + `RetrievalHit` record.
- **Dependency:** `Microsoft.Data.Sqlite` (MIT; FTS5 bundled). Dense = exact brute-force cosine (HNSW deferred).
- **Tests** — +11 (exact RRF/graph math; real-SQLite integration with a deterministic fake embedder;
  cache hit/invalidate; no-embedder degraded path). ADR-0007.
- The real ONNX embedder (Slice 6) plugs into the `IEmbedder` seam; the 5-layer LLM answer engine is a later slice.

### Added — Slice 4c: PDF parser (text PDFs)
- **`DocNest.Parsers/Pdf`** — `PdfParser` (font-size heading detection: median font, ×1.15 threshold,
  bold rule, distinct-size level banding, "Introduction" fallback, title = largest line) + `PdfLineExtractor`
  (reconstructs ordered lines from PdfPig letters, clustering on the text **baseline**). Registers in `ParserFactory`.
- **Dependency:** `PdfPig` (Apache-2.0, pure-managed) — pdf only, behind the `PdfParser` wrapper.
- **Tests** — +4; `.pdf` fixture built in-test with PdfSharp (MIT). **All 6 common formats now ingest**
  (md, html, csv, docx, xlsx, pdf). ADR-0006.
- **Deferred (scheduled):** PDF table extraction and OCR/scanned PDFs are their own later hardening slices.

### Added — Slice 4b: OpenXML parsers (docx + xlsx)
- **`DocNest.Parsers/Office`** — `DocxParser` (ordered body walk; heading styles + ALL-CAPS/bold/colon
  pseudo-headings; tables with `gridSpan`/`vMerge` **merged-cell expansion** via `DocxTable`) and
  `ExcelParser` (each sheet → section; shared-strings + sparse-row reading via `XlsxReader`; logical-table
  split heuristic; `.xls` rejected). Both register in `ParserFactory`.
- **Dependency:** `DocumentFormat.OpenXml` (MIT) — docx/xlsx only, behind the Office `IParser` wrappers.
- **Tests** — +9; `.docx`/`.xlsx` fixtures built in-test via the OpenXML SDK (no committed binaries).
- Addresses the "complex tables" hardening target (Word merged cells). ADR-0005.

### Added — Slice 4: text-format parsers + ParserFactory
- **`DocNest.Parsers`** (new assembly) — `MarkdownParser` (zero-dep ATX scan with fenced-code
  tracking), `CsvParser` (zero-dep RFC-4180 reader + delimiter heuristic + UTF-8-BOM→UTF-8→Latin-1
  cascade), `HtmlParser` (AngleSharp; `h1`–`h6` hierarchy + `<table>` rowspan/colspan grid expansion),
  and `ParserFactory` (ordered registry, first-match routing, runtime register/unregister).
- **Dependency:** AngleSharp (MIT) — HTML only, kept behind the `HtmlParser` `IParser` wrapper.
- **Tests** — +18 (per-parser case tables incl. HTML rowspan grid + CSV quoting; ParserFactory;
  parse → pipeline → `.udf` round-trip for `.md`/`.csv`/`.html`). Real text files now ingest end-to-end.
- Parser sub-split: OpenXML (docx/xlsx) and PDF land in later slices (4b/4c).

### Added — Slice 3: pipeline + normaliser (milestone M1)
- **`DocNest.Core/Pipeline`** — `SectionNormaliser` (immutable two-pass §id assignment with compact
  depth, parent/child links, `token_count = int(words×1.3)`, table column-width normalisation) and
  `DocNestPipeline` (normalise → deterministic key-numbers → deterministic keywords; optional injected
  `IParser` for `ProcessAsync(path)`).
- **`DocNest.Core/Intelligence`** — `KeyNumberExtractor` and `KeywordExtractor`: pure, LLM-free,
  `[GeneratedRegex]` ports of the Python `key_numbers.py` / `keywords.py` (figure detection + label
  binding + noise filters; frequency × specificity keywords with title priority).
- **Tests** — +28 (normaliser, extractor parity case tables, pipeline incl. a `.udf` round-trip tie-in).
- **M1 reached:** a `.udf` round-trips between Python and .NET on synthetic documents (Slices 1–3).

### Added — Slice 2: `.udf` read/write
- **`DocNest.Storage`** — `ZipStorageBackend` implementing `IStorageBackend` over
  `System.IO.Compression` (DEFLATE for JSON/text, stored for binary/images).
- **`DocNest.Core/Udf`** — wire DTOs (`ManifestDto`/`CatalogueDto`/`ContentDto`/…) modelling the exact
  `.udf` JSON schema; a dedicated `UdfJson` serializer that writes `null`s and emits raw non-ASCII
  (`UnsafeRelaxedJsonEscaping`) to match Python's `ensure_ascii=False`; `UdfWriter`, `UdfReader` +
  `UdfPackage.ToDocument()` (reconstruct by §id join), and `SourceSanitiser`.
- **Tests** — `DocNest.Storage.Tests` + `.udf` read/write/round-trip/key-parity/version-gate/embeddings
  tests; cross-ecosystem interop tests E1 (load a Python-built golden `.udf`) and E2 (Python reads a
  .NET-built `.udf`), both `[SkippableFact]` (skip with reason until a fixture / Python env exists).
- **Tooling** — `tools/make_fixture.py` to generate the golden `tests/fixtures/sample.udf` from Python.

### Changed
- Renamed the `.udf` constant holder `Udf` → **`UdfFormat`** so the new `DocNest.Udf` namespace doesn't
  collide with a same-named type. `UdfFormat.Version` is unchanged (`"1.0"`). (Pre-release; no consumers.)

### Added — Slice 1: Core domain + contracts
- **`DocNest.Abstractions`** — immutable domain records (`TableData`, `ImageRef`, `Section`,
  `KeyNumber`, `RawDocument`, `DocMeta`, `Document`, `SectionIndexEntry`, `Catalogue`), the five
  wrapper interfaces (`IParser`, `IEmbedder`, `ILlmProvider`, `IStorageBackend`, `ISearchProvider`),
  the `DocNestException` hierarchy (base + 9 specific types), and the `Udf.Version` (`"1.0"`) constant.
- **`DocNest.Core`** — `System.Text.Json` source-generated context (`DocNestJson`) and
  `DocId.FromPath` (ports the Python `_make_doc_id` slug rules).
- **Tests** — xUnit suite (32 tests): defaults parity, `SectionId` alias non-serialisation,
  exception hierarchy, JSON round-trip / key fidelity / forward-compat, and contract implementability.

### Notes
- Targets `net8.0`; the library has **zero runtime package dependencies**. Central Package Management.
- JSON serialisation omits nulls (`WhenWritingNull`); collection properties are guaranteed never-null
  via null-coalescing `init` setters.
- The `.udf` wire schema (`manifest`/`catalogue`/`content` + `embeddings.bin`) is a **separate
  contract** from the in-memory domain and is implemented in Slice 2; domain records are
  persistence-ignorant. See [ADR-0001](docs/adr/0001-domain-records-and-wrapper-interfaces.md).
