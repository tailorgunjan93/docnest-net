# DocNest .NET — Project Charter / North Star

> The single source of truth for *why* DocNest .NET exists and *what "done well" means*.
> Every task must trace back to this. A change that does not serve the charter is out of scope.
> Read alongside [DEVELOPMENT_PROTOCOL.md](DEVELOPMENT_PROTOCOL.md).
>
> This charter inherits the vision of the Python reference implementation
> (`D:\Learning\docnest`, `docnest-ai` on PyPI). The .NET port serves the **same**
> North Star for a different runtime — it does not invent a new product.

## Vision
The document normalization engine RAG has always needed: read a document's **structure**
before its content, so an LLM always receives the *right* context — never blind chunks.
Output a portable, self-contained `.udf` knowledge base that is **byte-compatible across
Python and .NET**.

## Who it's for
.NET / C# developers building RAG pipelines and tools (ASP.NET services, desktop apps,
Azure Functions) who want the DocNest engine natively on NuGet — no Python runtime, no
subprocess bridge. The `.udf` produced here must open in the Python `docnest` and vice versa.

## Decision filter (the motto)
Every decision is judged against: **Secure · Fast · Reliable · Cost-Effective.**
If a change weakens one of these without a deliberate, recorded trade-off, reconsider it.

## Success Metrics / KPIs (define "done well")
A change is only "green" if it holds ALL of these (see protocol GATE 5):
- **Compatibility (the headline metric for a port):** every `.udf` written by DocNest .NET
  opens in the Python `docnest` and round-trips; every `.udf` written by Python opens here.
  `UDF_VERSION` and the JSON schema are the cross-ecosystem contract and never silently drift.
- **Accuracy:** RAG accuracy on the shared doc/question suite **does not regress** versus the
  Python reference on the same inputs.
- **Reliability:** full test suite green; **0 escaped defects**; regression suite only grows.
- **Speed:** warm retrieval ~**1 ms/query**; ingestion predictable.
- **Memory:** large-document processing uses **bounded RAM** (scales with chunk size, not
  file size); no `OutOfMemoryException`.
- **Cost:** the majority of queries answered with **0 LLM tokens** (deterministic layers);
  token use per query does not regress.
- **Privacy:** core path (parse → normalise → embed) runs **fully local** via ONNX Runtime,
  no mandatory network calls. Cloud providers are opt-in only.

## Product-level non-goals (for now)
- Not a consumer GUI.
- No cryptographic provenance / signing in core yet.
- No cloud dependency in the core path.
- Not a re-design of the `.udf` format — `.udf` semantics are owned by `udf-spec`; .NET conforms.

## Idiomatic-.NET principles (how the port differs from a line-by-line transliteration)
The user-approved direction is an **idiomatic clean rewrite**, not a transliteration. That means:
- Immutable domain modelled as `record` types; collections exposed as read-only.
- Nullable reference types enabled; no `null` surprises across the public API.
- `System.Text.Json` source-generated (de)serialisation; `[JsonPropertyName]` preserves the
  exact `snake_case` keys of the `.udf` contract.
- Dependency Inversion via interfaces + constructor injection (`Microsoft.Extensions.*` friendly).
- `async`/`await` for all I/O and model inference; `CancellationToken` honoured.
- Every external library sits behind a DocNest wrapper interface — never called from core.

## Current mission
Stand up an idiomatic .NET DocNest that reaches `.udf` parity with Python, slice by slice,
without ever breaking the cross-ecosystem `.udf` contract.
