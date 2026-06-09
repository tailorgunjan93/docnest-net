using System.Collections.Generic;
using System.IO;
using DocNest;

namespace DocNest.Tests.Udf;

/// <summary>Shared sample document + temp-path helper for the .udf read/write tests.</summary>
internal static class UdfTestData
{
    public static string TempUdf()
        => Path.Combine(Path.GetTempPath(), "docnest-udf-" + System.Guid.NewGuid().ToString("N") + ".udf");

    /// <summary>A multi-section document with a table, an image, key numbers, metadata, and non-ASCII text.
    /// Source is a basename so <c>SourceSanitiser</c> is a no-op (keeps round-trip exact).</summary>
    public static Document Sample() => new()
    {
        DocId = "annual-report",
        Title = "Annual Report 2024 — Café Ünïcode",
        Source = "annual-report.pdf",
        Format = "pdf",
        Summary = "Overview of the year.",
        Insights = new[] { "Revenue grew", "Costs fell" },
        KeyNumbers = new[]
        {
            new KeyNumber { Label = "Revenue", Value = "$142M", Unit = "USD", Section = "§1" },
            new KeyNumber { Label = "Headcount", Value = "1200", Section = "§2" },
        },
        Meta = new DocMeta
        {
            Owner = "Finance",
            Department = "Corp",
            Tags = new[] { "annual", "report" },
            Version = "2.1",
            LastUpdated = "2025-04-22",
        },
        Sections = new[]
        {
            new Section
            {
                Id = "§1", Title = "Revenue §", Level = 1, Text = "Revenue was strong.",
                Summary = "Revenue summary", Keywords = new[] { "revenue" }, TokenCount = 7,
                Children = new[] { "§1.1" },
                Tables = new[]
                {
                    new TableData
                    {
                        TableId = "tbl_001", Caption = "By quarter", Headers = new[] { "Q", "Amount" },
                        Rows = new[]
                        {
                            (IReadOnlyList<string>)new[] { "Q1", "$30M" },
                            new[] { "Q2", "$40M" },
                        },
                    },
                },
            },
            new Section
            {
                Id = "§1.1", Title = "Details", Level = 2, Text = "Breakdown.", ParentId = "§1", TokenCount = 2,
                Images = new[] { new ImageRef { ImageId = "img_001", Alt = "chart", AssetPath = "assets/img_001.png" } },
            },
            new Section { Id = "§2", Title = "People", Level = 1, Text = "Headcount grew.", TokenCount = 3 },
        },
    };
}
