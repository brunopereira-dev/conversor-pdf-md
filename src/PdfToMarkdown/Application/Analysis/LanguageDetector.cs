using System.Text.RegularExpressions;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>Identifies the language of the extracted text by counting function words.</summary>
public static partial class LanguageDetector
{
    private static readonly Dictionary<string, string[]> Markers = new()
    {
        ["en"] = ["the", "and", "of", "to", "is", "for", "with", "you", "this", "are", "from", "will"],
        ["pt"] = ["de", "que", "para", "com", "não", "uma", "dos", "como", "das", "ou", "pelo", "está"],
        ["es"] = ["de", "que", "para", "con", "los", "una", "del", "las", "por", "como", "está", "pero"],
        ["fr"] = ["le", "la", "les", "des", "une", "pour", "avec", "vous", "est", "dans", "sur", "par"],
        ["de"] = ["der", "die", "das", "und", "mit", "für", "ist", "den", "auf", "sie", "nicht", "ein"],
        ["it"] = ["il", "la", "di", "che", "per", "con", "una", "del", "sono", "nel", "come", "non"]
    };

    /// <summary>Returns an ISO 639-1 code, or <c>null</c> when the sample is too small to decide.</summary>
    public static string? Detect(IEnumerable<string> paragraphs)
    {
        var words = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            foreach (Match match in WordPattern().Matches(paragraph.ToLowerInvariant()))
            {
                words.Add(match.Value);
                if (words.Count >= 4000)
                {
                    break;
                }
            }

            if (words.Count >= 4000)
            {
                break;
            }
        }

        if (words.Count < 40)
        {
            return null;
        }

        var frequencies = words.GroupBy(word => word).ToDictionary(group => group.Key, group => group.Count());

        var best = Markers
            .Select(entry => (Language: entry.Key, Score: entry.Value.Sum(marker => frequencies.GetValueOrDefault(marker))))
            .OrderByDescending(candidate => candidate.Score)
            .First();

        return best.Score * 100 >= words.Count ? best.Language : null;
    }

    [GeneratedRegex(@"[\p{L}][\p{L}\p{M}']*", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}
