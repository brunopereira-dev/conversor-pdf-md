using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>
/// Splits a page into reading regions. Multi column pages are read one full column at a time,
/// never interleaving lines of different columns.
/// </summary>
public static class ReadingOrderResolver
{
    private const int BinCount = 240;
    private const double MinGutterWidthRatio = 0.035;
    private const double MaxCrossingRatio = 0.15;
    private const double MinColumnWidthRatio = 0.25;
    private const int MinColumnLines = 3;
    private const int MinColumnCharacters = 20;

    public static List<ReadingRegion> Resolve(IReadOnlyList<TextLine> lines, RawPage page)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var contentLeft = lines.Min(line => line.Left);
        var contentRight = lines.Max(line => line.Right);
        var contentWidth = contentRight - contentLeft;
        var single = new List<ReadingRegion> { new(lines, contentLeft, contentRight) };

        if (lines.Count < 4 || contentWidth <= 0)
        {
            return single;
        }

        var center = FindGutter(lines, contentLeft, contentWidth);
        if (center is null || !LooksLikeColumns(lines, center.Value, contentWidth))
        {
            return single;
        }

        return SplitIntoZones(lines, contentLeft, contentRight, center.Value);
    }

    /// <summary>
    /// Confirms that both sides carry real running text. Without this a table with a wide gap
    /// between two of its columns would be mistaken for a two column page.
    /// </summary>
    private static bool LooksLikeColumns(IReadOnlyList<TextLine> lines, double center, double contentWidth)
    {
        var left = Side(lines, word => word.CenterX < center);
        var right = Side(lines, word => word.CenterX > center);

        return left.Lines >= MinColumnLines
            && right.Lines >= MinColumnLines
            && left.Width >= contentWidth * MinColumnWidthRatio
            && right.Width >= contentWidth * MinColumnWidthRatio
            && left.AverageCharacters >= MinColumnCharacters
            && right.AverageCharacters >= MinColumnCharacters;
    }

    private static (int Lines, double Width, double AverageCharacters) Side(
        IReadOnlyList<TextLine> lines,
        Func<RawWord, bool> belongs)
    {
        var slices = lines
            .Select(line => line.Words.Where(belongs).ToList())
            .Where(words => words.Count > 0)
            .ToList();

        if (slices.Count == 0)
        {
            return (0, 0, 0);
        }

        var width = slices.Max(words => words.Max(word => word.Right)) - slices.Min(words => words.Min(word => word.Left));
        var characters = slices.Average(words => words.Sum(word => word.Text.Length + 1));

        return (slices.Count, width, characters);
    }

    /// <summary>
    /// Reads each zone one column at a time. A line that physically crosses the gutter closes the
    /// zone and is kept whole, which is what happens with headings spanning the full page width.
    /// </summary>
    private static List<ReadingRegion> SplitIntoZones(
        IReadOnlyList<TextLine> lines,
        double contentLeft,
        double contentRight,
        double center)
    {
        var regions = new List<ReadingRegion>();
        var zone = new List<TextLine>();
        var crossing = new List<TextLine>();

        void FlushZone()
        {
            if (zone.Count == 0)
            {
                return;
            }

            AddColumn(regions, zone, word => word.CenterX < center, contentLeft, center);
            AddColumn(regions, zone, word => word.CenterX > center, center, contentRight);
            zone.Clear();
        }

        void FlushCrossing()
        {
            if (crossing.Count == 0)
            {
                return;
            }

            regions.Add(new ReadingRegion([.. crossing], contentLeft, contentRight));
            crossing.Clear();
        }

        foreach (var line in lines)
        {
            if (Crosses(line, center))
            {
                FlushZone();
                crossing.Add(line);
                continue;
            }

            FlushCrossing();
            zone.Add(line);
        }

        FlushZone();
        FlushCrossing();

        return regions;
    }

    private static void AddColumn(
        List<ReadingRegion> regions,
        List<TextLine> zone,
        Func<RawWord, bool> belongs,
        double left,
        double right)
    {
        var column = zone
            .Select(line => line.Words.Where(belongs).ToList())
            .Where(words => words.Count > 0)
            .Select(words => new TextLine(words))
            .ToList();

        if (column.Count > 0)
        {
            regions.Add(new ReadingRegion(column, left, right));
        }
    }

    private static bool Crosses(TextLine line, double center) =>
        line.Words.Any(word => word.Left < center && word.Right > center);

    /// <summary>Finds the horizontal position of the widest vertical white band splitting the page.</summary>
    private static double? FindGutter(IReadOnlyList<TextLine> lines, double contentLeft, double contentWidth)
    {
        var binWidth = contentWidth / BinCount;
        if (binWidth <= 0)
        {
            return null;
        }

        var crossings = new int[BinCount];
        foreach (var line in lines)
        {
            foreach (var word in line.Words)
            {
                var from = Math.Max((int)Math.Floor((word.Left - contentLeft) / binWidth), 0);
                var to = Math.Min((int)Math.Ceiling((word.Right - contentLeft) / binWidth), BinCount - 1);

                for (var bin = from; bin <= to; bin++)
                {
                    crossings[bin]++;
                }
            }
        }

        var limit = Math.Max(1, (int)(lines.Count * MaxCrossingRatio));
        var minBins = (int)(BinCount * MinGutterWidthRatio);

        var best = -1;
        var bestLength = 0;
        var runStart = -1;

        for (var bin = 0; bin <= BinCount; bin++)
        {
            if (bin < BinCount && crossings[bin] <= limit)
            {
                if (runStart < 0)
                {
                    runStart = bin;
                }

                continue;
            }

            if (runStart >= 0)
            {
                var length = bin - runStart;
                var middle = runStart + (length / 2.0);
                var relative = middle / BinCount;

                if (length >= minBins && relative is >= 0.30 and <= 0.70 && length > bestLength)
                {
                    bestLength = length;
                    best = (int)middle;
                }

                runStart = -1;
            }
        }

        return best < 0 ? null : contentLeft + (best * binWidth);
    }
}
