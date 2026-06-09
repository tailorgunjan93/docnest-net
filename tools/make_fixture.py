"""Generate the golden `.udf` interop fixture for the DocNest .NET tests.

Run this in a Python environment where the Python `docnest` package is importable
(e.g. the `D:\\Learning\\docnest` project's venv):

    python tools/make_fixture.py

It writes `tests/fixtures/sample.udf`. Commit that file. The .NET test
`InteropFixtureTests.E1_loads_python_produced_golden_udf` then activates (it
auto-skips while the fixture is absent).

The document mirrors `UdfTestData.Sample()` on the .NET side so both runtimes
exercise the same structure (multi-section hierarchy, a table, an image,
key numbers, metadata, and non-ASCII text). No embedder is used, so the archive
has `embedding_dims: 0` and no `embeddings.bin`.
"""

from pathlib import Path

from docnest.models import Document, Section, TableData, ImageRef, KeyNumber, DocMeta
from docnest.writer import UDFWriter


def build_doc() -> Document:
    return Document(
        doc_id="annual-report",
        title="Annual Report 2024 — Café Ünïcode",
        source="annual-report.pdf",
        format="pdf",
        summary="Overview of the year.",
        insights=["Revenue grew", "Costs fell"],
        key_numbers=[
            KeyNumber(label="Revenue", value="$142M", unit="USD", section="§1"),
            KeyNumber(label="Headcount", value="1200", section="§2"),
        ],
        meta=DocMeta(
            owner="Finance",
            department="Corp",
            tags=["annual", "report"],
            version="2.1",
            last_updated="2025-04-22",
        ),
        sections=[
            Section(
                id="§1", title="Revenue §", level=1, text="Revenue was strong.",
                summary="Revenue summary", keywords=["revenue"], token_count=7,
                children=["§1.1"],
                tables=[TableData(
                    table_id="tbl_001", caption="By quarter",
                    headers=["Q", "Amount"], rows=[["Q1", "$30M"], ["Q2", "$40M"]],
                )],
            ),
            Section(
                id="§1.1", title="Details", level=2, text="Breakdown.",
                parent_id="§1", token_count=2,
                images=[ImageRef(image_id="img_001", alt="chart", asset_path="assets/img_001.png")],
            ),
            Section(id="§2", title="People", level=1, text="Headcount grew.", token_count=3),
        ],
    )


def main() -> None:
    out = Path(__file__).resolve().parents[1] / "tests" / "fixtures" / "sample.udf"
    out.parent.mkdir(parents=True, exist_ok=True)
    # embedder=None → no embeddings.bin, embedding_dims 0 (Slice-2 scope).
    UDFWriter(embedder=None).write(build_doc(), str(out), include_source_path=False)
    print(f"wrote {out}")


if __name__ == "__main__":
    main()
