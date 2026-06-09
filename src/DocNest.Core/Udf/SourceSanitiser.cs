namespace DocNest.Udf;

/// <summary>
/// Produces a privacy-safe, portable <c>source</c> label for a <c>.udf</c>. Ports the Python
/// <c>writer._sanitise_source</c>: a <c>.udf</c> is shareable, so by default only the file
/// <b>basename</b> is stored — never the author's absolute path.
/// </summary>
public static class SourceSanitiser
{
    /// <summary>
    /// Returns <paramref name="source"/> unchanged when <paramref name="keepFull"/> is set or it is
    /// empty or a URL (contains <c>://</c>); otherwise the last path segment (splitting on both
    /// <c>/</c> and <c>\</c> so the result is correct regardless of host OS).
    /// </summary>
    public static string Sanitise(string source, bool keepFull = false)
    {
        if (keepFull || string.IsNullOrEmpty(source))
        {
            return source;
        }
        if (source.Contains("://", StringComparison.Ordinal))
        {
            return source; // URL — already portable
        }
        var trimmed = source.Replace('\\', '/').TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        var basename = slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
        return basename.Length > 0 ? basename : source;
    }
}
