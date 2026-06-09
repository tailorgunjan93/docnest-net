using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocNest;
using DocNest.Pipeline;
using DocNest.Udf;
using FluentAssertions;
using Xunit;

namespace DocNest.Parsers.Tests;

public class PdfParserTests
{
    [Fact]
    public async Task Detects_headings_by_font_size()
    {
        var path = PdfFixtureBuilder.Create();
        try
        {
            var raw = await new PdfParser().ParseAsync(path);

            raw.Title.Should().Be("Quarterly Report");
            raw.Sections.Select(s => (s.Title, s.Level)).Should().Equal(
                ("Quarterly Report", 1), ("Revenue", 2), ("Details", 3));
            raw.Sections[1].Text.Should().Contain("Revenue grew strongly");
            raw.Sections.Should().OnlyContain(s => s.Id == "");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Supports_pdf_only()
    {
        var parser = new PdfParser();
        parser.Supports("a.pdf").Should().BeTrue();
        parser.Supports("a.docx").Should().BeFalse();
    }

    [Fact]
    public async Task Missing_pdf_throws()
    {
        var missing = Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid().ToString("N") + ".pdf");
        var act = async () => await new PdfParser().ParseAsync(missing);
        await act.Should().ThrowAsync<ParseException>();
    }

    [Fact]
    public async Task Pdf_via_factory_through_pipeline_and_udf_round_trips()
    {
        var path = PdfFixtureBuilder.Create();
        var udf = Path.Combine(Path.GetTempPath(), "docnest-pdf-udf-" + Guid.NewGuid().ToString("N") + ".udf");
        try
        {
            var raw = await new ParserFactory().Get(path).ParseAsync(path);
            var doc = new DocNestPipeline().Process(raw);

            doc.Sections.Should().OnlyContain(s => s.Id.StartsWith('§'));

            await new UdfWriter().WriteAsync(doc, udf, includeSourcePath: true);
            var pkg = await UdfReader.LoadAsync(udf);
            pkg.ToDocument().Should().BeEquivalentTo(doc);
        }
        finally
        {
            File.Delete(path);
            File.Delete(udf);
        }
    }
}
