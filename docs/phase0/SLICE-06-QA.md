# Phase 0.3 — QA / User Document
## Slice 6a: Embeddings (ONNX) + Quantizer

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### What "working" means
The `Quantizer` compresses/decompresses embedding vectors exactly as the Python engine does (so
`.udf` `embeddings.bin` is byte-compatible), and the `OnnxEmbedder` produces real, deterministic,
semantically-meaningful MiniLM vectors locally — making dense retrieval and `.udf` embeddings real.

### Test plan (test-first — written in Phase 3, failing first)

**Unit — `Quantizer` (pure, always runs)**
- U1: float32 round-trip is **exact**.
- U2: float16 round-trip within ~1e-3; int8 within ~1% (relative) for in-range values; binary →
  sign vector (`bit*2-1`).
- U3: `Stride` = `ceil(dims/8)` for binary, else `dims*BytesPerElement`; `BytesPerElement` = 4/2/1/0.
- U4: `CosineSimilarity` matches the dot/‖·‖ definition; zero vector → 0.
- U5: an unsupported mode → throws; `quantize` then `dequantize` length matches `Stride`.
- U6 (layout parity): float16 bytes for a known vector equal the IEEE-half little-endian bytes
  (the `embeddings.bin` contract).

**Unit/Integration — `OnnxEmbedder` (gated on model presence, `[SkippableFact]`)**
- E1: 384-dim output, L2-norm ≈ 1; the same text twice → identical vectors (deterministic).
- E2: cosine("carbon emissions budget", "greenhouse gas allowance") > cosine(either, "banana bread recipe").
- E3: batching N texts returns N vectors in order; empty input → empty output.
- E4: cancellation is honoured.

**Integration — end-to-end (gated where it needs the model)**
- I1: ingest a doc → `OnnxEmbedder` → `Quantizer` → `UdfWriter` (`embeddings.bin` present,
  `embedding_dims=384`, `quantization="float16"`) → `UdfReader` → dequantise → vectors recovered.
- I2: `HybridRetriever` with an `OnnxEmbedder` ranks a semantically-related section above an unrelated one.

### Fixtures / model
The MiniLM model+vocab are not committed (~90 MB). `MiniLmModel.EnsureDownloaded(cacheDir)` (or
`tools/get_minilm`) fetches them; the gated tests **skip with a clear reason** when absent (never a
silent pass). Quantizer tests need no model.

### Edge / negative cases
- Very long text → truncated to max length (no crash); empty/whitespace text → a valid (zero-ish) vector.
- Non-ASCII text tokenizes (WordPiece `[UNK]` for unknown pieces) without error.
- int8 quantization of a vector whose max is ~0 → no divide-by-zero (the `+1e-8`).
- binary stride for non-multiple-of-8 dims rounds up; dequantize trims to `dims`.

### What constitutes a regression (regression-suite seeds)
- A quantization mode's byte layout drifting from Python (breaks `.udf` `embeddings.bin` cross-ecosystem).
- float16/int8 tolerance regressions; binary not producing a sign vector.
- `OnnxEmbedder` non-determinism, wrong dimensionality, or un-normalised output.
- A crash (instead of skip) when the model is absent.
