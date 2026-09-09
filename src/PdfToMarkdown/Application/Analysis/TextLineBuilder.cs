using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>Groups the words of a page into baseline aligned lines, ordered top to bottom.</summary>
public static class TextLineBuilder
{
    public static List<TextLine> Build(IEnumerable<RawWord> words)
    {
        var ordered = words
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderByDescending(word => word.Bottom)
            .ToList();

        if (ordered.Count == 0)
        {
            return [];
        }

        var reference = Median(ordered.Select(word => Math.Max(word.Height, word.FontSize * 0.6)).ToList());
        var tolerance = Math.Max(reference * 0.45, 0.5);

        var lines = new List<TextLine>();
        var current = new List<RawWord> { ordered[0] };
        var currentBaseline = ordered[0].Bottom;

        foreach (var word in ordered.Skip(1))
        {
            if (Math.Abs(word.Bottom - currentBaseline) <= tolerance)
            {
                current.Add(word);
                currentBaseline = current.Average(item => item.Bottom);
                continue;
            }

            lines.Add(Close(current));
            current = [word];
            currentBaseline = word.Bottom;
        }

        lines.Add(Close(current));

        return lines.OrderByDescending(line => line.Top).ToList();
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(value => value).ToList();
        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2d;
    }

    private static TextLine Close(List<RawWord> words) =>
        new(words.OrderBy(word => word.Left).ToList());
}
