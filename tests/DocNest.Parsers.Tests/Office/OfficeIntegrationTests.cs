using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocNest.Pipeline;
using DocNest.Udf;
using FluentAssertions;
using Xunit;

namespace DocNest.Parsers.Tests;

public class OfficeIntegrationTests
{
    [Fact]
    public async Task Docx_via_factory_through_pipeline_and_udf_round_trips()
    {
        var path = DocxFixtureBuilder.Create();
        var udf = Path.Combine(Path.GetTempPath(), "docnest-office-" + Guid.NewGuid().ToString("N") + ".udf");
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

    [Fact]
    public async Task Xlsx_via_factory_parses_to_sections()
    {
        var path = XlsxFixtureBuilder.Create();
        try
        {
            var raw = await new ParserFactory().Get(path).ParseAsync(path);
            var doc = new DocNestPipeline().Process(raw);
            doc.Sections.Select(s => s.Title).Should().Contain("Sales");
            doc.Sections.Should().OnlyContain(s => s.Id.StartsWith('§'));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
