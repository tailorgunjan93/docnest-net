using System.Linq;
using System.Threading.Tasks;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Parsers.Tests;

public class MarkdownParserTests
{
    private static Task<RawDocument> Parse(string name) => new MarkdownParser().ParseAsync(ParserFixtures.Path(name));

    [Fact]
    public async Task Parses_headings_title_and_ignores_fenced_code()
    {
        var raw = await Parse("sample.md");
        raw.Title.Should().Be("Annual Report");

        var titles = raw.Sections.Select(s => s.Title).ToList();
        titles.Should().Contain("Annual Report");
        titles.Should().Contain("Revenue");
        titles.Should().Contain("Costs");
        titles.Should().NotContain(t => t.Contains("this is code"));
    }

    [Fact]
    public async Task Revenue_is_level_2_and_ids_unassigned()
    {
        var raw = await Parse("sample.md");
        raw.Sections.First(s => s.Title == "Revenue").Level.Should().Be(2);
        raw.Sections.Should().OnlyContain(s => s.Id == "");
    }

    [Fact]
    public void Supports_md_and_markdown()
    {
        var parser = new MarkdownParser();
        parser.Supports("x.md").Should().BeTrue();
        parser.Supports("x.markdown").Should().BeTrue();
        parser.Supports("x.txt").Should().BeFalse();
    }
}
