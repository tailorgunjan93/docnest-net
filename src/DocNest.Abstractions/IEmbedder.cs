namespace DocNest;

/// <summary>
/// Generates dense vector representations of section text for semantic search (Stage 6a).
/// Strategy pattern — swap the embedding model without changing the pipeline.
/// Mirrors the Python <c>IEmbedder</c> ABC; the numpy array becomes a jagged
/// <see cref="IReadOnlyList{T}"/> of <see cref="float"/>[] (one vector per input text, order-preserving).
/// </summary>
public interface IEmbedder
{
    /// <summary>
    /// Embed a list of texts. Returns one <see cref="float"/>[] of length <see cref="Dims"/> per input
    /// text, in input order.
    /// </summary>
    /// <exception cref="EmbedException">If embedding generation fails.</exception>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);

    /// <summary>Embedding dimensionality.</summary>
    int Dims { get; }

    /// <summary>Canonical model identifier stored in <c>manifest.json</c>.</summary>
    string ModelName { get; }
}
