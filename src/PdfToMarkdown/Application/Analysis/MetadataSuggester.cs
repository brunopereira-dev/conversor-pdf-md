using System.Text.RegularExpressions;
using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>Suggests title and version from the first PDF of a document. Values are never assumed.</summary>
public static partial class MetadataSuggester
{
    public static string SuggestTitle(RawPdfDocument document)
    {
        if (IsUsable(document.Title))
        {
            return TextCleaner.CollapseSpaces(document.Title!);
        }

        var cover = document.Pages.FirstOrDefault();
        if (cover is not null)
        {
            var lines = TextLineBuilder.Build(cover.Words)
                .Where(line => line.Text.Trim().Length >= 3)
                .ToList();

            if (lines.Count > 0)
            {
                var largest = lines.Max(line => Math.Round(line.MaxFontSize, 1));
                var title = string.Join(
                    " ",
                    lines.Where(line => Math.Round(line.MaxFontSize, 1) >= largest - 0.1).Select(line => line.Text));

                if (IsUsable(title))
                {
                    return TextCleaner.CollapseSpaces(title);
                }
            }
        }

        var bookmark = document.Bookmarks.FirstOrDefault();
        if (bookmark is not null && IsUsable(bookmark.Title))
        {
            return TextCleaner.CollapseSpaces(bookmark.Title);
        }

        return Path.GetFileNameWithoutExtension(document.FilePath);
    }

    public static string SuggestVersion(RawPdfDocument document)
    {
        foreach (var page in document.Pages.Take(3))
        {
            var text = string.Join(" ", page.Words.Select(word => word.Text));
            var match = VersionPattern().Match(text);
            if (match.Success)
            {
                return TextCleaner.CollapseSpaces(match.Value);
            }
        }

        return string.Empty;
    }

    private static bool IsUsable(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Trim().Length >= 3
        && !value.Trim().Equals("untitled", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(
        @"\b(?:V|Ver\.?|Version|Versão|Versao|Rev\.?|Revision|Revisão)\s*\.?\s*\d+(?:\.\d+)*\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
