# Phase 1 + Phase 2 — Impact/Risk & Design
## Slice 3: Pipeline + Normaliser

**Status:** Phases 1–2 (owner approved straight-through to GATE 5) · **Owner:** Gunjan
**ADR:** [ADR-0003](../adr/0003-pipeline-and-deterministic-intelligence.md)

---

## Phase 1 — Impact & Risk
**Blast radius:** additive only — new types in `DocNest.Core` under `DocNest.Pipeline` and
`DocNest.Intelligence`. No changes to Slice-1 domain, Slice-2 `.udf` layer, or the schema.

| Area | Change | Breaking? |
|---|---|---|
| `DocNest.Core/Pipeline` | `SectionNormaliser`, `DocNestPipeline` | No — new |
| `DocNest.Core/Intelligence` | `KeyNumberExtractor`, `KeywordExtractor` (static) | No — new |
| Tests | normaliser + extractor + pipeline + Slice-2 round-trip | No — new |

**Watch-items / mitigations:**
1. **§id parity** with Python (compact depth) — the contract the `.udf`/retrieval depend on.
   Mitigation: case-table tests (U1–U7) before code; §id drift = highest-severity regression.
2. **Regex parity** (`\b`, `\d`, alternation order, identifier/acronym/year filters). Mitigation:
   port patterns verbatim via `[GeneratedRegex]`; case tables U8–U13.
3. **Immutable rebuild correctness** (child links need a forward reference). Mitigation: two-pass —
   pass 1 assigns ids + collects child-id lists in a scratch map; pass 2 freezes `Section`s.
4. **`int(words*1.3)` + `str.split()` semantics.** Mitigation: `(int)(WordCount*1.3)` truncation +
   `Split((char[]?)null, RemoveEmptyEntries)`; tested incl. empty/multi-space.

**Risk = Low, Impact = Low.** Pure-CPU, zero external deps, additive.

## Phase 2 — Design
**DSA:** §id assignment O(n) time / O(n) space (counters O(1), child-map O(n)); key numbers
O(text) per section (pre-compiled regex), capped at 64; keywords O(tokens)+O(u log u) stable sort.
Memory bounded per document.

**SOLID / patterns:**
- **SRP** — normaliser (structure) vs extractors (intelligence) vs pipeline (composition).
- **Immutability** — every stage returns *new* records (`with`), never mutates (unlike Python).
- **DIP** — `DocNestPipeline` injects an optional `IParser`; `Process(RawDocument)` is the
  parser-free path used this slice. Deterministic extractors are **pure static helpers** (no state,
  no deps) — a Strategy interface is YAGNI until a second algorithm exists (ADR-0003).

**Wrapper boundaries:** only the BCL (`System.Text.RegularExpressions` via `[GeneratedRegex]`); no
external module to wrap.

**Code plan (signatures):**
- `DocNest.Pipeline/SectionNormaliser.cs` — `Document Normalise(RawDocument raw)`.
- `DocNest.Intelligence/KeyNumberExtractor.cs` — `static IReadOnlyList<KeyNumber> Extract(string text, string sectionId)`;
  `static double? ParseNumber(string raw)`; `static Document Enrich(Document doc, int maxNumbers = 64)`.
- `DocNest.Intelligence/KeywordExtractor.cs` — `static IReadOnlyList<string> Extract(string text, string title = "", int k = 8)`;
  `static Document Enrich(Document doc, int k = 8)`.
- `DocNest.Pipeline/DocNestPipeline.cs` — ctor `(IParser? parser = null)`; `Document Process(RawDocument raw)`;
  `Task<Document> ProcessAsync(string filePath, CancellationToken ct = default)` (throws if no parser).

**Resolved open questions:** Q1 → both `Process(RawDocument)` and `ProcessAsync(path)` (needs parser);
Q2 → namespaces `DocNest.Pipeline` + `DocNest.Intelligence` (no type named Pipeline/Intelligence);
Q3 → **static pure helpers** (not interfaces) — YAGNI; Q4 → truncating `(int)`, whitespace `Split`.
