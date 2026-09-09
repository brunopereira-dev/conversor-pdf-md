using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>
/// Recovers tabular data from runs of lines whose words align in the same horizontal positions.
/// A merged cell is repeated in every column it spans; an empty cell stays empty.
/// </summary>
public static class TableExtractor
{
    private const int MinimumRows = 3;
    private const int MaximumColumns = 12;

    public static TableCandidate? TryExtract(IReadOnlyList<TextLine> lines, int start, double regionWidth)
    {
        var run = CollectRun(lines, start, regionWidth);
        if (run.Count < MinimumRows)
        {
            return null;
        }

        var columns = ClusterColumns(run, regionWidth);
        if (columns.Count is < 2 or > MaximumColumns)
        {
            return null;
        }

        var rows = new List<IReadOnlyList<string>>();
        foreach (var groups in run)
        {
            rows.Add(BuildRow(groups, columns, regionWidth));
        }

        rows = DropEmptyColumns(rows, columns.Count);
        if (rows.Count < MinimumRows || rows[0].Count < 2)
        {
            return null;
        }

        var header = rows[0];
        var body = rows.Skip(1).ToList();

        return new TableCandidate(header, body, run.Count);
    }

    private static List<List<CellGroup>> CollectRun(IReadOnlyList<TextLine> lines, int start, double regionWidth)
    {
        var run = new List<List<CellGroup>>();
        TextLine? previous = null;

        for (var index = start; index < lines.Count; index++)
        {
            var line = lines[index];
            if (previous is not null)
            {
                var gap = previous.Bottom - line.Top;
                if (gap > Math.Max(previous.Height * 2.0, 6))
                {
                    break;
                }
            }

            var groups = GroupCells(line, regionWidth);
            if (groups.Count < 2)
            {
                break;
            }

            run.Add(groups);
            previous = line;
        }

        return run;
    }

    private static List<CellGroup> GroupCells(TextLine line, double regionWidth)
    {
        var threshold = Math.Max(line.FontSize * 0.9, regionWidth * 0.015);
        var groups = new List<CellGroup>();
        var current = new List<RawWord>();

        foreach (var word in line.Words)
        {
            if (current.Count > 0 && word.Left - current[^1].Right > threshold)
            {
                groups.Add(CellGroup.From(current));
                current = [];
            }

            current.Add(word);
        }

        if (current.Count > 0)
        {
            groups.Add(CellGroup.From(current));
        }

        return groups;
    }

    private static List<double> ClusterColumns(List<List<CellGroup>> run, double regionWidth)
    {
        var tolerance = Math.Max(regionWidth * 0.02, 4);
        var starts = run.SelectMany(groups => groups).Select(group => group.Left).OrderBy(value => value).ToList();

        var clusters = new List<List<double>>();
        foreach (var value in starts)
        {
            if (clusters.Count > 0 && value - clusters[^1][^1] <= tolerance)
            {
                clusters[^1].Add(value);
                continue;
            }

            clusters.Add([value]);
        }

        var support = Math.Max(2, (int)Math.Ceiling(run.Count * 0.35));

        return clusters
            .Where(cluster => cluster.Count >= support || cluster.Count >= run.Count)
            .Select(cluster => cluster.Min())
            .OrderBy(value => value)
            .ToList();
    }

    private static List<string> BuildRow(List<CellGroup> groups, List<double> columns, double regionWidth)
    {
        var cells = new string[columns.Count];
        Array.Fill(cells, string.Empty);
        var tolerance = Math.Max(regionWidth * 0.02, 4);

        foreach (var group in groups)
        {
            var index = IndexOfColumn(columns, group.Left, tolerance);
            var spanned = SpannedColumns(columns, group, tolerance, groups.Count);

            foreach (var target in spanned.Count > 0 ? spanned : [index])
            {
                cells[target] = Append(cells[target], group.Text);
            }
        }

        return [.. cells];
    }

    /// <summary>Columns fully covered by a merged cell; the value is repeated in each of them.</summary>
    private static List<int> SpannedColumns(List<double> columns, CellGroup group, double tolerance, int groupCount)
    {
        var spanned = new List<int>();
        if (groupCount >= columns.Count)
        {
            return spanned;
        }

        for (var index = 0; index < columns.Count; index++)
        {
            var columnLeft = columns[index];
            var columnRight = index + 1 < columns.Count ? columns[index + 1] : double.MaxValue;
            var columnWidth = columnRight - columnLeft;

            if (group.Left <= columnLeft + tolerance && group.Right >= columnLeft + Math.Min(columnWidth * 0.5, 40))
            {
                spanned.Add(index);
            }
        }

        return spanned.Count >= 2 ? spanned : [];
    }

    private static int IndexOfColumn(List<double> columns, double left, double tolerance)
    {
        for (var index = columns.Count - 1; index >= 0; index--)
        {
            if (left >= columns[index] - tolerance)
            {
                return index;
            }
        }

        return 0;
    }

    private static List<IReadOnlyList<string>> DropEmptyColumns(List<IReadOnlyList<string>> rows, int columnCount)
    {
        var keep = new List<int>();
        for (var column = 0; column < columnCount; column++)
        {
            if (rows.Any(row => row[column].Length > 0))
            {
                keep.Add(column);
            }
        }

        return rows
            .Select(row => (IReadOnlyList<string>)keep.Select(column => row[column]).ToList())
            .ToList();
    }

    private static string Append(string current, string addition) =>
        current.Length == 0 ? addition : current + " " + addition;

    private sealed record CellGroup(string Text, double Left, double Right)
    {
        public static CellGroup From(List<RawWord> words) => new(
            string.Join(" ", words.Select(word => word.Text)),
            words.Min(word => word.Left),
            words.Max(word => word.Right));
    }
}
