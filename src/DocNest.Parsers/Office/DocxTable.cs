using DocumentFormat.OpenXml.Wordprocessing;

namespace DocNest.Parsers;

/// <summary>
/// Expands a Word table into a dense rectangular grid, repeating merged-cell values so columns stay
/// aligned. The OpenXML SDK (unlike python-docx) does not auto-repeat merges: horizontal
/// <c>w:gridSpan</c> repeats a value across columns; vertical <c>w:vMerge</c> (a <c>restart</c> cell)
/// repeats its value down into following <c>continue</c> cells in the same column.
/// </summary>
internal static class DocxTable
{
    public static List<List<string>> ToGrid(Table table)
    {
        var grid = new List<List<string>>();

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements<TableCell>())
            {
                var props = cell.TableCellProperties;

                var span = 1;
                if (props?.GridSpan?.Val is { } gridSpan)
                {
                    span = Math.Max(1, gridSpan.Value);
                }

                var vMerge = props?.GetFirstChild<VerticalMerge>();
                var isContinuation = vMerge is not null
                    && (vMerge.Val is null || vMerge.Val.Value != MergedCellValues.Restart);

                string text;
                if (isContinuation && grid.Count > 0)
                {
                    var above = grid[^1];
                    var col = cells.Count;
                    text = col < above.Count ? above[col] : string.Empty;
                }
                else
                {
                    text = cell.InnerText.Trim();
                }

                for (var dc = 0; dc < span; dc++)
                {
                    cells.Add(text);
                }
            }
            grid.Add(cells);
        }

        var maxCols = grid.Count == 0 ? 0 : grid.Max(r => r.Count);
        foreach (var row in grid)
        {
            while (row.Count < maxCols)
            {
                row.Add(string.Empty);
            }
        }
        return grid;
    }
}
