using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Parsers.Tests;

public class ExcelParserTests
{
    [Fact]
    public async Task Each_data_sheet_becomes_a_section_empty_skipped()
    {
        var path = XlsxFixtureBuilder.Create();
        try
        {
            var raw = await new ExcelParser().ParseAsync(path);
            raw.Sections.Select(s => s.Title).Should().Equal("Sales", "Notes");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Splits_a_sheet_into_logical_tables()
    {
        var path = XlsxFixtureBuilder.Create();
        try
        {
            var raw = await new ExcelParser().ParseAsync(path);
            var sales = raw.Sections.First(s => s.Title == "Sales");

            sales.Tables.Should().HaveCount(2);
            sales.Tables[0].Headers.Should().Equal("Year", "Revenue", "Profit");
            sales.Tables[0].Rows[0].Should().Equal("2021", "100", "10");
            sales.Tables[1].Headers.Should().Equal("Region", "Sales");
            sales.Tables[1].Rows[0].Should().Equal("North", "300");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Xls_is_rejected_with_clear_message()
    {
        var path = Path.Combine(Path.GetTempPath(), "x-" + Guid.NewGuid().ToString("N") + ".xls");
        await File.WriteAllTextAsync(path, "not really an xls");
        try
        {
            var act = async () => await new ExcelParser().ParseAsync(path);
            await act.Should().ThrowAsync<ParseException>().WithMessage("*xls*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Supports_xlsx_and_xls()
    {
        var parser = new ExcelParser();
        parser.Supports("a.xlsx").Should().BeTrue();
        parser.Supports("a.xls").Should().BeTrue();
        parser.Supports("a.md").Should().BeFalse();
    }
}
