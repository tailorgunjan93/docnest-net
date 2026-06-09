using System.Linq;
using System.Threading.Tasks;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Parsers.Tests;

public class HtmlParserTests
{
    private static Task<RawDocument> Parse() => new HtmlParser().ParseAsync(ParserFixtures.Path("sample.html"));

    [Fact]
    public async Task Title_from_title_tag()
        => (await Parse()).Title.Should().Be("Quarterly");

    [Fact]
    public async Task Walks_heading_hierarchy()
    {
        var raw = await Parse();
        raw.Sections.Select(s => (s.Title, s.Level)).Should().Equal(("Quarterly Report", 1), ("Numbers", 2));
        raw.Sections[0].Text.Should().Contain("Intro text.");
    }

    [Fact]
    public async Task Extracts_table_with_rowspan_grid()
    {
        var raw = await Parse();
        var table = raw.Sections[1].Tables.Should().ContainSingle().Subject;
        table.Caption.Should().Be("By region");
        table.Headers.Should().Equal("Region", "Q1", "Q2");
        table.Rows[0].Should().Equal("North", "10", "20");
        table.Rows[1].Should().Equal("North", "30", "40");
    }

    [Fact]
    public void Supports_html_and_htm()
    {
        var parser = new HtmlParser();
        parser.Supports("x.html").Should().BeTrue();
        parser.Supports("x.htm").Should().BeTrue();
        parser.Supports("x.md").Should().BeFalse();
    }
}
