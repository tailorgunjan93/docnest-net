# Phase 0.2 — Dev / Technical Document
## Slice 3: Pipeline + Normaliser

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### 1. Python reference — read end-to-end (files traced)
| Python file | Logic | Port target |
|---|---|---|
| `normalizer.py` `SectionNormaliser.normalise` | §id via `counters[6]` + ancestor `stack` of `(raw_level, id, depth)`; compact depth; parent/child; `token_count = int(words*1.3)`; table row width-normalisation | `SectionNormaliser` |
| `key_numbers.py` | `_PATTERNS` (money/percent/duration/ratio/count) → `_NUM_RE`; `parse_number`; `_label_for` (inline `Label:` else trailing nouns); identifier/acronym/list-marker/year filters; `extract_key_numbers`; `enrich_key_numbers` (cap 64, no-op if set) | `KeyNumberExtractor` |
| `keywords.py` | `_STOP` set; `extract_keywords` (title terms first, then `freq + 0.1*len`); `enrich_keywords` (per-section no-op) | `KeywordExtractor` |
| `pipeline.py` `DocNestPipeline.process` | parse → normalise → (LLM enrich, skippable) → deterministic `enrich_key_numbers` + `enrich_keywords` → embed | `DocNestPipeline` |

### 2. Immutability strategy (the key idiomatic difference)
Python mutates `Section`/`Document` in place (assigns `section.id`, `section.keywords`, `doc.key_numbers`).
Our domain records are **immutable** (Slice 1). So each stage **produces new records** via `with`
expressions / re-construction:
- `SectionNormaliser` builds a fresh `IReadOnlyList<Section>` with assigned `Id`/`ParentId`/`Children`/
  `TokenCount` and width-normalised `Tables`, then `new Document { … Sections = … }`.
- `KeywordExtractor.Enrich(doc)` returns `doc with { Sections = sections.Select(s => s.Keywords.Count==0 ? s with { Keywords = Extract(s.Text, s.Title) } : s) }`.
- `KeyNumberExtractor.Enrich(doc)` returns `doc with { KeyNumbers = … }` when empty.

Because `children` is built as parents are discovered (forward references), normalisation uses a
**two-pass** or a mutable scratch map then freezes: pass 1 assigns ids + parent links + collects
child-id lists in a `Dictionary<string,List<string>>`; pass 2 materialises immutable `Section`s with
their `Children`. (Python appends to the parent in one pass because it mutates; we collect then freeze.)

### 3. Algorithms & complexity (DSA)
- **§id assignment:** single pass over sections, O(n); stack/counters O(depth ≤ 6) = O(1) extra; the
  child-collection map is O(n). Total **O(n) time, O(n) space** — optimal.
- **Key numbers:** O(total_text) regex scan per section; bounded by `max_numbers=64`. Pre-compiled
  `[GeneratedRegex]` (no per-call compile).
- **Keywords:** O(tokens) tokenise + `Counter` (dictionary) + a sort of distinct terms O(u log u),
  u = distinct tokens. Fine for section-sized text.
- **Memory:** bounded per document; no archive/stream concerns here.

### 4. Regex parity (must match Python exactly)
Port each pattern with `[GeneratedRegex]`:
- number alternation (money/percent/duration/ratio/count) — order matters (most-specific first);
- `_BULLET_LABEL`, `_LIST_MARKER`, `_IDENTIFIER_PREFIX`, `_INLINE_LABEL`, acronym check, keyword token
  pattern `[a-z0-9][a-z0-9\-]{2,}`. **.NET `\b`, `\d`, case handling must reproduce Python results** —
  verified by a shared case table (QA), not by eyeballing the regex.
- `parse_number`: trillion/billion/million/thousand multipliers + trailing `k`; invariant culture.

### 5. Layout (new, additive)
```
src/DocNest.Core/Pipeline/SectionNormaliser.cs
src/DocNest.Core/Intelligence/KeyNumberExtractor.cs
src/DocNest.Core/Intelligence/KeywordExtractor.cs
src/DocNest.Core/Pipeline/DocNestPipeline.cs
tests/DocNest.Core.Tests/Pipeline/…  Intelligence/…
```
- `DocNestPipeline` ctor injects an optional `IParser` (for `Process(filePath)`) and exposes
  `Process(RawDocument) → Document` (parser-free path used this slice). Normaliser + extractors are
  plain classes (no external deps); pure functions where possible.

### 6. Backward-compat surface
- No `.udf` schema change; no Slice-1/2 type changes. Additive only.
- The §id format (`§N.N`) is the contract consumed by Slice 2 catalogue/content and future retrieval —
  must match Python exactly (guarded by parity tests).

### 7. Open questions (resolve GATE 0 / Phase 2)
- Q1: Pipeline surface — `Process(RawDocument)` only this slice, plus a `ProcessAsync(string path)`
  overload that requires an injected `IParser` (throws a clear error if none)? (Recommend yes.)
- Q2: Namespaces — `DocNest.Pipeline` + `DocNest.Intelligence` vs all under `DocNest`? (Recommend the
  two sub-namespaces; watch for the type/namespace clash lesson from Slice 2 — no type named `Pipeline`/`Intelligence`.)
- Q3: Extractors as static helpers vs instances behind small interfaces (`IKeyNumberExtractor`,
  `IKeywordExtractor`) so the pipeline can swap them? (Recommend tiny interfaces for testability/OCP.)
- Q4: Confirm the `token_count` rounding (`int(words*1.3)` = truncation) and the `words = text.split()`
  whitespace semantics map to `Text.Split((char[])null, RemoveEmptyEntries).Length`.
