# Phase 0.2 — Dev / Technical Document
## Slice 4b: OpenXML parsers (docx + xlsx)

**Status:** Phase 0 (awaiting GATE 0) · **Owner:** Gunjan

---

### 1. Python reference — read end-to-end (files traced)
| Python file | Logic | Port target |
|---|---|---|
| `parsers/docx.py` | ordered body walk; `_HEADING_STYLES` + `heading N` regex; `_is_pseudo_heading` (ALL-CAPS / >50 % bold / colon label, ≤100 chars); List → `- `; pre-heading "Introduction"; table grid (python-docx already repeats merged cells) | `DocxParser` |
| `parsers/xlsx.py` | sheet → Section; `_split_into_tables` (header-after-numeric); `_looks_like_header`/`_is_mostly_numeric`/`_is_numeric`; merged title-row skip; `_build_table` width-norm; `_table_text_summary` | `ExcelParser` |

### 2. Dependency decision
- **`DocumentFormat.OpenXml`** (Microsoft, MIT) for **both** formats — one dependency, behind the two
  `IParser` wrappers; never referenced from `Core`. (Open Q1: keep docx+xlsx in one Slice 4b, or split.)

### 3. The two hard parts (where .NET differs from Python)
- **docx merged cells — must expand ourselves.** python-docx returns a *rectangular grid with merged
  values already repeated*; the OpenXML SDK does **not**. We must expand: horizontal `w:gridSpan`
  (repeat the value across N columns) and vertical `w:vMerge` (a `restart` cell's value repeats down
  into subsequent `continue` cells in the same column). This is the column-alignment guarantee (AC2)
  and the "complex tables" target — it gets dedicated tests.
- **xlsx cell reading — OpenXML is low-level.** Cells carry a type (`t="s"` → index into the
  **shared-strings** table; numbers inline in `<v>`; booleans; inline strings). Rows are **sparse**
  (cell `r="C2"` references skip empties). We read **cached values** (`data_only` equivalent — the
  `<v>` element), map shared strings, and densify each row by column letter so `_split_into_tables`
  sees Python-equivalent rows. (`SpreadsheetDocument` + `SharedStringTablePart` + `WorksheetPart`.)

### 4. Mapping notes
- **Ordered body walk (docx):** `body.Elements()` yields `Paragraph` and `Table` in order — directly
  mirrors `_iter_blocks_in_order`. Paragraph style = `p.ParagraphProperties?.ParagraphStyleId?.Val`;
  text = `p.InnerText`; bold runs = `run.RunProperties?.Bold` (+ `runProps` toggles); list = style id contains "List".
- **Heading map:** styleId/name → level. Word stores style **ids** (often `Heading1`, `Title`) — match
  both the id and the display name (`Heading 1`) and the `heading\s*N` regex. *Open Q2: match on style
  id vs name — resolve by reading the `styles.xml` name, or accept id-prefix matching.*
- **xlsx numeric test (`_is_numeric`):** strip commas, `double.TryParse` (invariant) — but cached `<v>`
  for numbers is already invariant text; booleans/dates handled as their cached string.
- Titles, `_filename_to_title`, width-norm reuse Slice-4 `ParserText`/the same helpers.

### 5. Layout (additive)
```
src/DocNest.Parsers/Office/DocxParser.cs   (+ internal OpenXmlDocxTable grid expander)
src/DocNest.Parsers/Office/ExcelParser.cs  (+ internal Xlsx row reader / table splitter)
tests/DocNest.Parsers.Tests/Office/…  + fixtures (sample.docx, sample.xlsx)
```
- Add `DocumentFormat.OpenXml` to `DocNest.Parsers` (centrally pinned). Register both in `ParserFactory`.
- **Fixtures:** small `.docx`/`.xlsx` are binary — generate them with a tiny committed generator
  (an OpenXML build helper or a one-off script), like the Slice-2 fixture approach, so they're reproducible.

### 6. Backward-compat surface
- Additive: new parsers + one dependency on `DocNest.Parsers`; no Slice-1/2/3 changes; no `.udf` change.
- `ParserFactory` gains two registrations (constructor) — a public-behaviour addition, not a break.

### 7. Open questions (resolve GATE 0 / Phase 2)
- Q1: **Scope** — docx + xlsx together in Slice 4b, or split into 4b-docx / 4b-xlsx? (Both Med; together
  shares the dependency + OpenXML helpers — recommend together unless you want smaller gates.)
- Q2: docx style matching — by style **id** (`Heading1`) vs resolved **name** (`Heading 1`). (Recommend
  matching both: id-prefix `Heading%d`/`Title`/`Subtitle` + the name regex.)
- Q3: xlsx `_split_into_tables` — port the full heuristic now, or start with **one table per sheet** and
  add multi-table splitting as a hardening follow-up? (Recommend full port for parity; flag risk.)
- Q4: fixture generation — committed binary fixtures via a small OpenXML generator vs hand-checked-in files.
