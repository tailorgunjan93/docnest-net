namespace DocNest;

/// <summary>
/// Converts a raw file into a <see cref="RawDocument"/> (Stage 1 — structure extraction).
/// Implement this to add support for a new document format. Parsers do NOT assign §ids —
/// that is the normaliser's job. Mirrors the Python <c>IParser</c> ABC.
/// </summary>
public interface IParser
{
    /// <summary>Parse the file and return a structured <see cref="RawDocument"/> (sections without §ids).</summary>
    /// <exception cref="ParseException">If the file cannot be read or parsed.</exception>
    Task<RawDocument> ParseAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Returns <see langword="true"/> if this parser handles the given file (by extension by default).</summary>
    bool Supports(string path);
}
