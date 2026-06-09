namespace DocNest;

/// <summary>One retrieval result: a section and its fused relevance score (higher = more relevant).</summary>
public sealed record RetrievalHit(Section Section, double Score);

/// <summary>
/// Retrieves the most relevant sections of a document for a query. Implementations may fuse multiple
/// signals (keyword, dense, graph). Mirrors the Python retrieval surface.
/// </summary>
public interface IRetriever
{
    /// <summary>Return the top-<paramref name="k"/> sections for <paramref name="query"/>, ranked.</summary>
    Task<IReadOnlyList<RetrievalHit>> RetrieveAsync(
        Document doc,
        string query,
        int k = 8,
        CancellationToken cancellationToken = default);
}
