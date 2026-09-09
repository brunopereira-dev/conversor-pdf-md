using System.Globalization;
using System.Text;
using PdfToMarkdown.Application.Abstractions;

namespace PdfToMarkdown.Application.Documents;

/// <inheritdoc />
public sealed class OutputFileNameFactory : IOutputFileNameFactory
{
    public const string Fallback = "arquivo-sem-titulo";

    private const string Extension = ".md";

    public string FromTitle(string? title)
    {
        var slug = Slugify(title);

        return (slug.Length == 0 ? Fallback : slug) + Extension;
    }

    public string EnsureUnique(string fileName, ISet<string> taken)
    {
        if (taken.Add(fileName))
        {
            return fileName;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var suffix = 2;

        while (true)
        {
            var candidate = stem + "-" + suffix.ToString(CultureInfo.InvariantCulture) + Extension;
            if (taken.Add(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;

        foreach (var character in value.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingSeparator = false;
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            pendingSeparator = true;
        }

        return builder.ToString().Trim('-');
    }
}
