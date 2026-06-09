using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocNest.Parsers;

/// <summary>
/// Parses <c>.docx</c> Word documents (OpenXML SDK, behind this <see cref="IParser"/> wrapper).
/// Walks the body in document order, takes heading levels from Word styles (plus pseudo-headings),
/// and extracts tables with merged cells expanded. Ports the Python <c>DocxParser</c>.
/// </summary>
public sealed partial class DocxParser : IParser
{
    [GeneratedRegex(@"^heading\s*([1-6])$")]
    private static partial Regex HeadingIdRe();

    /// <inheritdoc/>
    public bool Supports(string path) => path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<RawDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new ParseException($"DOCX not found: {fullPath}");
        }
        if (new FileInfo(fullPath).Length == 0)
        {
            throw new ParseException($"DOCX is empty: {fullPath}");
        }

        return await Task.Run(() => Build(fullPath, path), cancellationToken).ConfigureAwait(false);
    }

    private static RawDocument Build(string fullPath, string originalPath)
    {
        try
        {
            using var document = WordprocessingDocument.Open(fullPath, false);
            var body = document.MainDocumentPart?.Document?.Body
                ?? throw new ParseException($"DOCX has no body: {Path.GetFileName(fullPath)}");

            return new RawDocument
            {
                DocId = DocId.FromPath(originalPath),
                Title = ExtractTitle(document, body, fullPath),
                Source = fullPath,
                Format = "docx",
                Sections = BuildSections(body),
            };
        }
        catch (ParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ParseException($"Failed to open '{Path.GetFileName(fullPath)}': {ex.Message}", ex);
        }
    }

    private static string ExtractTitle(WordprocessingDocument document, Body body, string fullPath)
    {
        var coreTitle = document.PackageProperties.Title?.Trim();
        if (!string.IsNullOrEmpty(coreTitle))
        {
            return coreTitle;
        }

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            var styleId = StyleId(paragraph);
            var text = paragraph.InnerText.Trim();
            if (text.Length > 0
                && (string.Equals(styleId, "Title", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(styleId, "Heading1", StringComparison.OrdinalIgnoreCase)))
            {
                return text;
            }
        }

        return ParserText.FilenameToTitle(Path.GetFileNameWithoutExtension(fullPath));
    }

    private static IReadOnlyList<Section> BuildSections(Body body)
    {
        var sections = new List<Section>();
        string? title = null;
        var level = 0;
        var text = new StringBuilder();
        var tables = new List<TableData>();
        var hasCurrent = false;
        var tableCounter = 0;

        void Flush()
        {
            if (hasCurrent)
            {
                sections.Add(new Section { Id = "", Title = title!, Level = level, Text = text.ToString().Trim(), Tables = tables });
            }
        }

        void Start(string newTitle, int newLevel)
        {
            Flush();
            title = newTitle;
            level = newLevel;
            text = new StringBuilder();
            tables = new List<TableData>();
            hasCurrent = true;
        }

        void EnsureCurrent(string fallbackTitle)
        {
            if (!hasCurrent)
            {
                Start(fallbackTitle, 1);
            }
        }

        foreach (var element in body.Elements())
        {
            if (element is Paragraph paragraph)
            {
                var styleId = StyleId(paragraph);
                var paraText = paragraph.InnerText.Trim();
                var headingLevel = HeadingLevel(styleId);

                if (headingLevel is not null && paraText.Length > 0)
                {
                    Start(paraText, headingLevel.Value);
                }
                else if (paraText.Length > 0 && IsPseudoHeading(paragraph, paraText))
                {
                    Start(paraText.TrimEnd(':').Trim(), 1);
                }
                else
                {
                    if (paraText.Length == 0)
                    {
                        continue;
                    }
                    EnsureCurrent("Introduction");
                    if (styleId.Contains("List", StringComparison.OrdinalIgnoreCase))
                    {
                        text.Append("- ").Append(paraText).Append('\n');
                    }
                    else
                    {
                        text.Append(paraText).Append("\n\n");
                    }
                }
            }
            else if (element is Table table)
            {
                tableCounter++;
                var built = BuildTable(table, tableCounter);
                if (built is not null)
                {
                    EnsureCurrent("Tables");
                    tables.Add(built);
                }
            }
        }

        Flush();
        return sections;
    }

    private static string StyleId(Paragraph paragraph)
        => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;

    private static int? HeadingLevel(string styleId)
    {
        if (string.IsNullOrEmpty(styleId))
        {
            return null;
        }
        var id = styleId.Trim();
        if (string.Equals(id, "Title", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }
        if (string.Equals(id, "Subtitle", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }
        var match = HeadingIdRe().Match(id.ToLowerInvariant());
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static bool IsPseudoHeading(Paragraph paragraph, string text)
    {
        if (text.Length == 0 || text.Length > 100)
        {
            return false;
        }

        if (string.Equals(text, text.ToUpperInvariant(), StringComparison.Ordinal) && text.Any(char.IsLetter))
        {
            return true;
        }

        var total = 0;
        var bold = 0;
        foreach (var run in paragraph.Elements<Run>())
        {
            var runText = run.InnerText;
            if (runText.Length == 0)
            {
                continue;
            }
            total += runText.Length;
            if (IsBold(run))
            {
                bold += runText.Length;
            }
        }
        if (total > 0 && (double)bold / total > 0.5)
        {
            return true;
        }

        return text.EndsWith(':')
            && !text[..^1].Contains('.', StringComparison.Ordinal)
            && !text.Contains('?', StringComparison.Ordinal)
            && !text.Contains('!', StringComparison.Ordinal);
    }

    private static bool IsBold(Run run)
    {
        var bold = run.RunProperties?.Bold;
        return bold is not null && (bold.Val is null || bold.Val.Value);
    }

    private static TableData? BuildTable(Table table, int counter)
    {
        var grid = DocxTable.ToGrid(table);
        if (grid.Count == 0)
        {
            return null;
        }
        return new TableData
        {
            TableId = $"tbl_{counter:D3}",
            Caption = null,
            Headers = grid[0],
            Rows = grid.Skip(1).Select(r => (IReadOnlyList<string>)r).ToList(),
        };
    }
}
