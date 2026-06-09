using System.Text;

namespace DocNest.Parsers;

/// <summary>
/// Minimal RFC-4180 CSV reader (zero dependency). Handles quoted fields, escaped <c>""</c>,
/// and delimiters/newlines embedded inside quotes. Embedded newlines inside a quoted field are
/// preserved (Python's <c>splitlines</c>-based reader drops them — a documented, benign divergence).
/// </summary>
internal static class CsvReader
{
    public static List<List<string>> Parse(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;
        var n = text.Length;

        while (i < n)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < n && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                field.Append(c);
                i++;
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                i++;
                continue;
            }
            if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
                i++;
                continue;
            }
            if (c == '\r')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
                i += i + 1 < n && text[i + 1] == '\n' ? 2 : 1;
                continue;
            }
            if (c == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
                i++;
                continue;
            }

            field.Append(c);
            i++;
        }

        row.Add(field.ToString());
        rows.Add(row);
        return rows;
    }
}
