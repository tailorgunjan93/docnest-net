namespace DocNest;

/// <summary>
/// Re-scores candidate passages against a query for retrieval precision (Stage 11). A cross-encoder
/// reranker reads the query and passage <em>together</em> (unlike the bi-encoder <see cref="IEmbedder"/>),
/// so it judges true relevance and reorders the hybrid retriever's candidate pool. Strategy pattern —
/// swap the reranker without changing the retriever. Mirrors the Python cross-encoder (CE) stage.
/// </summary>
public interface IReranker
{
    /// <summary>
    /// Score each passage's relevance to <paramref name="query"/>. Returns one <see cref="double"/> per
    /// passage in input order; higher = more relevant. Scores are comparable within a single call only.
    /// </summary>
    /// <exception cref="EmbedException">If scoring fails.</exception>
    Task<IReadOnlyList<double>> ScoreAsync(string query, IReadOnlyList<string> passages, CancellationToken cancellationToken = default);

    /// <summary>Canonical model identifier.</summary>
    string ModelName { get; }
}
