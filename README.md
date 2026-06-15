<div align="center">

# DocNest .NET

**Secure · Fast · Reliable · Cost-Effective**

*The document normalization engine RAG has always needed — native for .NET.*

[![NuGet](https://img.shields.io/nuget/v/DocNest.Core?color=green&label=DocNest.Core)](https://www.nuget.org/packages/DocNest.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![RAG Accuracy](https://img.shields.io/badge/RAG%20accuracy-7.1%2F10-brightgreen)](#-accuracy)

[Install](#-install) • [Quick start](#-quick-start-60-seconds) • [Library API](#-library-api) •
[CLI](#-cli) • [How it works](#-how-it-works) • [Packages](#-packages) • [Accuracy](#-accuracy)

</div>

---

An idiomatic .NET / C# port of [DocNest](https://github.com/tailorgunjan93/docnest) (`docnest-ai` on
PyPI). DocNest reads a document's **structure** before its content — every heading becomes a navigable
`§section`, every table is preserved as `{ caption, headers, rows[] }` — so an LLM always receives the
right section as context instead of a blind 512-char slice. The output is a portable **`.udf`** knowledge
base, **byte-compatible with the Python implementation**.

> **Status: pre-1.0**, built slice-by-slice under a gated protocol. Core pipeline, hybrid retrieval,
> cross-encoder reranking, and the 5-layer answer engine are implemented and tested.

### Two independent choices

- **Embeddings run locally** — a small ONNX MiniLM model (+ an optional ONNX cross-encoder reranker),
  downloaded once and cached. **No API key, fully offline.** *(Cloud embedding providers such as OpenAI
  are supported in the Python engine but are **not yet** ported to .NET — embeddings here are local-only.)*
- **The LLM is optional** — Layers 0–1 answer factual questions at **zero tokens, no key**. Add a provider
  **only** for synthesis (Layers 2–4): any OpenAI-compatible endpoint (OpenAI, Groq, Cerebras, Together,
  OpenRouter), Anthropic, or a fully local Ollama / LM Studio server. Here "OpenAI" means the *answer LLM*,
  not embeddings.

## The problem it solves

Most RAG pipelines ingest the same broken way — `extract text → split every 512 chars → embed → hope` —
which shreds tables and splits clauses mid-sentence. The LLM gets noise and returns approximate answers.
DocNest preserves structure:

```jsonc
// A revenue table survives as structured data the LLM can actually reason over:
{
  "section": "§4.2 Revenue by Region",
  "table": {
    "headers": ["Region", "Q2", "Q3", "Change"],
    "rows": [["Europe", "38.1%", "45.2%", "+7.1pp"], ["Asia", "29.3%", "41.7%", "+12.4pp"]]
  }
}
```

## 📦 Install

```bash
# Library — add what you need (DocNest.Abstractions comes transitively)
dotnet add package DocNest.Core        # pipeline, .udf reader/writer, normaliser
dotnet add package DocNest.Parsers     # md / html / csv / docx / xlsx / pdf
dotnet add package DocNest.Retrieval   # hybrid retriever (FTS5 + dense + rerank + RRF + graph)
dotnet add package DocNest.Query       # 5-layer answer engine + LLM providers
dotnet add package DocNest.Embeddings  # optional: local ONNX embeddings + cross-encoder reranker

# CLI — installs the `docnest` command
dotnet tool install -g DocNest.Cli
```

## 🚀 Quick start (60 seconds)

No API key, no internet — parse a document, save a `.udf`, and answer factual questions at **0 LLM tokens**:

```csharp
using DocNest;
using DocNest.Parsers;
using DocNest.Pipeline;
using DocNest.Query;
using DocNest.Retrieval;
using DocNest.Udf;

// 1. Parse → normalise → write a portable .udf knowledge base
var raw = await new ParserFactory().Get("report.pdf").ParseAsync("report.pdf");
var doc = new DocNestPipeline().Process(raw);
await new UdfWriter().WriteAsync(doc, "report.udf");

// 2. Load it back and ask a question (deterministic layers — no LLM)
var document = (await UdfReader.LoadAsync("report.udf")).ToDocument();

using var retriever = new HybridRetriever(".docnest_cache");
var engine = new DocNestQueryEngine(retriever);          // no LLM → Layers 0–1 only
var result = await engine.AnswerAsync(document, "What was Q3 revenue?", allowLlm: false);

Console.WriteLine(result.Answer);      // e.g. "Q3 revenue: $38M (source: §3.1)"
Console.WriteLine(result.LayerUsed);   // 0 or 1 — answered from the index
Console.WriteLine(result.TokensUsed);  // 0
```

## 🧰 Library API

### Add an LLM (Layers 2–4) — any OpenAI-compatible endpoint

`OpenAiCompatibleLlmProvider` works with OpenAI, Groq, Cerebras, Together, OpenRouter, and local servers
(Ollama, LM Studio) — just change the base URL and model:

```csharp
using DocNest;
using DocNest.Query;

// Groq (generous free tier) — or OpenAI, Cerebras, Ollama, …
ILlmProvider llm = new OpenAiCompatibleLlmProvider(
    apiKey:  Environment.GetEnvironmentVariable("GROQ_API_KEY")!,
    model:   "llama-3.3-70b-versatile",
    baseUrl: "https://api.groq.com/openai/v1");

using var retriever = new HybridRetriever(".docnest_cache");
var engine = new DocNestQueryEngine(retriever, llm);
var result = await engine.AnswerAsync(document, "Summarise the key risks.", allowLlm: true);

Console.WriteLine(result.Answer);
Console.WriteLine(string.Join(", ", result.Citations));   // e.g. ["§5.2", "§5.3"]
Console.WriteLine($"Layer {result.LayerUsed} · {result.TokensUsed} tokens · conf {result.Confidence:F2}");
```

```csharp
// Local, fully offline via Ollama (OpenAI-compatible endpoint)
ILlmProvider local = new OpenAiCompatibleLlmProvider("ollama", "qwen2.5", "http://localhost:11434/v1");

// Anthropic Claude
ILlmProvider claude = new AnthropicLlmProvider(
    Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!, "claude-haiku-4-5-20251001");
```

### Turn on semantic retrieval (dense embeddings)

The MiniLM ONNX model (~90 MB) is downloaded once on first use and cached locally — fully local, no cloud:

```csharp
using DocNest.Embeddings;

var (modelPath, vocabPath) = await MiniLmModel.EnsureDownloadedAsync("./models/minilm");
using var embedder = new OnnxEmbedder(modelPath, vocabPath);

// BM25 + dense cosine + semantic-graph edges (degrades to BM25-only if no embedder is passed)
using var retriever = new HybridRetriever(".docnest_cache", embedder);
```

### Add the cross-encoder reranker (best accuracy on dense PDFs)

```csharp
using DocNest.Embeddings;

var (ceModel, ceVocab) = await CrossEncoderModel.EnsureDownloadedAsync("./models/ms-marco");
using var reranker = new OnnxCrossEncoderReranker(ceModel, ceVocab);

// Re-scores the top RRF candidates by true query↔section relevance → the right section reaches the LLM
using var retriever = new HybridRetriever(".docnest_cache", embedder, reranker);
```

### Retrieve sections directly (no LLM)

```csharp
var hits = await retriever.RetrieveAsync(document, "remaining carbon budget", k: 5);
foreach (var hit in hits)
    Console.WriteLine($"{hit.Section.Id}  {hit.Section.Title}  (score {hit.Score:F3})");
```

### Parse any supported format / register a custom parser

```csharp
using DocNest;
using DocNest.Parsers;

var factory = new ParserFactory();                  // md, html, csv, docx, xlsx, pdf built in
var raw = await factory.Get("data.xlsx").ParseAsync("data.xlsx");
Console.WriteLine($"{raw.Sections.Count} sections");

// Add your own format — implement IParser and register it (first match wins)
factory.Register(new MyFormatParser());             // class MyFormatParser : IParser
```

### Inspect a `.udf`

```csharp
var package = await UdfReader.LoadAsync("report.udf");
Console.WriteLine($"Title:       {package.Manifest.Title}");
Console.WriteLine($"UDF version: {package.Manifest.UdfVersion}");
Console.WriteLine($"Sections:    {package.Catalogue.SectionIndex.Count}");
Console.WriteLine($"Key numbers: {package.Catalogue.KeyNumbers.Count}");
```

## 🖥 CLI

```bash
dotnet tool install -g DocNest.Cli      # provides the `docnest` command

# Convert a document to .udf (-q float32|float16|int8|binary, default float16)
docnest convert report.pdf -o report.udf

# Ask a question (deterministic layers by default; add an LLM for Layers 2–4)
docnest query report.udf "What was Q3 revenue?"
docnest query report.udf "Summarise the risks." \
  --provider openai --model llama-3.3-70b-versatile \
  --base-url https://api.groq.com/openai/v1 --api-key $GROQ_API_KEY

# Catalogue summary
docnest info report.udf
```

## 🧠 How it works

A document is normalised once, then queried forever:

```
file  → IParser → DocNestPipeline (normalise · key-numbers · keywords) → Document → .udf
query → HybridRetriever (BM25 + dense + cross-encoder rerank + RRF + 1-hop graph) → top-k sections
      → DocNestQueryEngine (5 layers) → answer (+ citations, tokens, confidence)
```

The `.udf` is a self-contained ZIP — `manifest.json` (version, model) + `catalogue.json` (section index,
key-numbers, keywords) + `content.json` (section text/tables) + `embeddings.bin` (quantised vectors) —
portable and byte-compatible with the Python engine.

### Five answer layers — escalate only as needed

| Layer | Mechanism | Tokens |
|---|---|---|
| 0 | Pre-computed key-numbers / summary | **0** |
| 1 | Extractive from the top section | **0** |
| 2 | Single-section LLM | ~300 |
| 3 | Multi-section synthesis (reranked context) | ~900 |
| 4 | Broad fallback over retrieved sections | ~1,500 |

Layers 0–1 answer many factual questions at **zero LLM cost**; the engine escalates to the LLM only when
the deterministic layers aren't confident.

## 📦 Packages

| Package | Role |
|---|---|
| `DocNest.Abstractions` | Domain records + wrapper interfaces (`IParser`, `IEmbedder`, `IReranker`, `IRetriever`, `ILlmProvider`) |
| `DocNest.Core` | Pipeline, normaliser, `.udf` reader/writer, quantizer |
| `DocNest.Parsers` | md / html / csv / docx / xlsx / pdf parsers |
| `DocNest.Embeddings` | ONNX MiniLM embedder + ms-marco cross-encoder reranker |
| `DocNest.Retrieval` | Hybrid retriever (FTS5 BM25 + dense + rerank + RRF + graph) |
| `DocNest.Query` | 5-layer answer engine + LLM providers |
| `DocNest.Storage` | `.udf` ZIP storage backend |
| `DocNest.Cli` | `docnest` dotnet tool (`convert` / `query` / `info`) |

Every external dependency sits behind a DocNest wrapper interface; package versions are centrally pinned.

## 📂 Supported formats

`pdf` (PdfPig, font-size heading detection) · `docx` / `xlsx` (OpenXML) · `html` (AngleSharp) ·
`csv` / `tsv` · `markdown`. Tables are preserved as structured `{ caption, headers, rows[] }`, never
flattened.

## 🧪 Accuracy

A multi-format eval (10 documents · 88 questions · 5 formats — the same set as the Python reference)
tracks parity. Latest run — dense + cross-encoder rerank, `gpt-oss-120b` narrator, `qwen2.5` judge:

| Format | Score | Hit-rate (≥7) |
|---|---|---|
| 📊 XLSX | 8.7 / 10 | 93% |
| 📋 MD | 8.7 / 10 | 100% |
| 📝 DOCX | 7.0 / 10 | 79% |
| 🌐 HTML | 4.8 / 10 | 50% |
| 📄 PDF | 6.8 / 10 | 70% |
| **Overall** | **~7.1 / 10** | **~78%** |

The cross-encoder reranker lifted PDFs from **5.1 → 6.8** (hit-rate 47% → 70%). Honest and reproducible —
see [`eval/`](eval/). The Python reference's honest figure is **8.5/10** with `gpt-oss-120b`; this .NET
port is closing the gap slice by slice.

## 🛠 Development

Built under a **mandatory gated protocol**: understand (BA / Dev / QA + roadmap) → plan → impact/risk →
design + ADR → tests-first → full suite green → owner sign-off per phase. No change may break the `.udf`
cross-ecosystem contract, `UDF_VERSION`, or the public API.

| Doc | Purpose |
|---|---|
| [CHARTER](docs/CHARTER.md) | Vision, audience, success metrics |
| [DEVELOPMENT_PROTOCOL](docs/DEVELOPMENT_PROTOCOL.md) | The gated workflow |
| [ROADMAP](docs/ROADMAP.md) | Slices and milestones |
| [ADRs](docs/adr/) | Architecture decision records |
| [Phase 0 docs](docs/phase0/) | Per-slice BA / Dev / QA understanding |

## 📄 License

MIT — free for commercial use. See [LICENSE](LICENSE).

## 🔗 Ecosystem

| Project | Description |
|---|---|
| [docnest](https://github.com/tailorgunjan93/docnest) | The original Python engine (`pip install docnest-ai`) |
| [udf-spec](https://github.com/tailorgunjan93/udf-spec) | Open specification for the `.udf` format |

<div align="center">

🔒 Secure · ⚡ Fast · 🛡️ Reliable · 💰 Cost-Effective

</div>
