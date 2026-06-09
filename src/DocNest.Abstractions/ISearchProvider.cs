namespace DocNest;

/// <summary>
/// Abstract interface for keyword-based document search. Lifecycle: call
/// <see cref="BuildIndex"/> once with a tokenised corpus, then <see cref="GetScores"/> per query.
/// Mirrors the Python <c>ISearchProvider</c> ABC.
/// </summary>
public interface ISearchProvider
{
    /// <summary>
    /// Build a search index from a tokenised corpus — a list of documents, each a list of tokens.
    /// </summary>
    void BuildIndex(IReadOnlyList<IReadOnlyList<string>> corpus);

    /// <summary>
    /// Score all indexed documents against query tokens. Returns one score per document in corpus
    /// order, normalised to [0, 1] (higher = more relevant).
    /// </summary>
    IReadOnlyList<double> GetScores(IReadOnlyList<string> queryTokens);

    /// <summary>Backend identifier — e.g. <c>bm25</c>, <c>tfidf</c>, <c>keyword</c>.</summary>
    string BackendName { get; }
}
