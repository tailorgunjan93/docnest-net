# Phase 0.2 — Dev / Technical Document
## Slice 6a: Embeddings (ONNX) + Quantizer

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### 1. Python reference — read end-to-end (files traced)
| Python | Logic | Port |
|---|---|---|
| `quantizer.py` `Quantizer` | float32/float16/int8/binary quantize+dequantize; `stride`; `bytes_per_element`; `cosine_similarity` | **`Quantizer`** (pure) |
| `embedder.py` `IEmbedder` ABC + `LangChainEmbedder` | `embed(texts)→ndarray`, `dims`, `model_name`; default MiniLM, normalize | **`OnnxEmbedder`** (new native impl; not a LangChain port) |

### 2. Dependency decisions
- **`Microsoft.ML.OnnxRuntime`** (MIT) — run the MiniLM ONNX graph on CPU.
- **`Microsoft.ML.Tokenizers`** (MIT) — BERT **WordPiece** tokenizer (needs `vocab.txt`).
- **Quantizer** = zero dependency (uses `System.Half` for float16, BCL bit ops for binary/int8).

### 3. The ONNX embedder pipeline (the real work)
- **Model + vocab files:** `all-MiniLM-L6-v2` `model.onnx` (~90 MB) + `vocab.txt`. **Too big to commit.**
  The embedder takes file **paths** (`OnnxEmbedder(string modelPath, string vocabPath)`). A helper
  `MiniLmModel.EnsureDownloaded(cacheDir)` may fetch them from Hugging Face on first use (opt-in).
- **Tokenize:** WordPiece → `input_ids`, `attention_mask`, `token_type_ids` (all zeros); pad/truncate to a
  max length (e.g. 256); build `DenseTensor<long>` inputs.
- **Infer:** `InferenceSession.Run` → `last_hidden_state` `[batch, seq, 384]`.
- **Mean-pool with mask:** `pooled = Σ_t (hidden_t · mask_t) / Σ_t mask_t`; then **L2-normalise**. → `float[384]`.
- **Batch:** process in batches (e.g. 32) to bound memory; honour `CancellationToken`.

### 4. Quantizer mapping (exact port)
- `float32` → `BitConverter`/`MemoryMarshal` raw bytes. `float16` → `System.Half` (`(Half)f` → 2 bytes via
  `BitConverter.GetBytes(Half)` / `Half.GetBytes`). `int8` → `scale = 127/(|max|+1e-8)`, clip[-127,127],
  `sbyte`. `binary` → bit `f>0`, pack 8/byte. Dequantize inverts each; `binary` → `bit*2-1`.
- `Stride(dims)` = `ceil(dims/8)` for binary else `dims*BytesPerElement`; `BytesPerElement` =
  4/2/1/0. `CosineSimilarity(a,b)`. **These must match the Python `embeddings.bin` layout** (Slice-2 contract).

### 5. Layout (new assembly + Core additions)
```
src/DocNest.Core/Quantization/Quantizer.cs        (pure; Core, zero dep)
src/DocNest.Embeddings/OnnxEmbedder.cs            (new assembly; OnnxRuntime + Tokenizers)
src/DocNest.Embeddings/MiniLmModel.cs             (paths + optional download helper)
tests/DocNest.Core.Tests/Quantization/…           (Quantizer round-trip — always runs)
tests/DocNest.Embeddings.Tests/…                  (OnnxEmbedder — gated on model presence)
```
- **Quantizer in `DocNest.Core`** (pure, used by `.udf` writer + tests). **`OnnxEmbedder` in a new
  `DocNest.Embeddings` assembly** so OnnxRuntime/Tokenizers stay behind the `IEmbedder` wrapper.

### 6. Test strategy (model is large)
- **Quantizer tests always run** (pure, no model): round-trip tolerances + stride/bytes parity.
- **`OnnxEmbedder` tests are gated** (`[SkippableFact]`, like the Slice-2 interop): skip-with-reason when
  the model/vocab files aren't present; run real inference when they are (determinism + cosine ordering).
  A `tools/get_minilm` (or `MiniLmModel.EnsureDownloaded`) fetches them on demand.

### 7. Backward-compat surface
- Additive: `Quantizer` in Core (new), new `DocNest.Embeddings` assembly + 2 MIT deps. No `.udf` schema
  change — but the quantiser **defines** the `embeddings.bin` byte layout that Slice 2 deferred; the
  Slice-2 wire DTOs/manifest already carry `embedding_dims`/`quantization`.

### 8. Open questions (resolve GATE 0 / Phase 2)
- Q1: **Split** — 6a (embeddings + quantizer) now, 6b (LLM providers + 5-layer answer engine) next? (Recommend.)
- Q2: **Model handling** — embedder takes file **paths** + an opt-in `EnsureDownloaded` helper (recommend),
  vs require the user to always provide paths, vs attempt a bundled model.
- Q3: tokenizer — `Microsoft.ML.Tokenizers` BERT vs a hand-rolled WordPiece (recommend the library).
- Q4: max sequence length / pooling (mean vs CLS) — MiniLM uses **mean pooling**; max length 256 (configurable).
