using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocNest;
using FluentAssertions;
using Xunit;

namespace DocNest.Tests;

/// <summary>F1/F2 — the five wrapper interfaces are implementable and substitutable by trivial fakes.</summary>
public class ContractFakeTests
{
    private sealed class FakeEmbedder : IEmbedder
    {
        public int Dims => 3;
        public string ModelName => "fake-emb";
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[] { 0f, 0f, 0f }).ToList());
    }

    private sealed class FakeParser : IParser
    {
        public bool Supports(string path) => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
        public Task<RawDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(new RawDocument { DocId = "d", Title = "t", Source = path, Format = "txt" });
    }

    private sealed class FakeLlm : ILlmProvider
    {
        public string ProviderName => "fake";
        public string ModelName => "fake-1";
        public Task<string> CompleteAsync(string prompt, string system = "", double temperature = 0.1, int maxTokens = 512, CancellationToken cancellationToken = default)
            => Task.FromResult("ok");
    }

    private sealed class FakeStorage : IStorageBackend
    {
        public string BackendName => "fake";
        public Task<string> WriteArchiveAsync(IReadOnlyDictionary<string, byte[]> entries, string outputPath, CancellationToken cancellationToken = default)
            => Task.FromResult(outputPath);
        public Task<byte[]> ReadEntryAsync(string archivePath, string name, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());
        public Task<IReadOnlyList<string>> ListEntriesAsync(string archivePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class FakeSearch : ISearchProvider
    {
        public string BackendName => "fake";
        public void BuildIndex(IReadOnlyList<IReadOnlyList<string>> corpus) { }
        public IReadOnlyList<double> GetScores(IReadOnlyList<string> queryTokens) => Array.Empty<double>();
    }

    [Fact]
    public async Task All_interfaces_are_implementable_and_substitutable()
    {
        IEmbedder embedder = new FakeEmbedder();
        (await embedder.EmbedAsync(new[] { "a", "b" })).Should().HaveCount(2);
        embedder.Dims.Should().Be(3);
        embedder.ModelName.Should().Be("fake-emb");

        IParser parser = new FakeParser();
        parser.Supports("x.txt").Should().BeTrue();
        (await parser.ParseAsync("x.txt")).Format.Should().Be("txt");

        ILlmProvider llm = new FakeLlm();
        (await llm.CompleteAsync("hi")).Should().Be("ok");
        llm.ProviderName.Should().Be("fake");

        IStorageBackend storage = new FakeStorage();
        storage.BackendName.Should().Be("fake");
        (await storage.ListEntriesAsync("a.udf")).Should().BeEmpty();

        ISearchProvider search = new FakeSearch();
        search.BuildIndex(new[] { (IReadOnlyList<string>)new[] { "a" } });
        search.GetScores(new[] { "q" }).Should().BeEmpty();
    }
}
