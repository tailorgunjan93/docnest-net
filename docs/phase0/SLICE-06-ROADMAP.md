# Phase 0.4 — Roadmap: Slice 6a (Embeddings + Quantizer)

> Consolidates the Slice-6a BA/Dev/QA. Two MIT deps (`Microsoft.ML.OnnxRuntime`,
> `Microsoft.ML.Tokenizers`) behind the `OnnxEmbedder` wrapper; the `Quantizer` is pure/zero-dep in
> `DocNest.Core`. **Slice split: 6a (embeddings + quantizer) → 6b (LLM providers + 5-layer answer engine).**

## Ordered steps (what GATE 0 approves entering Phase 1 for)
1. **Phase 1 (Impact & risk):** new `DocNest.Embeddings` assembly + 2 ONNX deps; `Quantizer` added to
   Core. Watch-items: `embeddings.bin` byte-layout parity (the `.udf` contract); ONNX model/vocab handling;
   tokenizer correctness; bounded-memory batching. Risk Med (ONNX/model) → Low by isolating `Quantizer`
   (pure, fully tested) and gating the model-dependent tests.
2. **Phase 2 (Design + ADR-0008):** resolve Q1–Q4 (split; model-paths + opt-in download; library
   tokenizer; mean-pool/max-len); the embedder pipeline; quantizer byte layout; file-by-file plan.
3. **Phase 3 (Test-first):** `Quantizer` U1–U6 (always run) + gated `OnnxEmbedder` E1–E4 + I1–I2;
   `MiniLmModel.EnsureDownloaded`/`tools/get_minilm`. Failing first.
4. **Phase 4 (Implement):** `Quantizer` (Core), `OnnxEmbedder` + `MiniLmModel` (new assembly).
5. **Phase 5 (Verify):** full suite green; Quantizer parity holds; gated ONNX tests pass when the model
   is present (else skip-with-reason); `.udf` embeddings round-trip.
6. **Phase 6:** defects → regression + unit tests then fix (layout/tokenizer edge cases).
7. **Phase 7:** branch `slice/06-embeddings` → green → merge `main`; CHANGELOG; tag `v0.0.8-embeddings`.

**Risk/impact expectation:** Med → Low — the pure `Quantizer` (the `.udf` contract piece) is fully
tested without a model; the ONNX embedder is isolated behind `IEmbedder` and its tests gate on the model.

## Decisions needed from owner at GATE 0
- Approve the four Phase 0 documents **and** the **6a/6b split** (embeddings now, LLM+answer engine next).
- Q2: model handling — file paths + opt-in `EnsureDownloaded` helper (recommend) vs always-provided paths.
- Q3 (tokenizer library) can defer to ADR-0008.
