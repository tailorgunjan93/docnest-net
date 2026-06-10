using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocNest.Embeddings;
using DocNest.Quantization;
using FluentAssertions;
using Xunit;

namespace DocNest.Embeddings.Tests;

/// <summary>
/// Real-inference tests for <see cref="OnnxEmbedder"/>. The ~90 MB MiniLM model is not committed, so
/// these are <see cref="SkippableFact"/> — they skip with a clear reason when the model is absent and
/// run real inference when present (provision via <c>MiniLmModel.EnsureDownloadedAsync</c>).
/// </summary>
public class OnnxEmbedderTests
{
    private static string CacheDir => Path.Combine(AppContext.BaseDirectory, "minilm-cache");

    [SkippableFact]
    public async Task Produces_normalised_384d_deterministic_vectors()
    {
        Skip.IfNot(
            MiniLmModel.IsPresent(CacheDir),
            $"MiniLM model not present in {CacheDir}. Run MiniLmModel.EnsureDownloadedAsync(cacheDir) to enable ONNX embedder tests.");

        var (modelPath, vocabPath) = MiniLmModel.Paths(CacheDir);
        using var embedder = new OnnxEmbedder(modelPath, vocabPath);

        var vectors = await embedder.EmbedAsync(new[] { "carbon emissions budget", "carbon emissions budget" });

        vectors.Should().HaveCount(2);
        vectors[0].Should().HaveCount(384);
        vectors[0].Should().Equal(vectors[1]); // deterministic
        Quantizer.CosineSimilarity(vectors[0], vectors[0]).Should().BeApproximately(1.0, 1e-4);
    }

    [SkippableFact]
    public async Task Similar_texts_are_more_similar_than_dissimilar()
    {
        Skip.IfNot(MiniLmModel.IsPresent(CacheDir), "MiniLM model not present.");

        var (modelPath, vocabPath) = MiniLmModel.Paths(CacheDir);
        using var embedder = new OnnxEmbedder(modelPath, vocabPath);

        var vectors = await embedder.EmbedAsync(new[]
        {
            "carbon emissions budget",
            "greenhouse gas allowance",
            "banana bread recipe",
        });

        var near = Quantizer.CosineSimilarity(vectors[0], vectors[1]);
        var far = Quantizer.CosineSimilarity(vectors[0], vectors[2]);
        near.Should().BeGreaterThan(far);
    }
}
