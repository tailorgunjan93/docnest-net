using System;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
// 'Document' alone resolves to the DocNest.Document domain record in this namespace.
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace DocNest.Parsers.Tests;

/// <summary>Builds a known <c>.docx</c> in a temp file (OpenXML SDK) for the docx parser tests.</summary>
internal static class DocxFixtureBuilder
{
    public static string Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "docnest-docx-" + Guid.NewGuid().ToString("N") + ".docx");

        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();

        var body = new Body();
        body.Append(Styled("Heading1", "Quarterly Report"));
        body.Append(Normal("Intro text."));
        body.Append(Styled("Heading2", "Numbers"));
        body.Append(Normal("See table."));
        body.Append(BuildTable());
        body.Append(Normal("SUMMARY"));   // ALL-CAPS pseudo-heading
        body.Append(Normal("All good."));

        main.Document = new WordDocument(body);
        main.Document.Save();
        return path;
    }

    private static Paragraph Styled(string styleId, string text)
        => new(new ParagraphProperties(new ParagraphStyleId { Val = styleId }), new Run(new Text(text)));

    private static Paragraph Normal(string text) => new(new Run(new Text(text)));

    private static Table BuildTable()
    {
        var table = new Table();
        table.Append(Row(Cell("Region"), Cell("Q1"), Cell("Q2")));
        table.Append(Row(VMergeRestart("North"), Cell("10"), Cell("20")));
        table.Append(Row(VMergeContinue(), Cell("30"), Cell("40")));
        table.Append(Row(GridSpan2("Total"), Cell("30")));
        return table;
    }

    private static TableRow Row(params TableCell[] cells)
    {
        var row = new TableRow();
        foreach (var cell in cells)
        {
            row.Append(cell);
        }
        return row;
    }

    private static TableCell Cell(string text) => new(new Paragraph(new Run(new Text(text))));

    private static TableCell VMergeRestart(string text)
        => new(new TableCellProperties(new VerticalMerge { Val = MergedCellValues.Restart }), new Paragraph(new Run(new Text(text))));

    private static TableCell VMergeContinue()
        => new(new TableCellProperties(new VerticalMerge()), new Paragraph(new Run(new Text(string.Empty))));

    private static TableCell GridSpan2(string text)
        => new(new TableCellProperties(new GridSpan { Val = 2 }), new Paragraph(new Run(new Text(text))));
}
