using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocNest;
using DocNest.Retrieval;
using FluentAssertions;
using Xunit;

namespace DocNest.Retrieval.Tests;

public class HybridRetrieverTests
{
    [Fact]
    public async Task Bm25_ranks_matching_section_first()
    {
        await Run(async (dir, _) =>
        {
            var retriever = new HybridRetriever(dir);
            var doc = Doc(
                ("§1", "Introduction", "overview of the document", null),
                ("§2", "Background", "history and context", null),
                ("§3", "Carbon Budget", "the carbon budget limits total emissions", null),
                ("§4", "Conclusion", "summary remarks", null));
            try
            {
                var hits = await retriever.RetrieveAsync(doc, "carbon budget emissions", 3);
                hits.Should().NotBeEmpty();
                hits[0].Section.Id.Should().Be("§3");
            }
            finally
            {
                retriever.Dispose();
            }
        });
    }

    [Fact]
    public async Task Dense_signal_contributes_with_embedder()
    {
        await Run(async (dir, _) =>
        {
            var embedder = new FakeEmbedder("carbon", "emissions", "budget", "history", "revenue");
            var retriever = new HybridRetriever(dir, embedder);
            var doc = Doc(
                ("§1", "Intro", "general overview", null),
                ("§2", "Emissions", "carbon emissions and budget figures", null),
                ("§3", "History", "company history", null));
            try
            {
                var hits = await retriever.RetrieveAsync(doc, "carbon budget", 2);
                hits[0].Section.Id.Should().Be("§2");
                retriever.IsCached(doc).Should().BeTrue();
                retriever.Stats(doc.DocId).Indexed.Should().BeTrue();
            }
            finally
            {
                retriever.Dispose();
            }
        });
    }

    [Fact]
    public async Task Graph_expansion_surfaces_child_of_a_matched_parent()
    {
        await Run(async (dir, _) =>
        {
            var retriever = new HybridRetriever(dir);
            var doc = Doc(
                ("§1", "Carbon Policy", "carbon policy overview", null),
                ("§1.1", "Targets", "specific reduction milestones", "§1"),
                ("§2", "Finance", "quarterly finance numbers", null),
                ("§3", "People", "staff and culture", null),
                ("§4", "Legal", "compliance matters", null));
            try
            {
                var hits = await retriever.RetrieveAsync(doc, "carbon policy", 2);
                hits.Select(h => h.Section.Id).Should().Contain("§1.1");
            }
            finally
            {
                retriever.Dispose();
            }
        });
    }

    [Fact]
    public async Task Cache_hits_on_unchanged_doc_and_invalidates_on_change()
    {
        await Run(async (dir, _) =>
        {
            var retriever = new HybridRetriever(dir);
            var doc = Doc(("§1", "A", "alpha text here", null), ("§2", "B", "beta content", null));
            try
            {
                await retriever.RetrieveAsync(doc, "alpha", 2);
                retriever.IsCached(doc).Should().BeTrue();
                var builtAt = retriever.Stats(doc.DocId).BuiltAt;

                await retriever.RetrieveAsync(doc, "beta", 2);
                retriever.Stats(doc.DocId).BuiltAt.Should().Be(builtAt); // no rebuild

                var changed = doc with
                {
                    Sections = doc.Sections.Append(new Section { Id = "§3", Title = "C", Level = 1, Text = "gamma" }).ToList(),
                };
                retriever.IsCached(changed).Should().BeFalse();
            }
            finally
            {
                retriever.Dispose();
            }
        });
    }

    [Fact]
    public async Task Works_without_embedder()
    {
        await Run(async (dir, _) =>
        {
            var retriever = new HybridRetriever(dir);
            var doc = Doc(
                ("§1", "Carbon", "carbon budget details", null),
                ("§2", "Other", "unrelated material", null));
            try
            {
                var hits = await retriever.RetrieveAsync(doc, "carbon budget", 2);
                hits.Should().NotBeEmpty();
            }
            finally
            {
                retriever.Dispose();
            }
        });
    }

    [Fact]
    public async Task Special_characters_in_query_do_not_throw()
    {
        await Run(async (dir, _) =>
        {
            var retriever = new HybridRetriever(dir);
            var doc = Doc(("§1", "A", "carbon budget", null));
            try
            {
                var act = async () => await retriever.RetrieveAsync(doc, "carbon: budget* (test) \"x\"", 2);
                await act.Should().NotThrowAsync();
            }
            finally
            {
                retriever.Dispose();
            }
        });
    }

    // ---- helpers ----

    private static Document Doc(params (string Id, string Title, string Text, string? Parent)[] sections) => new()
    {
        DocId = "d",
        Title = "t",
        Source = "s",
        Format = "md",
        Sections = sections.Select(x => new Section { Id = x.Id, Title = x.Title, Level = 1, Text = x.Text, ParentId = x.Parent }).ToList(),
    };

    private static async Task Run(Func<string, object?, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "docnest-ret-" + Guid.NewGuid().ToString("N"));
        try
        {
            await body(dir, null);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    private sealed class FakeEmbedder : IEmbedder
    {
        private readonly string[] _vocab;
        public FakeEmbedder(params string[] vocab) => _vocab = vocab;

        public int Dims => _vocab.Length;
        public string ModelName => "fake-bow";

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<float[]> result = texts.Select(text =>
            {
                var tokens = text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var vector = new float[_vocab.Length];
                for (var i = 0; i < _vocab.Length; i++)
                {
                    vector[i] = tokens.Count(t => t.Trim('.', ',', ':', '!', '?', '(', ')') == _vocab[i]);
                }
                return vector;
            }).ToList();
            return Task.FromResult(result);
        }
    }
}
