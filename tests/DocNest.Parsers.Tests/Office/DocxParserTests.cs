using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Parsers.Tests;

public class DocxParserTests
{
    [Fact]
    public async Task Parses_headings_in_order_with_pseudo_heading()
    {
        var path = DocxFixtureBuilder.Create();
        try
        {
            var raw = await new DocxParser().ParseAsync(path);

            raw.Title.Should().Be("Quarterly Report");
            raw.Sections.Select(s => (s.Title, s.Level)).Should().Equal(
                ("Quarterly Report", 1), ("Numbers", 2), ("SUMMARY", 1));
            raw.Sections[0].Text.Should().Contain("Intro text.");
            raw.Sections[2].Text.Should().Contain("All good.");
            raw.Sections.Should().OnlyContain(s => s.Id == "");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Expands_merged_table_cells()
    {
        var path = DocxFixtureBuilder.Create();
        try
        {
            var raw = await new DocxParser().ParseAsync(path);

            var table = raw.Sections[1].Tables.Should().ContainSingle().Subject;
            table.Headers.Should().Equal("Region", "Q1", "Q2");
            table.Rows[0].Should().Equal("North", "10", "20");
            table.Rows[1].Should().Equal("North", "30", "40");   // vMerge value copied down
            table.Rows[2].Should().Equal("Total", "Total", "30"); // gridSpan value repeated
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Supports_docx_only()
    {
        var parser = new DocxParser();
        parser.Supports("a.docx").Should().BeTrue();
        parser.Supports("a.doc").Should().BeFalse();
    }
}
