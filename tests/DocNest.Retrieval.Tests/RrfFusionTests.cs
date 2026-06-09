using System.Collections.Generic;
using DocNest.Retrieval;
using FluentAssertions;
using Xunit;

namespace DocNest.Retrieval.Tests;

public class RrfFusionTests
{
    [Fact]
    public void Fuse_applies_weights_and_rrf_constant()
    {
        var scores = RrfFusion.Fuse(bm25Ranks: new[] { 5, 7 }, denseRanks: new[] { 7, 9 });

        // idx 5: BM25 rank 0 → 2.0/61. idx 7: BM25 rank 1 (2.0/62) + Dense rank 0 (1.5/61). idx 9: Dense rank 1 (1.5/62).
        scores[5].Should().BeApproximately(2.0 / 61, 1e-9);
        scores[7].Should().BeApproximately((2.0 / 62) + (1.5 / 61), 1e-9);
        scores[9].Should().BeApproximately(1.5 / 62, 1e-9);
    }

    [Fact]
    public void Items_in_both_lists_outrank_single_list_items()
    {
        var scores = RrfFusion.Fuse(new[] { 1, 2 }, new[] { 2, 3 });
        scores[2].Should().BeGreaterThan(scores[1]);
        scores[2].Should().BeGreaterThan(scores[3]);
    }

    [Fact]
    public void GraphExpand_boosts_child_sibling_semantic_but_not_parent()
    {
        var scores = new Dictionary<int, double> { [0] = 1.0 };
        var edges = new[]
        {
            new GraphEdge(0, 1, "child", 1.0),
            new GraphEdge(0, 2, "sibling", 1.0),
            new GraphEdge(0, 3, "semantic", 1.0),
            new GraphEdge(0, 4, "parent", 1.0),
        };

        var expanded = RrfFusion.GraphExpand(scores, edges, sectionCount: 5);

        expanded[1].Should().BeApproximately(0.15, 1e-9);   // child α
        expanded[2].Should().BeApproximately(0.10, 1e-9);   // sibling α
        expanded[3].Should().BeApproximately(0.12, 1e-9);   // semantic α
        expanded.Should().NotContainKey(4);                 // parent → no boost
    }
}
