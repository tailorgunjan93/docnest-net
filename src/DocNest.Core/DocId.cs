using System.Text.RegularExpressions;

namespace DocNest;

/// <summary>
/// Generates stable, slug-friendly document ids from file paths. Ports the Python
/// <c>IParser._make_doc_id</c> rules (CamelCase / digit-boundary splitting, separator collapsing).
/// </summary>
public static partial class DocId
{
    /// <summary>
    /// Build a stable, slug-friendly <c>doc_id</c> from a file path. Handles CamelCase
    /// (<c>GunjanTailor → gunjan-tailor</c>), digit boundaries (<c>Report2024 → report-2024</c>),
    /// and spaces/underscores (collapsed to a single hyphen). The directory and extension are stripped.
    /// </summary>
    public static string FromPath(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path ?? "");

        // Insert a hyphen at lower→upper, letter→digit, and digit→letter boundaries.
        var s = LowerUpperBoundary().Replace(stem, "$1-$2");
        s = LetterDigitBoundary().Replace(s, "$1-$2");
        s = DigitLetterBoundary().Replace(s, "$1-$2");

        // Collapse runs of whitespace / underscores / hyphens into a single hyphen.
        s = SeparatorRun().Replace(s, "-");

        return s.ToLowerInvariant().Trim('-');
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex LowerUpperBoundary();

    [GeneratedRegex("([A-Za-z])([0-9])")]
    private static partial Regex LetterDigitBoundary();

    [GeneratedRegex("([0-9])([A-Za-z])")]
    private static partial Regex DigitLetterBoundary();

    [GeneratedRegex("[\\s_-]+")]
    private static partial Regex SeparatorRun();
}
