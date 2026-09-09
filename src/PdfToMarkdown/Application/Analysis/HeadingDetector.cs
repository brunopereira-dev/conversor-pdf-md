using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>
/// Recognises section titles by type size, weight, original numbering and the PDF outline.
/// Levels mirror the structure found in the document; the original numbering is never rewritten.
/// </summary>
public sealed partial class HeadingDetector(
    double bodyFontSize,
    IReadOnlyList<double> headingSizes,
    IReadOnlyDictionary<string, int> bookmarkLevels)
{
    public static HeadingDetector Build(IEnumerable<TextLine> lines, IEnumerable<RawBookmark> bookmarks)
    {
        var materialized = lines.ToList();
        var bodyFontSize = DominantFontSize(materialized);

        var headingSizes = materialized
            .Where(line => line.FontSize > bodyFontSize * 1.10 && line.WordCount <= 25)
            .Select(line => Math.Round(line.FontSize, 1))
            .Distinct()
            .OrderByDescending(size => size)
            .ToList();

        var bookmarkLevels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var bookmark in bookmarks)
        {
            var key = NormalizeKey(bookmark.Title);
            if (key.Length >= 3)
            {
                bookmarkLevels[key] = Math.Clamp(2 + bookmark.Level, 2, 4);
            }
        }

        return new HeadingDetector(bodyFontSize, headingSizes, bookmarkLevels);
    }

    public double BodyFontSize => bodyFontSize;

    /// <summary>Returns the heading for this line, or <c>null</c> when the line is body text.</summary>
    public DetectedHeading? Detect(TextLine line, bool isolated)
    {
        var text = TextCleaner.CollapseSpaces(line.Text);
        if (text.Length == 0 || line.WordCount > 25)
        {
            return null;
        }

        if (bookmarkLevels.TryGetValue(NormalizeKey(text), out var bookmarkLevel))
        {
            return new DetectedHeading(bookmarkLevel, text);
        }

        var numbering = NumberingPattern().Match(text);
        var ratio = bodyFontSize > 0 ? line.FontSize / bodyFontSize : 1;
        var endsOpen = !EndsWithSentencePunctuation(text);

        if (ratio >= 1.12 && line.WordCount <= 20 && endsOpen)
        {
            return new DetectedHeading(LevelFor(line, numbering), text);
        }

        if (numbering.Success && endsOpen && line.WordCount <= 15 && (line.IsBold || ratio >= 1.02)
            && StartsWithTitleCase(numbering.Groups[2].Value))
        {
            return new DetectedHeading(NumberingLevel(numbering.Groups[1].Value), text);
        }

        if (isolated && line.IsBold && ratio >= 0.98 && line.WordCount <= 10 && endsOpen)
        {
            return new DetectedHeading(Math.Min(LevelFor(line, numbering) + 1, 4), text);
        }

        return null;
    }

    private int LevelFor(TextLine line, Match numbering)
    {
        if (numbering.Success)
        {
            return NumberingLevel(numbering.Groups[1].Value);
        }

        var size = Math.Round(line.FontSize, 1);
        var rank = headingSizes.Count(candidate => candidate > size);

        return Math.Clamp(2 + rank, 2, 4);
    }

    private static int NumberingLevel(string numbering) =>
        Math.Clamp(2 + numbering.Count(character => character == '.'), 2, 4);

    private static bool EndsWithSentencePunctuation(string text) =>
        text.Length > 0 && ".,;:".Contains(text[^1]);

    private static bool StartsWithTitleCase(string text) =>
        text.Length > 0 && (char.IsUpper(text[0]) || char.IsDigit(text[0]));

    private static double DominantFontSize(IReadOnlyList<TextLine> lines)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        var weights = new Dictionary<double, int>();
        foreach (var line in lines)
        {
            var size = Math.Round(line.FontSize, 1);
            weights[size] = weights.GetValueOrDefault(size) + line.Text.Length;
        }

        return weights.OrderByDescending(entry => entry.Value).First().Key;
    }

    private static string NormalizeKey(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^(\d+(?:\.\d+){0,3})[.)]?\s+(\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberingPattern();
}
