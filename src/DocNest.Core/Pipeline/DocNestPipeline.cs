using DocNest.Intelligence;

namespace DocNest.Pipeline;

/// <summary>
/// Orchestrates the deterministic normalisation pipeline: (optional parse) → normalise →
/// deterministic key-numbers → deterministic keywords → <see cref="Document"/>. LLM enrichment and
/// embeddings are pluggable later stages and are not required for this path. Design: Pipeline +
/// Dependency Inversion (the parser is injected).
/// </summary>
public sealed class DocNestPipeline
{
    private readonly SectionNormaliser _normaliser = new();
    private readonly IParser? _parser;

    /// <summary>Create a pipeline. Inject an <see cref="IParser"/> to enable <see cref="ProcessAsync"/>.</summary>
    public DocNestPipeline(IParser? parser = null) => _parser = parser;

    /// <summary>Normalise and deterministically enrich a parsed <see cref="RawDocument"/>.</summary>
    public Document Process(RawDocument raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var document = _normaliser.Normalise(raw);
        document = KeyNumberExtractor.Enrich(document);
        document = KeywordExtractor.Enrich(document);
        return document;
    }

    /// <summary>Parse a file via the injected <see cref="IParser"/>, then <see cref="Process"/> it.</summary>
    /// <exception cref="InvalidOperationException">If no parser was injected.</exception>
    public async Task<Document> ProcessAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_parser is null)
        {
            throw new InvalidOperationException(
                "No IParser configured. Inject an IParser or call Process(RawDocument).");
        }

        var raw = await _parser.ParseAsync(filePath, cancellationToken).ConfigureAwait(false);
        return Process(raw);
    }
}
