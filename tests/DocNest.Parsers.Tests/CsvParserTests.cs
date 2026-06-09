using System.Threading.Tasks;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Parsers.Tests;

public class CsvParserTests
{
    private static async Task<TableData> Table(string name)
    {
        var raw = await new CsvParser().ParseAsync(ParserFixtures.Path(name));
        return raw.Sections[0].Tables[0];
    }

    [Fact]
    public async Task Parses_headers_and_quoted_fields()
    {
        var table = await Table("sample.csv");
        table.Headers.Should().Equal("Name", "Amount", "Note");
        table.Rows[0].Should().Equal("Alice", "1,200", "says \"hi\"");
    }

    [Fact]
    public async Task Pads_short_and_truncates_long_rows()
    {
        var table = await Table("sample.csv");
        table.Rows[1].Should().Equal("Bob", "800", "");
        table.Rows[2].Should().Equal("Carol", "500", "extra");
    }

    [Fact]
    public void Supports_csv_and_tsv()
    {
        var parser = new CsvParser();
        parser.Supports("x.csv").Should().BeTrue();
        parser.Supports("x.tsv").Should().BeTrue();
        parser.Supports("x.md").Should().BeFalse();
    }
}
