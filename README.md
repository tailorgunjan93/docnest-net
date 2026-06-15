# DocNest .NET

> The document normalization engine RAG has always needed — native for .NET.
> **Secure · Fast · Reliable · Cost-Effective.**

An idiomatic .NET / C# port of [DocNest](https://github.com/tailorgunjan93/docnest) (`docnest-ai` on
PyPI). DocNest reads a document's **structure** before its content, so an LLM always receives the right
section as context — and emits a portable, self-contained **`.udf`** knowledge base that is
**byte-compatible with the Python implementation**.

> **Status: pre-1.0, built slice-by-slice** under a gated development protocol. The core pipeline,
> hybrid retrieval, cross-encoder reranking, and the 5-layer answer engine are implemented and tested.

## What it does
- **Structure-first normalization** — parse `md` / `html` / `csv` / `docx` / `xlsx` / `pdf` into a
  section tree (headings and tables preserved, never blindly chunked), then write a `.udf` archive
  (JSON catalogue + sections + optional embeddings) that round-trips cross-ecosystem with the Python engine.
- **Hybrid retrieval** — SQLite FTS5 (BM25) + dense MiniLM embeddings + a **cross-encoder reranker**
  (ms-marco-MiniLM) + RRF fusion + a 1-hop section graph, cached to disk.
- **5-layer answer engine** — precomputed key-numbers → extractive → single-section / multi-section /
  full-document LLM, escalating only as needed (most factual questions answer at 0 LLM tokens).
- **Local-first** — parse → normalize → embed runs with no mandatory network calls (local ONNX models);
  cloud LLMs are opt-in and sit behind a provider wrapper.

## Architecture
```
file  → IParser → DocNestPipeline (normalise · key-numbers · keywords) → Document → .udf
query → HybridRetriever (BM25 + dense + cross-encoder rerank + RRF + graph) → top-k sections
      → DocNestQueryEngine (5 layers) → answer (+ citations, tokens, confidence)
```
Every external dependency sits behind a DocNest wrapper interface (`IParser`, `IEmbedder`, `IReranker`,
`IRetriever`, `ILlmProvider`, `IStorageBackend`); package versions are centrally pinned.

## Projects
| Project | Role |
|---|---|
| `DocNest.Abstractions` | Domain records + wrapper interfaces |
| `DocNest.Core` | Pipeline, normaliser, `.udf` reader/writer, quantizer |
| `DocNest.Parsers` | md / html / csv / docx / xlsx / pdf parsers |
| `DocNest.Embeddings` | ONNX MiniLM embedder + ms-marco cross-encoder reranker |
| `DocNest.Retrieval` | Hybrid retriever (FTS5 + dense + rerank + RRF + graph) |
| `DocNest.Query` | 5-layer answer engine + LLM providers |
| `DocNest.Storage` | `.udf` storage backend |
| `DocNest.Cli` | `docnest` CLI (`convert` / `query` / `info`) |

## Quick start
```bash
dotnet build DocNest.sln -c Release
dotnet test  DocNest.sln                 # full xUnit suite

# normalise a document to .udf
dotnet run --project src/DocNest.Cli -- convert report.pdf -o report.udf

# ask a question (deterministic layers; add --allow-llm + a provider for the LLM layers)
dotnet run --project src/DocNest.Cli -- query report.udf "what is the remaining carbon budget?"

dotnet run --project src/DocNest.Cli -- info report.udf
```

### Embeddings & reranker models (opt-in)
The MiniLM embedder and the ms-marco cross-encoder are ~90 MB ONNX models, downloaded on first use and
**not committed**. Without them, retrieval degrades cleanly to BM25 + the structural graph.

## Accuracy
A multi-format eval (10 documents · 88 questions · 5 formats — the same set as the Python reference)
tracks parity. Latest run (dense + cross-encoder rerank, `gpt-oss-120b` narrator, `qwen2.5` judge):
**~7.1/10 overall** — generated docs **7.3/81%**, PDFs **6.8/70%** (every PDF ≥ 6.0). Honest and
reproducible; see [`eval/`](eval/). The cross-encoder reranker lifted PDFs from **5.1 → 6.8** (hit-rate
47% → 70%). The Python reference's honest figure is 8.5/10 with `gpt-oss-120b`.

## Development
This repo follows a **mandatory gated protocol**: understand (BA / Dev / QA + roadmap) → plan →
impact/risk → design + ADR → tests-first → full suite green → owner sign-off per phase. No change is
allowed to break the `.udf` cross-ecosystem contract, `UDF_VERSION`, or the public API.

| Doc | Purpose |
|---|---|
| [CHARTER](docs/CHARTER.md) | Vision, audience, success metrics (the North Star) |
| [DEVELOPMENT_PROTOCOL](docs/DEVELOPMENT_PROTOCOL.md) | The mandatory gated workflow |
| [ROADMAP](docs/ROADMAP.md) | Slices and milestones |
| [ADRs](docs/adr/) | Architecture decision records |
| [Phase 0 docs](docs/phase0/) | Per-slice BA / Dev / QA understanding |

## License
TBD — see the repository owner.
