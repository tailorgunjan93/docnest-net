# SLICE-10 — BA / Functional: Dense embeddings in the eval retrieval path

> Eval-harness wiring + one regression test. **No change to the shipped library, public API,
> `.udf`/`UDF_VERSION`, RRF ranking, or the NuGet package.** This is the follow-up explicitly opened in
> [SLICE-08-ROADMAP.md](SLICE-08-ROADMAP.md) §"AC3 not met" → *"wire dense embeddings into retrieval."*
> (The companion follow-up, the LLM-as-judge, is the separate [SLICE-09](SLICE-09-BA.md).)

## Why
The accuracy eval (same 10 files / 88 questions / 5 formats as the Python reference, `eval/cases.json`)
scores the .NET engine at **6.7/10 overall** (Phase 1 generated 7.5, Phase 2 PDFs 5.1) with
gpt-oss-120b via Groq — versus the Python reference's **8.5/10**. Slice 8's RCA named the dominant
remaining cause:

> "the .NET eval pipeline retrieves **BM25-only (no embeddings)**, so the right section is often
> missing from the top-k; … several Layer-3 LLM answers come back empty."

The `HybridRetriever` already fuses FTS5-BM25 **+ dense cosine + semantic-graph edges** (ADR-0007) and
activates the dense + semantic signals when an `IEmbedder` is injected; the real `OnnxEmbedder` (MiniLM,
384-dim) shipped in Slice 6a (ADR-0008). But the eval constructs `new HybridRetriever(cacheDir)` **with
no embedder** ([eval/DocNest.Eval/Program.cs:88](../../eval/DocNest.Eval/Program.cs)), so the dense path
is dead — retrieval runs on BM25 + structural graph only. On PDF prose where question and answer
section share little surface vocabulary, BM25 misses, the wrong/empty section is retrieved, and Layer-3
LLM answers come back empty for lack of relevant context.

## What (scope)
Provision the MiniLM ONNX model and **inject one shared `OnnxEmbedder` into the eval's
`HybridRetriever`** so sections carry dense vectors, the dense + semantic-graph signals activate, and
retrieval picks the right section. The retriever already supports the embedder and is unit-tested with
a fake one; this slice adds only the harness wiring + a real-model regression test.

## Acceptance criteria
- **AC1 — dense path active.** With the model present, each document's index is built with section
  embeddings; the retriever's dense rank + semantic edges contribute (observable: embeddings stored and
  semantic edges > 0 on multi-section docs; the eval header reports "hybrid + dense").
- **AC2 — better section retrieval.** On a representative semantically-related but lexically-disjoint
  question, the hybrid+dense retriever **surfaces the correct section into the top-k**, where the
  BM25-only retriever **misses it entirely** (it shares no surface tokens with the query). This is the
  RCA's exact failure ("the right section is often missing from the top-k"). (Pinned by a real-model
  integration test.) Note: RRF weights BM25 (2.0) above dense (1.5), so a lexically-loaded section can
  still outrank a pure-semantic match — the dense win is *presence in the top-k*, not necessarily rank #1.
- **AC3 — eval improves.** Re-running with gpt-oss-120b **materially improves over the 6.7/10
  baseline** — Phase-2 (PDF) hit-rate up vs **47%** and overall up vs **6.7/10**, with **no Phase-1
  regression** (generated-file avg stays ≥ 7.5). The residual gap to Python's 8.5 is expected to be
  bounded by the eval's stricter local judge — the separate [SLICE-09](SLICE-09-BA.md) follow-up.
- **AC4 — no regression / graceful degrade.** Full xUnit suite green; public API, `QueryResult`,
  `.udf`/`UDF_VERSION` unchanged. With the model **absent** (offline/CI), the eval still runs the
  BM25-only path and reports cleanly (model provisioning is opt-in, ADR-0008).

## Non-goals
- Wiring embeddings into the **document pipeline** / `.udf` `embeddings.bin` (the **Quantizer** path).
  The retriever stores its own full-precision vectors in its SQLite cache and does **not** read
  `Section.Embedding`, so the Quantizer would not affect eval retrieval. Out of scope (tracked follow-up).
- Wiring the embedder into the **CLI** `query` command (same gap, separate concern). Tracked follow-up.
- HNSW/ANN speed work (brute-force cosine stays per ADR-0007) or changing the RRF constants.
- Closing the **judge** mismatch (that is [SLICE-09](SLICE-09-BA.md)).
