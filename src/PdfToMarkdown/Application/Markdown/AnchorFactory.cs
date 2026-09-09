using System.Text;

namespace PdfToMarkdown.Application.Markdown;

/// <summary>Builds GitHub style heading anchors, keeping them unique inside a document.</summary>
public sealed class AnchorFactory
{
    private readonly Dictionary<string, int> used = new(StringComparer.Ordinal);

    public string Create(string headingText)
    {
        var builder = new StringBuilder(headingText.Length);

        foreach (var character in headingText.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character) || character == '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length == 0)
        {
            slug = "secao";
        }

        if (!used.TryGetValue(slug, out var count))
        {
            used[slug] = 0;
            return slug;
        }

        used[slug] = count + 1;

        return slug + "-" + (count + 1);
    }
}
