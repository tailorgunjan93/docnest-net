# Phase 1 + Phase 2 — Impact/Risk & Design
## Slice 6a: Embeddings (ONNX) + Quantizer

**Status:** Phases 1–2 (owner approved straight-through to GATE 5) · **Owner:** Gunjan
**ADR:** [ADR-0008](../adr/0008-embeddings-and-quantizer.md) · **Decisions:** 6a/6b split; model = paths + opt-in download.

---

## Phase 1 — Impact & Risk
**Blast radius:** `Quantizer` added to `DocNest.Core` (pure); new `DocNest.Embeddings` assembly (+
`Microsoft.ML.OnnxRuntime`, `Microsoft.ML.Tokenizers`). No Slice-1..5 changes; no `.udf` schema change
(the quantizer *defines* the already-declared `embeddings.bin` layout).

**Watch-items / mitigations:**
1. **`embeddings.bin` byte-layout parity** (float16 LE, int8 truncate, binary MSB-first packbits) — the
   `.udf` cross-ecosystem contract. Mitigation: exact unit tests incl. a known-vector layout check.
2. **ONNX model handling** (~90 MB, not committed). Mitigation: file paths + opt-in
   `MiniLmModel.EnsureDownloaded`; ONNX tests `[SkippableFact]` gated on model presence.
3. **Tokenizer correctness** (WordPiece). Mitigation: `Microsoft.ML.Tokenizers` BERT tokenizer; gated tests.
4. **Bounded memory** — batch the embedder. Mitigation: batch size cap; `CancellationToken`.
**Risk Med → Low:** the `Quantizer` (the contract piece) is pure and fully tested without a model; the
ONNX embedder is isolated behind `IEmbedder` with gated tests.

## Phase 2 — Design
**DSA:** quantize/dequantize O(dims); embedder O(batch·seq·hidden) ONNX inference; mean-pool O(seq·hidden).
Memory bounded by batch size.

**SOLID / patterns:** `OnnxEmbedder : IEmbedder` (Strategy/DIP — already injected into the retriever +
`.udf` writer); OnnxRuntime/Tokenizers live only inside `DocNest.Embeddings` (wrapper rule). `Quantizer`
is a pure value service in Core.

**Quantizer byte layout (the contract):** `float32` raw LE; `float16` `System.Half` LE
(`BinaryPrimitives.WriteHalfLittleEndian`); `int8` `scale=127/(|max|+1e-8)`, **truncate toward zero**
(matches numpy `astype(int8)`), clip [-127,127]; `binary` bit `f>0` packed **MSB-first** (matches
`np.packbits`). `Stride` = `ceil(dims/8)` (binary) else `dims*BytesPerElement`.

**OnnxEmbedder pipeline:** WordPiece tokenize → `input_ids`/`attention_mask`/`token_type_ids` (zeros),
pad/truncate to max 256 → `InferenceSession.Run` → `last_hidden_state` → mean-pool with mask → L2-normalise.

**Code plan (signatures):**
- `DocNest.Core/Quantization/Quantizer.cs` — `Quantizer(string mode="float16")`; `byte[] Quantize(float[])`;
  `float[] Dequantize(byte[], int dims)`; `int Stride(int dims)`; `int BytesPerElement`;
  `static double CosineSimilarity(float[] a, float[] b)`.
- `DocNest.Embeddings/OnnxEmbedder.cs` — `OnnxEmbedder(string modelPath, string vocabPath, int maxLength=256, int batchSize=32)` : `IEmbedder`; `Dims=384`; `ModelName="sentence-transformers/all-MiniLM-L6-v2"`; `IDisposable`.
- `DocNest.Embeddings/MiniLmModel.cs` — `static Task<(string ModelPath, string VocabPath)> EnsureDownloadedAsync(string cacheDir, CancellationToken ct=default)` (opt-in HF download); `static bool IsPresent(string cacheDir)`.

**Resolved open questions:** Q1 → 6a/6b split; Q2 → paths + opt-in `EnsureDownloaded`; Q3 →
`Microsoft.ML.Tokenizers` BERT WordPiece; Q4 → mean pooling, max length 256.
