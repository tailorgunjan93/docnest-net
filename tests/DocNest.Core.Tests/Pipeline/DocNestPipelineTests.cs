using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocNest;
using DocNest.Pipeline;
using DocNest.Udf;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests.Pipeline;

public class DocNestPipelineTests
{
    [Fact]
    public void Process_normalises_and_enriches()
    {
        var raw = new RawDocument
        {
            DocId = "annual", Title = "Annual", Source = "annual.md", Format = "md",
            Sections = new[]
            {
                new Section { Id = "", Title = "Revenue", Level = 1, Text = "Revenue was $142M, up 23%." },
                new Section { Id = "", Title = "People", Level = 2, Text = "Headcount: 1200 staff." },
            },
        };

        var doc = new DocNestPipeline().Process(raw);

        doc.Sections.Select(s => s.Id).Should().Equal("§1", "§1.1");
        doc.Sections[1].ParentId.Should().Be("§1");
        doc.KeyNumbers.Should().Contain(k => k.Value == "$142M");
        doc.Sections[0].Keywords.Should().Contain("revenue");
        doc.Sections[0].TokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProcessAsync_uses_injected_parser()
    {
        var pipeline = new DocNestPipeline(new FakeParser());
        var doc = await pipeline.ProcessAsync("x.md");
        doc.Sections.Should().ContainSingle();
        doc.Sections[0].Id.Should().Be("§1");
    }

    [Fact]
    public async Task ProcessAsync_without_parser_throws()
    {
        var act = async () => await new DocNestPipeline().ProcessAsync("x.md");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Pipeline_output_round_trips_through_udf()
    {
        var raw = new RawDocument
        {
            DocId = "annual", Title = "Annual", Source = "annual.md", Format = "md",
            Sections = new[] { new Section { Id = "", Title = "Revenue", Level = 1, Text = "Revenue was $142M." } },
        };
        var doc = new DocNestPipeline().Process(raw);
        var path = Path.Combine(Path.GetTempPath(), "docnest-pipe-" + Guid.NewGuid().ToString("N") + ".udf");
        try
        {
            await new UdfWriter().WriteAsync(doc, path);
            var pkg = await UdfReader.LoadAsync(path);
            pkg.ToDocument().Should().BeEquivalentTo(doc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class FakeParser : IParser
    {
        public bool Supports(string path) => true;

        public Task<RawDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(new RawDocument
            {
                DocId = "d", Title = "t", Source = path, Format = "md",
                Sections = new[] { new Section { Id = "", Title = "Only", Level = 1, Text = "hi" } },
            });
    }
}
