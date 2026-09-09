using System.Text;
using System.Text.RegularExpressions;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>
/// Rebuilds paragraph text from the physical lines of the PDF: undoes line break hyphenation,
/// joins the lines of a sentence and marks keyboard shortcuts, without ever rewriting the wording.
/// </summary>
public static partial class TextCleaner
{
    private const string Hyphens = "-\u2010\u00ad";

    public static string JoinLines(IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();

        foreach (var raw in lines)
        {
            var line = CollapseSpaces(raw);
            if (line.Length == 0)
            {
                continue;
            }

            if (builder.Length == 0)
            {
                builder.Append(line);
                continue;
            }

            var previous = builder[^1];
            if (Hyphens.Contains(previous) && builder.Length > 1 && char.IsLetter(builder[^2]) && StartsLowercase(line))
            {
                builder.Length--;
                builder.Append(line);
                continue;
            }

            builder.Append(' ').Append(line);
        }

        return builder.ToString().Trim();
    }

    public static string Polish(string text) => MarkKeyboardShortcuts(CollapseSpaces(text));

    public static string CollapseSpaces(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var character in text)
        {
            var isSpace = char.IsWhiteSpace(character);
            if (isSpace)
            {
                if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }
            }
            else
            {
                builder.Append(character);
            }

            lastWasSpace = isSpace;
        }

        return builder.ToString().Trim();
    }

    private static bool StartsLowercase(string line) => line.Length > 0 && char.IsLower(line[0]);

    private static string MarkKeyboardShortcuts(string text) =>
        KeyboardShortcutPattern().Replace(text, match =>
        {
            var parts = match.Value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return "`" + string.Join(" + ", parts) + "`";
        });

    [GeneratedRegex(
        @"(?<!`)\b(?:Ctrl|Control|Alt|Shift|Cmd|Command|Win|Fn)(?:\s*\+\s*(?:F\d{1,2}|[A-Za-z0-9]{1,12}))+\b(?!`)",
        RegexOptions.CultureInvariant)]
    private static partial Regex KeyboardShortcutPattern();
}
