namespace DocNest.Retrieval;

/// <summary>A directed graph edge between two section indices (structural or semantic).</summary>
internal readonly record struct GraphEdge(int From, int To, string Type, double Weight);

/// <summary>
/// Reciprocal Rank Fusion + 1-hop graph expansion (pure math, no SQLite). Ports the Python constants
/// and the directional expansion (child/sibling/semantic boost; child→parent intentionally disabled).
/// </summary>
internal static class RrfFusion
{
    public const int RrfK = 60;
    public const double Bm25Weight = 2.0;
    public const double DenseWeight = 1.5;
    public const double ChildAlpha = 0.15;
    public const double SiblingAlpha = 0.10;
    public const double SemanticAlpha = 0.12;

    /// <summary>Fuse two ranked index lists: <c>score += weight / (RrfK + rank + 1)</c>.</summary>
    public static Dictionary<int, double> Fuse(IReadOnlyList<int> bm25Ranks, IReadOnlyList<int> denseRanks)
    {
        var scores = new Dictionary<int, double>();
        for (var rank = 0; rank < bm25Ranks.Count; rank++)
        {
            Add(scores, bm25Ranks[rank], Bm25Weight / (RrfK + rank + 1));
        }
        for (var rank = 0; rank < denseRanks.Count; rank++)
        {
            Add(scores, denseRanks[rank], DenseWeight / (RrfK + rank + 1));
        }
        return scores;
    }

    /// <summary>
    /// 1-hop expansion: a neighbour reachable from a seed by a child/sibling/semantic edge gains
    /// <c>α × weight × seedScore</c>. Parent edges contribute nothing (prevents parent inflation).
    /// </summary>
    public static Dictionary<int, double> GraphExpand(
        IReadOnlyDictionary<int, double> scores, IEnumerable<GraphEdge> edges, int sectionCount)
    {
        var expanded = new Dictionary<int, double>(scores);
        foreach (var edge in edges)
        {
            if (edge.To < 0 || edge.To >= sectionCount)
            {
                continue;
            }
            if (!scores.TryGetValue(edge.From, out var seedScore))
            {
                continue;
            }
            var alpha = edge.Type switch
            {
                "child" => ChildAlpha,
                "sibling" => SiblingAlpha,
                "semantic" => SemanticAlpha,
                _ => 0.0,
            };
            if (alpha == 0.0)
            {
                continue;
            }
            Add(expanded, edge.To, alpha * edge.Weight * seedScore);
        }
        return expanded;
    }

    private static void Add(Dictionary<int, double> scores, int key, double value)
        => scores[key] = scores.GetValueOrDefault(key) + value;
}
