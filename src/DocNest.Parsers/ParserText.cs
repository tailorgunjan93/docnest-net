using System.Globalization;
using System.Text.RegularExpressions;

namespace DocNest.Parsers;

/// <summary>Shared text helpers for the parsers (word counting, title casing, line splitting).</summary>
internal static partial class ParserText
{
    /// <summary>Whitespace word count, matching Python <c>len(text.split())</c>.</summary>
    public static int WordCount(string text)
        => string.IsNullOrEmpty(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Title-case a string (each word capitalised), matching Python <c>str.title()</c> closely.</summary>
    public static string TitleCase(string text)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);

    /// <summary>Filename stem → readable title (<c>sales_data-2024 → Sales Data 2024</c>).</summary>
    public static string FilenameToTitle(string stem)
        => TitleCase(SeparatorRun().Replace(stem, " "));

    /// <summary>Split into lines on <c>\n</c>/<c>\r\n</c>/<c>\r</c> (normalised first).</summary>
    public static string[] SplitLines(string text)
        => string.IsNullOrEmpty(text) ? [] : text.ReplaceLineEndings("\n").Split('\n');

    [GeneratedRegex("[-_]+")]
    private static partial Regex SeparatorRun();
}
