using System.Globalization;

namespace DocNest.Pipeline;

/// <summary>
/// Assigns hierarchical §ids to a <see cref="RawDocument"/> and builds the parent/child tree,
/// computes token counts, and normalises table column widths — producing a <see cref="Document"/>.
/// Ports the Python <c>SectionNormaliser</c>. Records are immutable, so this is a two-pass build:
/// pass 1 assigns ids + parent links and collects child ids; pass 2 freezes the sections.
/// </summary>
public sealed class SectionNormaliser
{
    /// <summary>Assign §ids and build parent/child links for every section in <paramref name="raw"/>.</summary>
    public Document Normalise(RawDocument raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var counters = new int[6];
        var stack = new List<(int RawLevel, string Id, int Depth)>();
        var childIds = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var assigned = new List<(Section Source, string Id, string? ParentId)>(raw.Sections.Count);

        foreach (var section in raw.Sections)
        {
            var rawLevel = Math.Clamp(section.Level, 1, 6);

            while (stack.Count > 0 && stack[^1].RawLevel >= rawLevel)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            var depth = Math.Min(stack.Count > 0 ? stack[^1].Depth + 1 : 0, 5);
            counters[depth]++;
            for (var i = depth + 1; i < 6; i++)
            {
                counters[i] = 0;
            }

            var id = "§" + string.Join(
                ".", Enumerable.Range(0, depth + 1).Select(i => counters[i].ToString(CultureInfo.InvariantCulture)));

            string? parentId = stack.Count > 0 ? stack[^1].Id : null;
            if (parentId is not null)
            {
                if (!childIds.TryGetValue(parentId, out var kids))
                {
                    kids = new List<string>();
                    childIds[parentId] = kids;
                }
                if (!kids.Contains(id))
                {
                    kids.Add(id);
                }
            }

            assigned.Add((section, id, parentId));
            stack.Add((rawLevel, id, depth));
        }

        var sections = new List<Section>(assigned.Count);
        foreach (var (source, id, parentId) in assigned)
        {
            IReadOnlyList<string> children = childIds.TryGetValue(id, out var kids) ? kids : [];
            sections.Add(source with
            {
                Id = id,
                ParentId = parentId,
                Children = children,
                TokenCount = TokenCount(source.Text),
                Tables = NormaliseTables(source.Tables),
            });
        }

        return new Document
        {
            DocId = raw.DocId,
            Title = raw.Title,
            Source = raw.Source,
            Format = raw.Format,
            Sections = sections,
        };
    }

    private static int TokenCount(string text) => (int)(WordCount(text) * 1.3);

    private static int WordCount(string text)
        => string.IsNullOrEmpty(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static IReadOnlyList<TableData> NormaliseTables(IReadOnlyList<TableData> tables)
    {
        if (tables.Count == 0)
        {
            return tables;
        }

        var result = new List<TableData>(tables.Count);
        foreach (var table in tables)
        {
            var width = table.Headers.Count;
            if (width == 0)
            {
                result.Add(table);
                continue;
            }

            var rows = new List<IReadOnlyList<string>>(table.Rows.Count);
            foreach (var row in table.Rows)
            {
                if (row.Count < width)
                {
                    var padded = new List<string>(row);
                    padded.AddRange(Enumerable.Repeat(string.Empty, width - row.Count));
                    rows.Add(padded);
                }
                else if (row.Count > width)
                {
                    rows.Add(row.Take(width).ToList());
                }
                else
                {
                    rows.Add(row);
                }
            }

            result.Add(table with { Rows = rows });
        }

        return result;
    }
}
