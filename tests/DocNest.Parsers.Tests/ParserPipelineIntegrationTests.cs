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

public class ParserPipelineIntegrationTests
{
    [Theory]
    [InlineData("sample.md")]
    [InlineData("sample.csv")]
    [InlineData("sample.html")]
    public async Task Parse_then_pipeline_then_udf_round_trips(string name)
    {
        var path = ParserFixtures.Path(name);
        var raw = await new ParserFactory().Get(path).ParseAsync(path);
        var doc = new DocNestPipeline().Process(raw);

        doc.Sections.Should().NotBeEmpty();
        doc.Sections.Should().OnlyContain(s => s.Id.StartsWith('§'));

        var udf = Path.Combine(Path.GetTempPath(), "docnest-parse-" + Guid.NewGuid().ToString("N") + ".udf");
        try
        {
            await new UdfWriter().WriteAsync(doc, udf, includeSourcePath: true);
            var pkg = await UdfReader.LoadAsync(udf);
            pkg.ToDocument().Should().BeEquivalentTo(doc);
        }
        finally
        {
            File.Delete(udf);
        }
    }

    [Fact]
    public async Task Pipeline_with_injected_factory_parser()
    {
        var path = ParserFixtures.Path("sample.md");
        var pipeline = new DocNestPipeline(new ParserFactory().Get(path));

        var doc = await pipeline.ProcessAsync(path);

        doc.Title.Should().Be("Annual Report");
        doc.Sections.Should().OnlyContain(s => s.Id.StartsWith('§'));
    }
}
