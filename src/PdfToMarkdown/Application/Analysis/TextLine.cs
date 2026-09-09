using System.Text;
using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>A horizontal run of words sharing the same baseline on a page.</summary>
public sealed class TextLine(IReadOnlyList<RawWord> words)
{
    public IReadOnlyList<RawWord> Words { get; } = words;

    public double Left { get; } = words.Min(word => word.Left);

    public double Right { get; } = words.Max(word => word.Right);

    public double Top { get; } = words.Max(word => word.Top);

    public double Bottom { get; } = words.Min(word => word.Bottom);

    public double Height => Top - Bottom;

    public double CenterY => (Top + Bottom) / 2d;

    /// <summary>Font size of the majority of the characters of the line.</summary>
    public double FontSize { get; } = DominantFontSize(words);

    public double MaxFontSize { get; } = words.Max(word => word.FontSize);

    public bool IsBold { get; } = CharacterShare(words, word => word.IsBold) >= 0.6;

    public bool IsMonospace { get; } = CharacterShare(words, word => word.IsMonospace) >= 0.8;

    public string Text { get; } = Join(words);

    public int WordCount => Words.Count;

    /// <summary>The part of this line contained in the given horizontal band, or <c>null</c> when empty.</summary>
    public TextLine? Slice(double left, double right)
    {
        var slice = Words.Where(word => word.CenterX >= left && word.CenterX <= right).ToList();
        return slice.Count == 0 ? null : new TextLine(slice);
    }

    private static string Join(IReadOnlyList<RawWord> words)
    {
        var builder = new StringBuilder();
        foreach (var word in words)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(word.Text);
        }

        return builder.ToString().Trim();
    }

    private static double DominantFontSize(IReadOnlyList<RawWord> words)
    {
        var bySize = words
            .GroupBy(word => Math.Round(word.FontSize, 1))
            .OrderByDescending(group => group.Sum(word => word.Text.Length))
            .First();

        return bySize.Key;
    }

    private static double CharacterShare(IReadOnlyList<RawWord> words, Func<RawWord, bool> predicate)
    {
        var total = words.Sum(word => word.Text.Length);
        if (total == 0)
        {
            return 0;
        }

        return (double)words.Where(predicate).Sum(word => word.Text.Length) / total;
    }
}
