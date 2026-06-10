# ADR-0008 — ONNX embeddings, the Quantizer, and model handling

- **Status:** Accepted (owner approved straight-through to GATE 5)
- **Date:** 2026-06-10
- **Context slice:** Slice 6a — embeddings + quantizer
- **Builds on:** ADR-0001 (`IEmbedder`), ADR-0002 (`.udf` `embeddings.bin`)

## Context
Dense retrieval (Slice 5) and `.udf` `embeddings.bin` (Slice 2) need a real embedder; only a fake exists.
The Python engine uses sentence-transformers MiniLM. The pure-.NET equivalent is ONNX Runtime + a
WordPiece tokenizer running the all-MiniLM-L6-v2 ONNX graph. The `Quantizer` compresses vectors for `.udf`
storage and **defines the `embeddings.bin` byte layout** shared with Python.

## Decision
1. **Split Slice 6** into **6a** (this: `OnnxEmbedder` + `Quantizer`) and **6b** (LLM providers + the
   5-layer answer engine) — different dependencies and risk.
2. **`Quantizer` is pure, in `DocNest.Core`** (zero dependency): float32/float16/int8/binary, ported to
   match numpy byte-for-byte — float16 IEEE-half **little-endian**; int8 `scale=127/(|max|+1e-8)`,
   **truncate toward zero**, clip [-127,127]; binary `f>0` packed **MSB-first** (numpy `packbits`). This is
   the `.udf` cross-ecosystem contract → always unit-tested without any model.
3. **`OnnxEmbedder` in a new `DocNest.Embeddings` assembly** behind the `IEmbedder` wrapper, using
   **`Microsoft.ML.OnnxRuntime`** + **`Microsoft.ML.Tokenizers`** (BERT WordPiece). Mean-pool with the
   attention mask, L2-normalise; 384-dim; batched; max length 256.
4. **Model handling = file paths + opt-in download.** The embedder takes `modelPath`/`vocabPath`;
   `MiniLmModel.EnsureDownloadedAsync(cacheDir)` fetches them from Hugging Face on first use. The ~90 MB
   model is **not committed**; ONNX tests are `[SkippableFact]` gated on model presence (skip-with-reason).

## Consequences
**Positive:** real local embeddings (no Python, no cloud) make dense retrieval + `.udf` embeddings real;
the `.udf` byte layout is pinned and tested; OnnxRuntime stays behind the wrapper; the always-run Quantizer
tests give solid coverage independent of the model.
**Negative / cost:** the ONNX embedder's real-inference tests only run when the model is present (CI must
provision it or they skip); a couple of native deps (OnnxRuntime) increase package size for consumers who
use embeddings.
**Neutral:** binary/int8 lossy modes match Python's tolerances; mean-pooling/max-256 follow the MiniLM defaults.

## Alternatives considered
- *Call Python / sentence-transformers:* not pure-.NET; rejected (the whole point of the port).
- *Bundle the model in the package:* ~90 MB inflates every consumer's restore; opt-in download is leaner. Rejected.
- *Hand-rolled WordPiece:* error-prone vs the maintained `Microsoft.ML.Tokenizers`. Rejected.
- *Quantizer in the Embeddings assembly:* it's a pure `.udf`-contract service used by the writer — belongs in `Core`. Rejected.
