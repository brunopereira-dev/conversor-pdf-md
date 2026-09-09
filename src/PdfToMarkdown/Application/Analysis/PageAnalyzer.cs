using System.Text.RegularExpressions;
using PdfToMarkdown.Application.Model;
using PdfToMarkdown.Domain;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>Turns the lines of one page into the content blocks that will be written to Markdown.</summary>
public sealed partial class PageAnalyzer(HeadingDetector headings, NoiseFilter noise)
{
    private const double FootnoteBand = 0.18;

    public List<ContentBlock> Analyze(PagePreparation preparation)
    {
        var page = preparation.Page;
        var lines = preparation.Lines.Where(line => !noise.IsNoise(line, page)).ToList();
        var footnotes = TakeFootnotes(lines, page);
        var figures = TakeFigures(preparation, lines);

        var regions = ReadingOrderResolver.Resolve(lines, page);
        var positioned = new List<Positioned>();

        foreach (var region in regions)
        {
            positioned.AddRange(AssembleRegion(region));
        }

        var blocks = regions.Count <= 1
            ? Merge(positioned, figures)
            : [.. positioned.Select(item => item.Block), .. figures.Select(item => item.Block)];

        if (page.ExtractionError is { Length: > 0 } error)
        {
            blocks.Add(new UnreadableBlock(error));
        }
        else if (lines.Count == 0)
        {
            blocks.Add(new UnreadableBlock("texto não extraído — verificar no PDF original"));
        }

        blocks.AddRange(footnotes);

        return blocks;
    }

    private static List<ContentBlock> Merge(List<Positioned> content, List<Positioned> figures) =>
        content.Concat(figures)
            .OrderByDescending(item => item.Top)
            .Select(item => item.Block)
            .ToList();

    private List<Positioned> AssembleRegion(ReadingRegion region)
    {
        var lines = region.Lines;
        var blocks = new List<Positioned>();
        var paragraph = new List<TextLine>();
        var spacing = MedianSpacing(lines);

        void Flush()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            var text = TextCleaner.Polish(TextCleaner.JoinLines(paragraph.Select(line => line.Text).ToList()));
            if (text.Length > 0)
            {
                blocks.Add(new Positioned(new ParagraphBlock(text), paragraph[0].Top));
            }

            paragraph.Clear();
        }

        var index = 0;
        while (index < lines.Count)
        {
            var line = lines[index];

            var table = TableExtractor.TryExtract(lines, index, region.Width);
            if (table is not null)
            {
                Flush();
                blocks.Add(new Positioned(BuildTable(table), line.Top));
                index += table.ConsumedLines;
                continue;
            }

            if (line.IsMonospace && !line.IsBold)
            {
                var code = new List<string>();
                var start = line.Top;
                while (index < lines.Count && lines[index].IsMonospace)
                {
                    code.Add(TextCleaner.CollapseSpaces(lines[index].Text));
                    index++;
                }

                Flush();
                blocks.Add(new Positioned(new CodeBlock(code), start));
                continue;
            }

            if (TryCallout(lines, index, region, spacing, out var callout, out var calloutLines))
            {
                Flush();
                blocks.Add(new Positioned(callout, line.Top));
                index += calloutLines;
                continue;
            }

            if (TryList(lines, index, region, spacing, out var list, out var listLines))
            {
                Flush();
                blocks.Add(new Positioned(list, line.Top));
                index += listLines;
                continue;
            }

            var heading = headings.Detect(line, IsIsolated(lines, index, spacing));
            if (heading is not null)
            {
                Flush();
                blocks.Add(new Positioned(new HeadingBlock(heading.Level, TextCleaner.Polish(heading.Text)), line.Top));
                index++;
                continue;
            }

            if (paragraph.Count > 0 && StartsNewParagraph(paragraph[^1], line, spacing, region))
            {
                Flush();
            }

            paragraph.Add(line);
            index++;
        }

        Flush();

        return blocks;
    }

    private static ContentBlock BuildTable(TableCandidate table)
    {
        var longest = table.Rows
            .Append(table.Header)
            .SelectMany(row => row)
            .Select(cell => cell.Length)
            .DefaultIfEmpty(0)
            .Max();

        var oversized = table.Header.Count > 6 || longest > 60;
        var description = "colunas: " + string.Join(" | ", table.Header.Select(cell => cell.Length == 0 ? "—" : cell));

        return new TableBlock(table.Header, table.Rows, oversized, description);
    }

    /// <summary>
    /// Builds one block per image and takes the caption line out of the flow, so the caption is
    /// transcribed inside the figure instead of being repeated as a paragraph.
    /// </summary>
    private static List<Positioned> TakeFigures(PagePreparation preparation, List<TextLine> lines)
    {
        var figures = new List<Positioned>();

        foreach (var figure in preparation.Figures)
        {
            var caption = FindCaption(figure, lines);
            if (caption is not null)
            {
                lines.Remove(caption);
            }

            var embedded = figure.EmbeddedLines
                .Select(line => TextCleaner.CollapseSpaces(line.Text))
                .Where(text => text.Length > 0)
                .ToList();

            var text = caption is null ? string.Empty : TextCleaner.CollapseSpaces(caption.Text);
            figures.Add(new Positioned(new FigureBlock(text, embedded), figure.Image.Top));
        }

        return figures;
    }

    private static TextLine? FindCaption(FigurePlacement figure, IReadOnlyList<TextLine> lines)
    {
        var image = figure.Image;
        var candidates = lines
            .Where(line => line.Top <= image.Bottom + line.Height && line.Top >= image.Bottom - (line.Height * 4))
            .Where(line => line.Right > image.Left && line.Left < image.Right)
            .OrderByDescending(line => line.Top);

        return candidates.FirstOrDefault(line => CaptionPattern().IsMatch(TextCleaner.CollapseSpaces(line.Text)));
    }

    private static List<ContentBlock> TakeFootnotes(List<TextLine> lines, RawPage page)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var limit = page.Height * FootnoteBand;
        var reference = TextLineBuilder.Median(lines.Select(line => line.FontSize).ToList());

        var footnotes = new List<ContentBlock>();
        var taken = new List<TextLine>();

        foreach (var line in lines.Where(line => line.Top <= limit))
        {
            var text = TextCleaner.CollapseSpaces(line.Text);
            if (reference > 0 && line.FontSize >= reference * 0.92)
            {
                continue;
            }

            if (!FootnotePattern().IsMatch(text))
            {
                continue;
            }

            taken.Add(line);
            footnotes.Add(new FootnoteBlock(TextCleaner.Polish(text)));
        }

        foreach (var line in taken)
        {
            lines.Remove(line);
        }

        return footnotes;
    }

    private bool TryCallout(
        IReadOnlyList<TextLine> lines,
        int index,
        ReadingRegion region,
        double spacing,
        out ContentBlock block,
        out int consumed)
    {
        block = null!;
        consumed = 0;

        var first = TextCleaner.CollapseSpaces(lines[index].Text);
        var warning = WarningPattern().Match(first);
        var note = NotePattern().Match(first);

        if (!warning.Success && !note.Success)
        {
            return false;
        }

        var kind = warning.Success ? CalloutKind.Warning : CalloutKind.Note;
        var match = warning.Success ? warning : note;
        var parts = new List<string> { match.Groups[1].Value };

        var cursor = index + 1;
        while (cursor < lines.Count
            && !StartsNewParagraph(lines[cursor - 1], lines[cursor], spacing, region)
            && headings.Detect(lines[cursor], false) is null)
        {
            parts.Add(lines[cursor].Text);
            cursor++;
        }

        var text = TextCleaner.Polish(TextCleaner.JoinLines(parts));
        consumed = cursor - index;
        block = new CalloutBlock(kind, text);

        return text.Length > 0;
    }

    private static bool TryList(
        IReadOnlyList<TextLine> lines,
        int index,
        ReadingRegion region,
        double spacing,
        out ContentBlock block,
        out int consumed)
    {
        block = null!;
        consumed = 0;

        var head = TextCleaner.CollapseSpaces(lines[index].Text);
        var ordered = OrderedItemPattern().Match(head);
        var bullet = BulletItemPattern().Match(head);

        if (!ordered.Success && !bullet.Success)
        {
            return false;
        }

        var isOrdered = ordered.Success;
        var items = new List<List<string>>();
        var cursor = index;
        var expected = isOrdered ? int.Parse(ordered.Groups[1].Value) : 0;

        while (cursor < lines.Count)
        {
            var text = TextCleaner.CollapseSpaces(lines[cursor].Text);
            var item = isOrdered ? OrderedItemPattern().Match(text) : BulletItemPattern().Match(text);

            if (item.Success && (!isOrdered || int.Parse(item.Groups[1].Value) == expected))
            {
                items.Add([item.Groups[isOrdered ? 2 : 1].Value]);
                expected++;
                cursor++;
                continue;
            }

            if (cursor == index || items.Count == 0)
            {
                break;
            }

            var previous = lines[cursor - 1];
            var current = lines[cursor];
            if (item.Success
                || StartsNewParagraph(previous, current, spacing, region)
                || current.Left < previous.Left - current.FontSize)
            {
                break;
            }

            items[^1].Add(text);
            cursor++;
        }

        if (items.Count < 2)
        {
            return false;
        }

        consumed = cursor - index;
        var start = isOrdered ? int.Parse(ordered.Groups[1].Value) : 1;
        block = new ListBlock(
            isOrdered,
            items.Select(item => TextCleaner.Polish(TextCleaner.JoinLines(item))).ToList(),
            start);

        return true;
    }

    private static bool IsIsolated(IReadOnlyList<TextLine> lines, int index, double spacing)
    {
        if (spacing <= 0)
        {
            return true;
        }

        var above = index == 0 || lines[index - 1].Bottom - lines[index].Top > spacing * 1.4;
        var below = index == lines.Count - 1 || lines[index].Bottom - lines[index + 1].Top > spacing * 1.2;

        return above && below;
    }

    private static bool StartsNewParagraph(TextLine previous, TextLine current, double spacing, ReadingRegion region)
    {
        var gap = previous.Bottom - current.Top;
        if (spacing > 0 && gap > Math.Max(spacing * 1.8, current.FontSize * 0.55))
        {
            return true;
        }

        var text = previous.Text.TrimEnd();
        var isShort = previous.Right < region.Right - (region.Width * 0.12);

        return isShort && text.Length > 0 && ".!?:".Contains(text[^1]);
    }

    private static double MedianSpacing(IReadOnlyList<TextLine> lines)
    {
        if (lines.Count < 2)
        {
            return 0;
        }

        var gaps = new List<double>();
        for (var index = 1; index < lines.Count; index++)
        {
            gaps.Add(lines[index - 1].Bottom - lines[index].Top);
        }

        return TextLineBuilder.Median(gaps);
    }

    [GeneratedRegex(
        @"^(?:[\u26a0\u2757!]\s*)?(?:WARNING|CAUTION|DANGER|ATTENTION|AVISO|ATEN[ÇC][ÃA]O|CUIDADO|PERIGO|ADVERT[ÊE]NCIA)\b[\s:.\-–—]*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WarningPattern();

    [GeneratedRegex(
        @"^(?:NOTE|NOTA|TIP|DICA|IMPORTANT|IMPORTANTE|HINT|OBSERVA[ÇC][ÃA]O)\b[\s:.\-–—]*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NotePattern();

    [GeneratedRegex(@"^(\d{1,3})[.)]\s+(\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderedItemPattern();

    [GeneratedRegex(@"^[\u2022\u25aa\u25e6\u2023\u00b7\u2219\u25cf\u25a0\u2043\-–—*]\s+(\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex BulletItemPattern();

    [GeneratedRegex(
        @"^(?:Fig(?:ura|ure)?\.?|Diagram(?:a)?|Imagem|Foto|Ilustra[çc][ãa]o|Picture)\s*\.?\s*\d*[.:\-–—\s]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CaptionPattern();

    [GeneratedRegex(@"^(?:[\*\u2020\u2021]|\(?\d{1,2}\)?)[\s.)]+\S", RegexOptions.CultureInvariant)]
    private static partial Regex FootnotePattern();

    private sealed record Positioned(ContentBlock Block, double Top);
}
