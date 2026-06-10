# Phase 0.1 — BA / Functional Document
## Slice 6a: Embeddings (ONNX MiniLM) + Quantizer

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan · **Depends on:** Slices 1–5 (✅)

---

### WHY
Dense retrieval (Slice 5) and `.udf` `embeddings.bin` (Slice 2) both depend on a real **embedder** —
so far only a fake exists. This slice provides a **native ONNX `IEmbedder`** (all-MiniLM-L6-v2, 384-dim,
no Python, no cloud) plus the **`Quantizer`** that compresses vectors for `.udf` storage. Together they
make dense retrieval *real* and let the pipeline write embeddings into the `.udf`. The **LLM providers
and the 5-layer answer engine** are the separate **Slice 6b** (different dependencies + risk).

### WHAT — exact functional behaviour
1. **`OnnxEmbedder : IEmbedder`** — load the MiniLM ONNX model + WordPiece vocab; tokenize input texts
   (BERT WordPiece, padding/truncation), run ONNX inference, **mean-pool** the token embeddings with the
   attention mask, **L2-normalise** → a `float[]` per text (`Dims = 384`, `ModelName =
   "sentence-transformers/all-MiniLM-L6-v2"`). Batched (`EmbedAsync(texts)`), bounded memory.
2. **`Quantizer`** — compress/decompress a `float[]` vector: `float32` (4 B/dim), `float16` (2 B, default),
   `int8` (1 B, scale = 127/|max|), `binary` (1 bit/dim, packbits). `Stride(dims)`, `BytesPerElement`,
   `CosineSimilarity`. Ports the Python `Quantizer` exactly.
3. **Wiring:** `UdfWriter` (Slice 2) can now receive real embeddings (quantised) → `embeddings.bin`;
   `HybridRetriever` (Slice 5) can take an `OnnxEmbedder` for real dense ranking.

**Before vs after**
- *Before:* dense retrieval / `.udf` embeddings only work with a fake embedder.
- *After:* `new OnnxEmbedder(modelPath)` produces real MiniLM vectors; quantised embeddings round-trip in `.udf`.

**Acceptance criteria**
- AC1: `Quantizer` round-trips each mode within the documented tolerance (float32 exact; float16 ~1e-3;
  int8 ~1%; binary = sign); `Stride`/`BytesPerElement` match Python.
- AC2: `OnnxEmbedder` returns 384-dim L2-normalised vectors; the same text → the same vector
  (deterministic); semantically similar texts have higher cosine than dissimilar ones.
- AC3: a real `.udf` written with quantised MiniLM embeddings reads back and the vectors dequantise.
- AC4: `HybridRetriever` with an `OnnxEmbedder` ranks a semantically-related section above an unrelated one.
- AC5: embedder honours `CancellationToken` and batches without unbounded memory.

### Non-goals (this slice)
- No **LLM providers** / **5-layer answer engine** (Slice 6b).
- No model **training**; no GPU; no alternative embedding models (MiniLM only this slice).
- No automatic model **download** if the owner prefers a provided path (see GATE-0 decision).

### HOW — scenarios
- *Embed:* `await embedder.EmbedAsync(["carbon budget", "emissions cap"])` → two 384-dim vectors with high mutual cosine.
- *Quantize:* a vector → float16 bytes (768 B) → dequantize → ≈ original.
- *End-to-end:* ingest a doc → embed → quantize → `.udf` (`embeddings.bin` present, `embedding_dims=384`);
  retrieve with the ONNX embedder → dense signal works.

### Traceability
Serves **Privacy** (local ONNX, no cloud), **Cost** (free embeddings), **Reliability/Speed** (real dense
recall, quantised storage). Unblocks the dense half of retrieval and `.udf` embeddings.
