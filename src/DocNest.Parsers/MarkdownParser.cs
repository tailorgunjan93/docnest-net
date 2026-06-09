using System.Text.RegularExpressions;

namespace DocNest.Parsers;

/// <summary>
/// Parses Markdown into structured sections via a line-by-line ATX heading scan (no dependencies).
/// Fenced code blocks (<c>```</c> / <c>~~~</c>) are preserved verbatim and never yield headings.
/// Ports the Python <c>MarkdownParser</c>.
/// </summary>
public sealed partial class MarkdownParser : IParser
{
    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*$")]
    private static partial Regex HeadingRe();

    /// <inheritdoc/>
    public bool Supports(string path)
        => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<RawDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ParseException($"Cannot read {path}: {ex.Message}", ex);
        }

        return Build(text, path);
    }

    private static RawDocument Build(string sourceText, string filePath)
    {
        var docId = DocId.FromPath(filePath);
        var stem = Path.GetFileNameWithoutExtension(filePath);
        var sections = new List<Section>();

        string? currentTitle = null;
        var currentLevel = 0;
        var currentLines = new List<string>();
        var docTitle = stem;

        void Flush(string title, int level, List<string> lines)
        {
            var text = string.Join("\n", lines).Trim();
            sections.Add(new Section
            {
                Id = "",
                Title = title,
                Level = level,
                Text = text,
                TokenCount = Math.Max(1, ParserText.WordCount(text)),
            });
        }

        var inFence = false;
        foreach (var line in ParserText.SplitLines(sourceText))
        {
            var stripped = line.Trim();
            if (stripped.StartsWith("```", StringComparison.Ordinal) || stripped.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }

            if (!inFence)
            {
                var m = HeadingRe().Match(line);
                if (m.Success)
                {
                    if (currentTitle is not null)
                    {
                        Flush(currentTitle, currentLevel, currentLines);
                    }
                    else if (currentLines.Any(l => l.Trim().Length > 0))
                    {
                        Flush(ParserText.TitleCase(docId.Replace("-", " ", StringComparison.Ordinal)), 1, currentLines);
                    }

                    currentLevel = m.Groups[1].Value.Length;
                    currentTitle = m.Groups[2].Value;
                    currentLines = new List<string>();

                    if (currentLevel == 1 && docTitle == stem)
                    {
                        docTitle = currentTitle;
                    }
                    continue;
                }
            }

            currentLines.Add(line);
        }

        if (currentTitle is not null)
        {
            Flush(currentTitle, currentLevel, currentLines);
        }
        else if (currentLines.Any(l => l.Trim().Length > 0))
        {
            Flush(docTitle, 1, currentLines);
        }

        if (sections.Count == 0)
        {
            sections.Add(new Section { Id = "", Title = docTitle, Level = 1, Text = "", TokenCount = 0 });
        }

        return new RawDocument
        {
            DocId = docId,
            Title = docTitle,
            Source = Path.GetFullPath(filePath),
            Format = "md",
            Sections = sections,
        };
    }
}
