using System.Globalization;
using System.Text;
using PdfToMarkdown.Domain;

namespace PdfToMarkdown.Application.Markdown;

/// <summary>Writes the Markdown of one logical document, following the output specification.</summary>
public sealed class MarkdownComposer(TimeProvider clock)
{
    public DocumentRenderResult Compose(LogicalDocument document)
    {
        var anchors = new AnchorFactory();
        var toc = new List<TocEntry>();
        var counters = new Counters();
        var body = RenderBody(document, anchors, toc, counters);

        var builder = new StringBuilder();
        builder.Append(RenderFrontMatter(document));
        builder.AppendLine();
        builder.Append("# ").AppendLine(document.Metadata.Title);
        builder.AppendLine();
        builder.Append(RenderTableOfContents(toc));
        builder.Append(body);

        return new DocumentRenderResult(
            Normalize(builder.ToString()),
            toc,
            counters.Tables,
            counters.Figures,
            counters.Unreadable);
    }

    private string RenderFrontMatter(LogicalDocument document)
    {
        var metadata = document.Metadata;
        var builder = new StringBuilder();

        builder.AppendLine("---");
        builder.Append("titulo: ").AppendLine(Quote(metadata.Title));
        builder.Append("versao: ").AppendLine(Quote(metadata.Version));
        builder.Append("fonte_url: ").AppendLine(Quote(metadata.SourceUrl));
        builder.Append("tipo: ").AppendLine(Quote(metadata.Kind));
        builder.Append("escopo: ").AppendLine(Quote(metadata.Scope));
        builder.Append("depende_de: ").AppendLine(metadata.DependsOn is null ? "null" : Quote(metadata.DependsOn));
        builder.Append("idioma: ").AppendLine(Quote(metadata.Language));
        builder.Append("extraido_em: ").AppendLine(
            Quote(clock.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        builder.AppendLine("partes:");

        foreach (var part in document.Parts)
        {
            builder.Append("  - arquivo: ").AppendLine(Quote(part.FileName));
            builder.Append("    paginas: ").AppendLine(part.PageCount.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append("total_paginas: ").AppendLine(document.TotalPages.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("---");

        return builder.ToString();
    }

    private static string RenderTableOfContents(IReadOnlyList<TocEntry> toc)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Sumário");
        builder.AppendLine();

        if (toc.Count == 0)
        {
            builder.AppendLine("(nenhum título identificado no documento)");
            builder.AppendLine();
            return builder.ToString();
        }

        foreach (var entry in toc)
        {
            var indent = entry.Level == 2 ? string.Empty : "  ";
            builder
                .Append(indent)
                .Append("- [")
                .Append(entry.Text)
                .Append("](#")
                .Append(entry.Anchor)
                .Append(") — ")
                .AppendLine(entry.PageReference);
        }

        builder.AppendLine();

        return builder.ToString();
    }

    private static string RenderBody(
        LogicalDocument document,
        AnchorFactory anchors,
        List<TocEntry> toc,
        Counters counters)
    {
        var builder = new StringBuilder();
        var footnotes = new List<FootnoteBlock>();
        var merged = !document.IsSinglePart;

        foreach (var part in document.Parts)
        {
            if (merged)
            {
                builder.Append("<!-- INÍCIO PARTE: ").Append(part.FileName).AppendLine(" -->");
                builder.AppendLine();
            }

            foreach (var page in part.Pages)
            {
                var reference = new PageReference(page.PageNumber, part.Label, merged);

                foreach (var block in page.Blocks)
                {
                    if (block is FootnoteBlock footnote)
                    {
                        footnotes.Add(footnote);
                        continue;
                    }

                    if (block is HeadingBlock heading)
                    {
                        FlushFootnotes(builder, footnotes);
                        RenderHeading(builder, heading, anchors, toc, reference);
                        continue;
                    }

                    RenderBlock(builder, block, reference, counters);
                }

                builder.AppendLine(reference.Marker);
                builder.AppendLine();
            }
        }

        FlushFootnotes(builder, footnotes);

        return builder.ToString();
    }

    private static void RenderHeading(
        StringBuilder builder,
        HeadingBlock heading,
        AnchorFactory anchors,
        List<TocEntry> toc,
        PageReference reference)
    {
        var level = Math.Clamp(heading.Level, 2, 6);
        var anchor = anchors.Create(heading.Text);

        if (level is 2 or 3)
        {
            toc.Add(new TocEntry(level, heading.Text, anchor, reference.Citation));
        }

        builder.Append(new string('#', level)).Append(' ').AppendLine(heading.Text);
        builder.AppendLine();
    }

    private static void FlushFootnotes(StringBuilder builder, List<FootnoteBlock> footnotes)
    {
        if (footnotes.Count == 0)
        {
            return;
        }

        foreach (var footnote in footnotes)
        {
            builder.Append("> Nota: ").AppendLine(footnote.Text);
            builder.AppendLine();
        }

        footnotes.Clear();
    }

    private static void RenderBlock(StringBuilder builder, ContentBlock block, PageReference reference, Counters counters)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                builder.AppendLine(paragraph.Text);
                builder.AppendLine();
                break;

            case ListBlock list:
                RenderList(builder, list);
                break;

            case CalloutBlock callout:
                builder
                    .Append(callout.Kind == CalloutKind.Warning ? "> ⚠️ **AVISO:** " : "> **Nota:** ")
                    .AppendLine(callout.Text);
                builder.AppendLine();
                break;

            case CodeBlock code:
                builder.AppendLine("```");
                foreach (var line in code.Lines)
                {
                    builder.AppendLine(line);
                }

                builder.AppendLine("```");
                builder.AppendLine();
                break;

            case TableBlock table:
                counters.Tables++;
                RenderTable(builder, table, reference);
                break;

            case FigureBlock figure:
                counters.Figures++;
                RenderFigure(builder, figure, reference);
                break;

            case UnreadableBlock unreadable:
                counters.Unreadable++;
                builder
                    .Append("> [ILEGÍVEL ")
                    .Append(reference.Citation)
                    .Append(": ")
                    .Append(unreadable.Reason)
                    .AppendLine("]");
                builder.AppendLine();
                break;
        }
    }

    private static void RenderList(StringBuilder builder, ListBlock list)
    {
        var number = list.Start;
        foreach (var item in list.Items)
        {
            builder
                .Append(list.Ordered ? (number++).ToString(CultureInfo.InvariantCulture) + ". " : "- ")
                .AppendLine(item);
        }

        builder.AppendLine();
    }

    private static void RenderFigure(StringBuilder builder, FigureBlock figure, PageReference reference)
    {
        builder.Append("> [FIGURA ").Append(reference.Citation).Append("] ");
        builder.AppendLine(figure.Caption.Length > 0 ? figure.Caption : "Imagem sem legenda no texto.");

        if (figure.Caption.Length > 0)
        {
            builder.Append("> Legenda original: \"").Append(figure.Caption).AppendLine("\"");
        }

        if (figure.EmbeddedText.Count > 0)
        {
            builder
                .Append("> Texto embutido: ")
                .AppendLine(string.Join(" | ", figure.EmbeddedText.Select(text => "\"" + text + "\"")));
            builder.AppendLine("> Descrição: transcrição integral do texto contido na imagem, na ordem de leitura.");
        }
        else
        {
            builder.AppendLine("> Descrição: imagem sem texto extraível — verificar no PDF original.");
        }

        builder.AppendLine();
    }

    private static void RenderTable(StringBuilder builder, TableBlock table, PageReference reference)
    {
        var columns = table.Header.Count;
        var rows = table.Rows.Select(row => Fit(row, columns)).ToList();

        if (table.Oversized)
        {
            builder
                .Append("> [TABELA ")
                .Append(reference.Citation)
                .Append(": ")
                .Append(table.Description)
                .AppendLine("]");
            builder.AppendLine();
            RenderFixedWidthTable(builder, table.Header, rows, columns);
            return;
        }

        builder.Append("| ").Append(string.Join(" | ", table.Header.Select(Escape))).AppendLine(" |");
        builder.Append('|').Append(string.Concat(Enumerable.Repeat("---|", columns))).AppendLine();

        foreach (var row in rows)
        {
            builder.Append("| ").Append(string.Join(" | ", row.Select(Escape))).AppendLine(" |");
        }

        builder.AppendLine();
    }

    private static void RenderFixedWidthTable(
        StringBuilder builder,
        IReadOnlyList<string> header,
        IReadOnlyList<IReadOnlyList<string>> rows,
        int columns)
    {
        var widths = new int[columns];
        for (var column = 0; column < columns; column++)
        {
            widths[column] = Math.Max(
                header[column].Length,
                rows.Count == 0 ? 0 : rows.Max(row => row[column].Length));
        }

        builder.AppendLine("```");
        builder.AppendLine(FixedRow(header, widths));
        builder.AppendLine(string.Join("  ", widths.Select(width => new string('-', Math.Max(width, 1)))));

        foreach (var row in rows)
        {
            builder.AppendLine(FixedRow(row, widths));
        }

        builder.AppendLine("```");
        builder.AppendLine();
    }

    private static string FixedRow(IReadOnlyList<string> cells, int[] widths) =>
        string.Join("  ", cells.Select((cell, index) => cell.PadRight(widths[index]))).TrimEnd();

    private static IReadOnlyList<string> Fit(IReadOnlyList<string> row, int columns)
    {
        if (row.Count == columns)
        {
            return row;
        }

        var cells = new List<string>(row);
        while (cells.Count < columns)
        {
            cells.Add(string.Empty);
        }

        if (cells.Count > columns)
        {
            var overflow = string.Join(" ", cells.Skip(columns - 1));
            cells = [.. cells.Take(columns - 1), overflow];
        }

        return cells;
    }

    private static string Escape(string cell) =>
        cell.Replace("|", @"\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>Collapses runs of blank lines so the file keeps a predictable shape.</summary>
    private static string Normalize(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var builder = new StringBuilder(markdown.Length);
        var blanks = 0;

        foreach (var line in lines)
        {
            if (line.Trim().Length == 0)
            {
                blanks++;
                if (blanks > 1)
                {
                    continue;
                }
            }
            else
            {
                blanks = 0;
            }

            builder.Append(line).Append('\n');
        }

        return builder.ToString().TrimEnd('\n') + "\n";
    }

    private sealed class Counters
    {
        public int Tables { get; set; }

        public int Figures { get; set; }

        public int Unreadable { get; set; }
    }
}
