# ADR-0003 — Immutable normalisation pipeline + static deterministic intelligence

- **Status:** Accepted (owner approved straight-through to GATE 5)
- **Date:** 2026-06-09
- **Context slice:** Slice 3 — pipeline + normaliser
- **Builds on:** ADR-0001 (immutable domain)

## Context
Python's `SectionNormaliser`, `key_numbers.py`, and `keywords.py` mutate `Section`/`Document` in
place. Our domain records are immutable. We also need the §id format and the deterministic extractor
outputs to match Python exactly, since they feed the `.udf` and (later) retrieval.

## Decision
1. **Stages return new records** (`with` / re-construction); no mutation. `SectionNormaliser` uses a
   **two-pass** algorithm: pass 1 assigns §ids/parent links and collects child-id lists in a scratch
   `Dictionary<string,List<string>>`; pass 2 materialises immutable `Section`s with frozen `Children`.
2. **Deterministic extractors are static pure helpers** (`KeyNumberExtractor`, `KeywordExtractor`) —
   no state, no external deps. A `Strategy` interface is deferred (YAGNI) until a second algorithm
   exists; promoting them later is non-breaking (add an interface, keep the statics).
3. **Regexes via `[GeneratedRegex]`**, ported verbatim from Python (alternation order preserved) for
   parity and zero per-call compilation.
4. **Pipeline** injects an optional `IParser`: `Process(RawDocument)` is the parser-free path (this
   slice); `ProcessAsync(path)` requires an injected parser (Slice 4) and throws a clear error otherwise.
5. **Namespaces** `DocNest.Pipeline` + `DocNest.Intelligence`, with no type named `Pipeline`/
   `Intelligence` (the Slice-2 type/namespace-clash lesson).
6. **Numeric semantics:** `token_count = (int)(wordCount * 1.3)` (truncation, matching Python `int()`);
   `wordCount` via whitespace `Split(RemoveEmptyEntries)` (matching `str.split()`).

## Consequences
**Positive:** thread-safe, side-effect-free stages; faithful §id/extractor parity; pure functions are
trivially unit-tested.
**Negative / cost:** the two-pass + record re-construction allocates more than in-place mutation
(acceptable at document scale); regex parity must be guarded by case tables, not inspection.
**Neutral:** static extractors mean no DI seam yet for swapping algorithms — revisited only if needed.

## Alternatives considered
- *Mutable Section (like Python):* simplest port but abandons the immutable-domain decision (ADR-0001)
  and its thread-safety/value-equality benefits. Rejected.
- *Extractor interfaces now:* more ceremony for a single implementation; deferred (YAGNI).
- *One-pass child links with a builder:* possible, but the two-pass with a scratch map is clearer and
  still O(n). Chosen for readability.
