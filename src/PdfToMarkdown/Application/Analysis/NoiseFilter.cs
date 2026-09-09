using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>
/// Recognises running headers, running footers, loose page numbers and repeated watermarks,
/// by looking at how often the same text appears at the same height across the pages of a part.
/// </summary>
public sealed partial class NoiseFilter(HashSet<string> repeatedAnywhere, HashSet<string> repeatedInMargin)
{
    private const double MarginBand = 0.10;

    public static NoiseFilter Build(IReadOnlyList<(RawPage Page, List<TextLine> Lines)> pages)
    {
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        var marginCounters = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (page, lines) in pages)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in lines)
            {
                var signature = Signature(line, page);
                if (signature is null || !seen.Add(signature))
                {
                    continue;
                }

                counters[signature] = counters.GetValueOrDefault(signature) + 1;
                if (IsInMargin(line, page))
                {
                    marginCounters[signature] = marginCounters.GetValueOrDefault(signature) + 1;
                }
            }
        }

        var anywhere = Repeated(counters, Math.Max(3, (int)Math.Ceiling(pages.Count * 0.6)));
        var margin = Repeated(marginCounters, Math.Max(2, (int)Math.Ceiling(pages.Count * 0.6)));

        return new NoiseFilter(anywhere, margin);
    }

    /// <summary>
    /// A line is noise when the same text repeats at the same height across the part: a running
    /// header, a running footer or a watermark. In the page margins two occurrences already count.
    /// </summary>
    public bool IsNoise(TextLine line, RawPage page)
    {
        var inMargin = IsInMargin(line, page);
        if (inMargin && IsLoosePageNumber(line.Text))
        {
            return true;
        }

        var signature = Signature(line, page);

        return signature is not null
            && (repeatedAnywhere.Contains(signature) || (inMargin && repeatedInMargin.Contains(signature)));
    }

    private static HashSet<string> Repeated(Dictionary<string, int> counters, int threshold) =>
        counters.Where(entry => entry.Value >= threshold).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);

    private static bool IsInMargin(TextLine line, RawPage page) =>
        line.Bottom >= page.Height * (1 - MarginBand) || line.Top <= page.Height * MarginBand;

    private static bool IsLoosePageNumber(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= 24 && LoosePageNumberPattern().IsMatch(trimmed);
    }

    /// <summary>Text plus vertical band, so that only text repeating at the same height counts as noise.</summary>
    private static string? Signature(TextLine line, RawPage page)
    {
        if (page.Height <= 0)
        {
            return null;
        }

        var normalized = Normalize(line.Text);
        if (normalized.Length == 0 || normalized.Length > 200)
        {
            return null;
        }

        var band = Math.Round(line.CenterY / page.Height, 2);
        return string.Create(CultureInfo.InvariantCulture, $"{band:0.00}|{normalized}");
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var character in text.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            lastWasSpace = false;
            builder.Append(char.IsDigit(character) ? '#' : character);
        }

        return builder.ToString().TrimEnd();
    }

    [GeneratedRegex(@"^[\-–—\s\[\(]*(?:p(?:a?g(?:e|ina)?)?\.?\s*)?\d{1,4}(?:\s*(?:/|of|de)\s*\d{1,4})?[\s\]\)\-–—]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LoosePageNumberPattern();

}
