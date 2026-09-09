using System.Globalization;
using System.Text.RegularExpressions;
using PdfToMarkdown.Application.Abstractions;
using PdfToMarkdown.Application.Documents;
using PdfToMarkdown.Application.Markdown;

namespace PdfToMarkdown.Application.UseCases;

/// <summary>The eight mandatory checks of the specification, run over the generated files.</summary>
public sealed partial class OutputValidator(IOutputStore store) : IOutputValidator
{
    public async Task<IReadOnlyList<ValidationFinding>> ValidateAsync(
        string directory,
        IReadOnlyList<string> generatedFiles,
        IReadOnlyList<string> selectedFiles,
        CancellationToken cancellationToken)
    {
        var findings = new List<ValidationFinding>();
        var consumed = new List<string>();

        foreach (var fileName in generatedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await store.ReadAsync(directory, fileName, cancellationToken).ConfigureAwait(false);
            var file = MarkdownFile.Parse(content);
            consumed.AddRange(file.PartFiles);

            findings.Add(CheckMarkerCount(fileName, file));
            findings.Add(CheckMarkerSequence(fileName, file));
            findings.Add(CheckTableOfContents(fileName, file));
            findings.Add(CheckTables(fileName, file));
            findings.Add(CheckPageSum(fileName, file));
            findings.Add(CheckPartSequences(fileName, file));
        }

        findings.AddRange(await CheckIndexAsync(directory, generatedFiles, cancellationToken).ConfigureAwait(false));
        findings.Add(CheckSelectedFiles(selectedFiles, consumed));

        return findings;
    }

    private static ValidationFinding CheckMarkerCount(string fileName, MarkdownFile file)
    {
        var passed = file.Markers.Count == file.TotalPages;

        return new ValidationFinding(
            1,
            "Marcadores de página iguais a total_paginas",
            fileName,
            passed,
            $"{file.Markers.Count} marcador(es) para total_paginas = {file.TotalPages}.");
    }

    private static ValidationFinding CheckMarkerSequence(string fileName, MarkdownFile file)
    {
        var problems = new List<string>();

        foreach (var group in file.Markers.GroupBy(marker => marker.Part))
        {
            var numbers = group.Select(marker => marker.Page).ToList();
            var duplicates = numbers.GroupBy(number => number).Where(item => item.Count() > 1).Select(item => item.Key).ToList();
            if (duplicates.Count > 0)
            {
                problems.Add($"repetidos em [{string.Join(", ", duplicates)}]");
            }

            for (var index = 1; index < numbers.Count; index++)
            {
                if (numbers[index] != numbers[index - 1] + 1)
                {
                    problems.Add($"salto de p.{numbers[index - 1]} para p.{numbers[index]}");
                    break;
                }
            }
        }

        return new ValidationFinding(
            2,
            "Marcadores em sequência, sem lacunas nem repetições",
            fileName,
            problems.Count == 0,
            problems.Count == 0 ? "Sequência contínua." : string.Join("; ", problems));
    }

    private static ValidationFinding CheckTableOfContents(string fileName, MarkdownFile file)
    {
        var missing = file.TocTitles.Where(title => !file.BodyHeadings.Contains(title)).ToList();

        return new ValidationFinding(
            3,
            "Títulos do sumário presentes no corpo",
            fileName,
            missing.Count == 0,
            missing.Count == 0
                ? $"{file.TocTitles.Count} título(s) conferido(s)."
                : $"ausentes no corpo: {string.Join(" | ", missing.Take(5))}");
    }

    private static ValidationFinding CheckTables(string fileName, MarkdownFile file)
    {
        var broken = file.Tables.Where(table => table.Distinct().Count() > 1).ToList();

        return new ValidationFinding(
            4,
            "Tabelas com número de colunas consistente",
            fileName,
            broken.Count == 0,
            broken.Count == 0
                ? $"{file.Tables.Count} tabela(s) conferida(s)."
                : $"{broken.Count} tabela(s) com linhas de larguras diferentes.");
    }

    private static ValidationFinding CheckPageSum(string fileName, MarkdownFile file)
    {
        var sum = file.PartPages.Sum();

        return new ValidationFinding(
            6,
            "Soma das páginas das partes igual a total_paginas",
            fileName,
            sum == file.TotalPages,
            $"soma = {sum}, total_paginas = {file.TotalPages}.");
    }

    private static ValidationFinding CheckPartSequences(string fileName, MarkdownFile file)
    {
        if (file.PartFiles.Count <= 1)
        {
            return new ValidationFinding(7, "Sequência de páginas por parte", fileName, true, "Documento com uma única parte.");
        }

        var problems = new List<string>();
        foreach (var group in file.Markers.GroupBy(marker => marker.Part))
        {
            var numbers = group.Select(marker => marker.Page).ToList();
            if (numbers.Count == 0 || numbers[0] != 1)
            {
                problems.Add($"parte \"{group.Key}\" não reinicia em p.1");
                continue;
            }

            if (numbers.Where((number, index) => number != index + 1).Any())
            {
                problems.Add($"parte \"{group.Key}\" não é contínua");
            }
        }

        return new ValidationFinding(
            7,
            "Cada parte reinicia em 1 e é contínua",
            fileName,
            problems.Count == 0,
            problems.Count == 0 ? "Todas as partes conferidas." : string.Join("; ", problems));
    }

    private async Task<IReadOnlyList<ValidationFinding>> CheckIndexAsync(
        string directory,
        IReadOnlyList<string> generatedFiles,
        CancellationToken cancellationToken)
    {
        if (!store.Exists(directory, IndexComposer.FileName))
        {
            return
            [
                new ValidationFinding(5, "INDEX.md cobre todos os documentos", IndexComposer.FileName, false, "INDEX.md não foi gerado.")
            ];
        }

        var index = await store.ReadAsync(directory, IndexComposer.FileName, cancellationToken).ConfigureAwait(false);
        var links = IndexLinkPattern().Matches(index).Select(match => match.Groups[1].Value).Distinct().ToList();

        var notListed = generatedFiles.Where(file => !links.Contains(file, StringComparer.Ordinal)).ToList();
        var broken = links.Where(link => !store.Exists(directory, link)).ToList();

        return
        [
            new ValidationFinding(
                5,
                "Todo .md gerado está listado no INDEX.md",
                IndexComposer.FileName,
                notListed.Count == 0,
                notListed.Count == 0
                    ? $"{generatedFiles.Count} documento(s) listado(s)."
                    : $"fora do índice: {string.Join(", ", notListed)}"),
            new ValidationFinding(
                5,
                "Todo link do INDEX.md aponta para arquivo existente",
                IndexComposer.FileName,
                broken.Count == 0,
                broken.Count == 0
                    ? $"{links.Count} link(s) conferido(s)."
                    : $"links quebrados: {string.Join(", ", broken)}")
        ];
    }

    private static ValidationFinding CheckSelectedFiles(IReadOnlyList<string> selectedFiles, IReadOnlyList<string> consumed)
    {
        var expected = selectedFiles.Select(Path.GetFileName).OfType<string>().ToList();
        var missing = expected.Where(file => !consumed.Contains(file, StringComparer.OrdinalIgnoreCase)).ToList();

        return new ValidationFinding(
            8,
            "Todo PDF selecionado foi consumido por algum documento",
            "execução",
            missing.Count == 0,
            missing.Count == 0
                ? $"{expected.Count} arquivo(s) consumido(s)."
                : $"não consumidos: {string.Join(", ", missing)}");
    }

    [GeneratedRegex(@"\]\(\./([^)]+\.md)\)", RegexOptions.CultureInvariant)]
    private static partial Regex IndexLinkPattern();

    [GeneratedRegex(@"<!--\s*p\.(\d+)(?:\s*\|\s*(.+?))?\s*-->", RegexOptions.CultureInvariant)]
    private static partial Regex MarkerPattern();

    [GeneratedRegex(@"^total_paginas:\s*(\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TotalPagesPattern();

    [GeneratedRegex(@"^\s*-\s*arquivo:\s*""(.*)""\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PartFilePattern();

    [GeneratedRegex(@"^\s+paginas:\s*(\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PartPagesPattern();

    [GeneratedRegex(@"^\s*-\s*\[(.+?)\]\(#.+?\)", RegexOptions.CultureInvariant)]
    private static partial Regex TocEntryPattern();

    /// <summary>A generated file parsed back from disk, so the checks run over the real output.</summary>
    private sealed record MarkdownFile(
        int TotalPages,
        IReadOnlyList<string> PartFiles,
        IReadOnlyList<int> PartPages,
        IReadOnlyList<(int Page, string Part)> Markers,
        IReadOnlyList<string> TocTitles,
        HashSet<string> BodyHeadings,
        IReadOnlyList<List<int>> Tables)
    {
        public static MarkdownFile Parse(string content)
        {
            var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

            var totalPages = 0;
            var partFiles = new List<string>();
            var partPages = new List<int>();
            var markers = new List<(int Page, string Part)>();
            var tocTitles = new List<string>();
            var bodyHeadings = new HashSet<string>(StringComparer.Ordinal);
            var tables = new List<List<int>>();

            var inFrontMatter = false;
            var frontMatterClosed = false;
            var inToc = false;
            var inCode = false;
            List<int>? currentTable = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    inCode = !inCode;
                    continue;
                }

                if (!frontMatterClosed && line.Trim() == "---")
                {
                    frontMatterClosed = inFrontMatter;
                    inFrontMatter = !inFrontMatter;
                    continue;
                }

                if (inFrontMatter)
                {
                    ParseFrontMatterLine(line, ref totalPages, partFiles, partPages);
                    continue;
                }

                foreach (Match marker in MarkerPattern().Matches(line))
                {
                    markers.Add((
                        int.Parse(marker.Groups[1].Value, CultureInfo.InvariantCulture),
                        marker.Groups[2].Success ? marker.Groups[2].Value.Trim() : string.Empty));
                }

                if (line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("### ", StringComparison.Ordinal))
                {
                    var text = line[(line.IndexOf(' ', StringComparison.Ordinal) + 1)..].Trim();
                    inToc = text == "Sumário";
                    if (!inToc)
                    {
                        bodyHeadings.Add(text);
                    }

                    continue;
                }

                if (line.StartsWith('#'))
                {
                    inToc = false;
                    continue;
                }

                if (inToc)
                {
                    var entry = TocEntryPattern().Match(line);
                    if (entry.Success)
                    {
                        tocTitles.Add(entry.Groups[1].Value.Trim());
                    }

                    continue;
                }

                if (!inCode && line.TrimStart().StartsWith('|'))
                {
                    currentTable ??= [];
                    currentTable.Add(ColumnCount(line));
                    continue;
                }

                if (currentTable is not null)
                {
                    tables.Add(currentTable);
                    currentTable = null;
                }
            }

            if (currentTable is not null)
            {
                tables.Add(currentTable);
            }

            return new MarkdownFile(totalPages, partFiles, partPages, markers, tocTitles, bodyHeadings, tables);
        }

        private static void ParseFrontMatterLine(string line, ref int totalPages, List<string> partFiles, List<int> partPages)
        {
            var total = TotalPagesPattern().Match(line);
            if (total.Success)
            {
                totalPages = int.Parse(total.Groups[1].Value, CultureInfo.InvariantCulture);
                return;
            }

            var partFile = PartFilePattern().Match(line);
            if (partFile.Success)
            {
                partFiles.Add(partFile.Groups[1].Value);
                return;
            }

            var pages = PartPagesPattern().Match(line);
            if (pages.Success)
            {
                partPages.Add(int.Parse(pages.Groups[1].Value, CultureInfo.InvariantCulture));
            }
        }

        private static int ColumnCount(string line)
        {
            var cleaned = line.Trim().Replace(@"\|", "\u0001", StringComparison.Ordinal).Trim('|');

            return cleaned.Split('|').Length;
        }
    }
}
