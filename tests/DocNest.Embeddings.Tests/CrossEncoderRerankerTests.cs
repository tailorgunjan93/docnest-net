using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocNest.Embeddings;
using FluentAssertions;
using Xunit;

namespace DocNest.Embeddings.Tests;

/// <summary>
/// Real-inference tests for <see cref="OnnxCrossEncoderReranker"/> (ADR-0013). The ~91 MB ms-marco model
/// is not committed, so these are <see cref="SkippableFact"/> — they skip with a clear reason when the
/// model is absent and run real inference when present. Cache dir = <c>DOCNEST_MSMARCO_CACHE</c> env, else
/// <c>AppContext.BaseDirectory/ms-marco-cache</c> (provision via <see cref="CrossEncoderModel.EnsureDownloadedAsync"/>).
/// </summary>
public class CrossEncoderRerankerTests
{
    private static string CacheDir =>
        Environment.GetEnvironmentVariable("DOCNEST_MSMARCO_CACHE") is { Length: > 0 } env
            ? env
            : Path.Combine(AppContext.BaseDirectory, "ms-marco-cache");

    [SkippableFact]
    public async Task Scores_relevant_passage_above_irrelevant()
    {
        Skip.IfNot(
            CrossEncoderModel.IsPresent(CacheDir),
            $"ms-marco cross-encoder not present in {CacheDir}. Run CrossEncoderModel.EnsureDownloadedAsync to enable.");

        var (modelPath, vocabPath) = CrossEncoderModel.Paths(CacheDir);
        using var reranker = new OnnxCrossEncoderReranker(modelPath, vocabPath);

        const string query = "how do mammals regulate their body temperature";
        var scores = await reranker.ScoreAsync(query, new[]
        {
            "Thermoregulation in mammals: endotherms hold a stable core temperature through shivering, sweating, panting, and vasodilation directed by the hypothalamus.",
            "The stock market closed higher today after strong quarterly earnings from several technology companies.",
        });

        scores.Should().HaveCount(2);
        scores[0].Should().BeGreaterThan(scores[1]); // the on-topic passage must outscore the off-topic one
    }

    [SkippableFact]
    public async Task Ranks_the_answer_bearing_passage_first_among_distractors()
    {
        Skip.IfNot(CrossEncoderModel.IsPresent(CacheDir), "ms-marco cross-encoder not present.");

        var (modelPath, vocabPath) = CrossEncoderModel.Paths(CacheDir);
        using var reranker = new OnnxCrossEncoderReranker(modelPath, vocabPath);

        const string query = "what is the remaining global carbon budget for 1.5 degrees";
        var passages = new[]
        {
            "Acknowledgements and the list of contributing authors for this assessment report.",                       // 0 — irrelevant
            "The remaining carbon budget to keep warming to 1.5°C is estimated at about 500 GtCO2 from 2020 onward.",  // 1 — the answer
            "Alphabetical index of topics, figures, and page references used throughout the volume.",                  // 2 — irrelevant
        };

        var scores = await reranker.ScoreAsync(query, passages);
        var best = Enumerable.Range(0, scores.Count).OrderByDescending(i => scores[i]).First();

        best.Should().Be(1); // the passage that actually states the carbon budget ranks highest
    }
}
