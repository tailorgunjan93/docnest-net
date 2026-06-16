# Contributing to DocNest .NET

Thanks for your interest — **DocNest .NET is built in the open and welcomes contributors at every level.**
Whether you're fixing a typo, adding a parser, or wiring a new embedder, you're in the right place.

## Quick start

```bash
git clone https://github.com/tailorgunjan93/docnest-net
cd docnest-net
dotnet build DocNest.sln -c Debug
dotnet test  DocNest.sln          # the full xUnit suite should be green before you change anything
```

- **.NET 8 SDK** is the only prerequisite.
- The ONNX model tests are `[SkippableFact]` — they skip automatically unless you've provisioned the
  MiniLM / ms-marco models locally, so a fresh clone runs green out of the box.

## Where to help

| Area | Good for |
|---|---|
| 🧩 **New parser** — PPTX, EPUB, JSON/JSONL, RTF, LaTeX | familiarity with a document format; implement `IParser`, register it in `ParserFactory` |
| ☁️ **Cloud embedder** — OpenAI, Cohere, Google, Azure | API integration; implement `IEmbedder` (mirrors the Python providers) |
| 🔄 **Reranker / retrieval** improvements | search / IR experience |
| 🔌 **Vector backend / connector** | infra experience |
| 🧪 **Test fixtures** — sample documents, edge cases | any skill level |
| 📖 **Documentation** — examples, clarifications | any skill level |
| 🐛 **Bug reports** | any skill level — try it, break it, [open an issue](https://github.com/tailorgunjan93/docnest-net/issues) |
| 💡 **Architecture discussion** | open a [Discussion](https://github.com/tailorgunjan93/docnest-net/discussions) |

Issues labelled **`good first issue`** are a great place to start. Not sure where to begin? Open a
Discussion and say hi.

## How we work — the gated protocol

DocNest is built slice-by-slice under a **mandatory gated protocol**
([docs/DEVELOPMENT_PROTOCOL.md](docs/DEVELOPMENT_PROTOCOL.md)). In short, every change:

1. **Understands first** — the *why* / *what* / *how* before code.
2. **Is tested first** — unit + integration tests, written before the fix, failing for the right reason.
3. **Runs the full suite every cycle** — regression-first; the suite only grows.
4. **Stays behind wrappers** — every external dependency sits behind a DocNest interface.
5. **Never breaks the contract** — the `.udf` archive layout, `UDF_VERSION`, and the public API are
   sacred. Breaking changes need an ADR + a version bump.

You don't have to write the formal Phase-0 docs for a small fix — but **tests and a green suite are
non-negotiable**, and larger features should open an issue/Discussion first so we can agree on the shape.

## Pull request checklist

- [ ] Branched from `main` (never commit to `main` directly).
- [ ] `dotnet test DocNest.sln` is **green** (new tests added for new behaviour).
- [ ] Code matches the surrounding style (the repo enables analyzers + nullable; warnings-as-errors).
- [ ] No change to `.udf` / `UDF_VERSION` / public API without an ADR + note in the PR.
- [ ] `CHANGELOG.md` updated for user-visible changes.

Open the PR against `main` with a clear description of the *why*. We review, iterate, and merge on green.

## Reporting bugs

Open an [issue](https://github.com/tailorgunjan93/docnest-net/issues) with: the input (format + a minimal
sample if possible), what you expected, what happened, and your OS / .NET version. Retrieval/parsing
quirks are especially welcome — they make the engine more robust for everyone.

## Code of conduct

Be kind, be constructive, assume good intent. We're here to build something useful together.

---

**Give the repo a ⭐ if DocNest solves a problem for you — it's the single best way to help others find it.**
