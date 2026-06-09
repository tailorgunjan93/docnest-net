using AngleSharp.Dom;
using AngleHtmlParser = AngleSharp.Html.Parser.HtmlParser;

namespace DocNest.Parsers;

/// <summary>
/// Parses HTML using AngleSharp: walks <c>h1</c>–<c>h6</c> to build the section hierarchy and
/// extracts <c>&lt;table&gt;</c> elements (expanding <c>rowspan</c>/<c>colspan</c> into a dense grid).
/// AngleSharp is kept entirely behind this <see cref="IParser"/> wrapper. Ports the Python
/// <c>HTMLParser</c>.
/// </summary>
public sealed class HtmlParser : IParser
{
    private static readonly Dictionary<string, int> HeadingTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["h1"] = 1, ["h2"] = 2, ["h3"] = 3, ["h4"] = 4, ["h5"] = 5, ["h6"] = 6,
    };

    /// <inheritdoc/>
    public bool Supports(string path)
        => path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<RawDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new ParseException($"File not found: {path}");
        }

        string html;
        try
        {
            html = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ParseException($"Cannot read {path}: {ex.Message}", ex);
        }

        var document = new AngleHtmlParser().ParseDocument(html);
        var stem = Path.GetFileNameWithoutExtension(path);

        var titleText = document.QuerySelector("title")?.TextContent.Trim();
        var h1 = document.QuerySelector("h1");
        var title = !string.IsNullOrEmpty(titleText)
            ? titleText
            : h1 is not null ? h1.TextContent.Trim()
            : ParserText.TitleCase(stem.Replace("-", " ", StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal));

        return new RawDocument
        {
            DocId = DocId.FromPath(path),
            Title = title,
            Source = Path.GetFullPath(path),
            Format = "html",
            Sections = ExtractSections(document),
        };
    }

    private static IReadOnlyList<Section> ExtractSections(IDocument document)
    {
        var headings = document.QuerySelectorAll("h1,h2,h3,h4,h5,h6");
        if (headings.Length == 0)
        {
            var body = document.Body?.TextContent.Trim() ?? string.Empty;
            return body.Length > 0
                ? new[] { new Section { Id = "", Title = "Document", Level = 1, Text = body } }
                : [];
        }

        var sections = new List<Section>(headings.Length);
        foreach (var heading in headings)
        {
            var level = HeadingTags[heading.LocalName];
            var headingText = heading.TextContent.Trim();
            var textParts = new List<string>();
            var tables = new List<TableData>();

            var sibling = heading.NextSibling;
            while (sibling is not null)
            {
                if (sibling is IElement element)
                {
                    if (HeadingTags.ContainsKey(element.LocalName))
                    {
                        break;
                    }
                    if (string.Equals(element.LocalName, "table", StringComparison.OrdinalIgnoreCase))
                    {
                        var table = ExtractTable(element, tables.Count);
                        if (table is not null)
                        {
                            tables.Add(table);
                        }
                    }
                    else
                    {
                        var chunk = element.TextContent.Trim();
                        if (chunk.Length > 0)
                        {
                            textParts.Add(chunk);
                        }
                    }
                }
                else if (sibling.NodeType == NodeType.Text)
                {
                    var chunk = sibling.TextContent.Trim();
                    if (chunk.Length > 0)
                    {
                        textParts.Add(chunk);
                    }
                }

                sibling = sibling.NextSibling;
            }

            sections.Add(new Section
            {
                Id = "",
                Title = headingText,
                Level = level,
                Text = string.Join(" ", textParts),
                Tables = tables,
            });
        }

        return sections;
    }

    private static TableData? ExtractTable(IElement tableElement, int index)
    {
        var rows = tableElement.QuerySelectorAll("tr");
        if (rows.Length == 0)
        {
            return null;
        }

        var grid = ExpandGrid(rows);
        if (grid.Count == 0 || grid[0].All(string.IsNullOrEmpty))
        {
            return null;
        }

        var headers = grid[0];
        var dataRows = grid.Skip(1)
            .Where(r => r.Any(c => c.Trim().Length > 0))
            .Select(r => (IReadOnlyList<string>)r)
            .ToList();

        var caption = tableElement.QuerySelector("caption")?.TextContent.Trim();

        return new TableData
        {
            TableId = $"tbl_{index + 1:D3}",
            Caption = string.IsNullOrEmpty(caption) ? null : caption,
            Headers = headers,
            Rows = dataRows,
        };
    }

    private static List<List<string>> ExpandGrid(IHtmlCollection<IElement> rows)
    {
        var occupied = new Dictionary<(int Row, int Col), string>();
        var maxCols = 0;

        for (var r = 0; r < rows.Length; r++)
        {
            var c = 0;
            foreach (var cell in rows[r].QuerySelectorAll("th,td"))
            {
                while (occupied.ContainsKey((r, c)))
                {
                    c++;
                }

                var value = cell.TextContent.Trim();
                var colspan = ParseSpan(cell.GetAttribute("colspan"));
                var rowspan = ParseSpan(cell.GetAttribute("rowspan"));

                for (var dr = 0; dr < rowspan; dr++)
                {
                    for (var dc = 0; dc < colspan; dc++)
                    {
                        occupied[(r + dr, c + dc)] = value;
                    }
                }

                c += colspan;
                maxCols = Math.Max(maxCols, c);
            }
        }

        if (occupied.Count == 0)
        {
            return new List<List<string>>();
        }

        var rowCount = occupied.Keys.Max(k => k.Row) + 1;
        var grid = new List<List<string>>(rowCount);
        for (var r = 0; r < rowCount; r++)
        {
            var rowList = new List<string>(maxCols);
            for (var c = 0; c < maxCols; c++)
            {
                rowList.Add(occupied.GetValueOrDefault((r, c), string.Empty));
            }
            grid.Add(rowList);
        }
        return grid;
    }

    private static int ParseSpan(string? raw)
        => int.TryParse(raw, out var value) ? Math.Max(1, value) : 1;
}
