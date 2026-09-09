using PdfToMarkdown.Application.Abstractions;
using PdfToMarkdown.Application.Analysis;
using PdfToMarkdown.Application.Documents;
using PdfToMarkdown.Application.Markdown;
using PdfToMarkdown.Domain;

namespace PdfToMarkdown.Application.UseCases;

/// <summary>
/// Converts every logical document of the run: reads the PDFs, writes one .md per document,
/// always writes INDEX.md and then validates the files that were produced.
/// </summary>
public sealed class ConvertDocumentsUseCase(
    IPdfContentReader reader,
    IOutputStore store,
    IOutputValidator validator,
    IDocumentInputValidator inputValidator,
    IOutputFileNameFactory fileNames,
    MarkdownComposer markdown,
    IndexComposer index,
    ReportComposer report) : IConvertDocumentsUseCase
{
    public async Task<ConversionOutcome> ExecuteAsync(
        ConversionRequest request,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var errors = inputValidator.Validate(request);
        if (errors.Count > 0)
        {
            return new ConversionOutcome(
                request.OutputDirectory,
                [],
                [],
                errors,
                report.Compose(request.OutputDirectory, [], [], errors));
        }

        var warnings = new List<string>();
        var outcomes = new List<DocumentOutcome>();
        var entries = new List<IndexEntry>();
        var generated = new List<string>();

        var resolution = ResolveFileNames(request, warnings);
        var total = request.Documents.Sum(document => document.PartFilePaths.Count) + 1;
        var counter = new ProgressCounter();

        for (var position = 0; position < request.Documents.Count; position++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var input = request.Documents[position];
            var fileName = resolution.Names[position];

            var expected = counter.Completed + input.PartFilePaths.Count;

            try
            {
                var document = await BuildDocumentAsync(input, fileName, resolution, progress, total, counter, cancellationToken)
                    .ConfigureAwait(false);

                var rendered = markdown.Compose(document);
                await store.WriteAsync(request.OutputDirectory, fileName, rendered.Markdown, cancellationToken)
                    .ConfigureAwait(false);

                generated.Add(fileName);
                entries.Add(new IndexEntry(
                    fileName,
                    document.Metadata.Kind,
                    document.Metadata.Version,
                    document.TotalPages,
                    document.Metadata.Scope,
                    document.Parts.Select(part => part.FileName).ToList(),
                    rendered.TableOfContents,
                    document.Metadata.DependsOn));

                outcomes.Add(new DocumentOutcome(
                    fileName,
                    document.Metadata.Title,
                    true,
                    document.TotalPages,
                    rendered.Tables,
                    rendered.Figures,
                    rendered.UnreadableFragments,
                    DocumentWarnings(document),
                    null));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                outcomes.Add(new DocumentOutcome(fileName, input.Title, false, 0, 0, 0, 0, [], exception.Message));
                warnings.Add($"{fileName}: falha ao processar — {exception.Message}");
            }
            finally
            {
                counter.Completed = expected;
            }
        }

        progress?.Report(new ConversionProgress(counter.Completed, total, "Gerando INDEX.md"));
        await store.WriteAsync(request.OutputDirectory, IndexComposer.FileName, index.Compose(entries), cancellationToken)
            .ConfigureAwait(false);
        counter.Completed++;

        progress?.Report(new ConversionProgress(counter.Completed, total, "Validando a saída"));
        var validations = await validator
            .ValidateAsync(request.OutputDirectory, generated, request.SelectedFiles, cancellationToken)
            .ConfigureAwait(false);

        return new ConversionOutcome(
            request.OutputDirectory,
            outcomes,
            validations,
            warnings,
            report.Compose(request.OutputDirectory, outcomes, validations, warnings));
    }

    /// <summary>
    /// Resolves the final .md name of every document, adding an incremental suffix on collision
    /// so that a file generated in this run is never overwritten.
    /// </summary>
    private NameResolution ResolveFileNames(ConversionRequest request, List<string> warnings)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>(request.Documents.Count);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in request.Documents)
        {
            var declared = string.IsNullOrWhiteSpace(input.OutputFileName)
                ? fileNames.FromTitle(input.TitleConfirmed ? input.Title : null)
                : input.OutputFileName;

            var unique = fileNames.EnsureUnique(declared, taken);
            if (!string.Equals(unique, declared, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{declared}: nome já usado nesta execução, gravado como {unique}.");
            }

            names.Add(unique);
            map.TryAdd(declared, unique);
        }

        return new NameResolution(names, map);
    }

    private async Task<LogicalDocument> BuildDocumentAsync(
        DocumentInput input,
        string fileName,
        NameResolution resolution,
        IProgress<ConversionProgress>? progress,
        int total,
        ProgressCounter counter,
        CancellationToken cancellationToken)
    {
        var parts = new List<DocumentPart>();
        var paragraphs = new List<string>();

        foreach (var path in input.PartFilePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ConversionProgress(counter.Completed, total, $"Lendo {Path.GetFileName(path)}"));

            var raw = await reader.ReadAsync(path, cancellationToken).ConfigureAwait(false);
            var prepared = raw.Pages.Select(PagePreparer.Prepare).ToList();
            var noise = NoiseFilter.Build(prepared.Select(page => (page.Page, page.Lines)).ToList());
            var headings = HeadingDetector.Build(prepared.SelectMany(page => page.Lines), raw.Bookmarks);
            var analyzer = new PageAnalyzer(headings, noise);

            var part = new DocumentPart(path);
            foreach (var page in prepared)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var blocks = analyzer.Analyze(page);
                part.AddPage(new PageContent(part.Label, part.PageCount + 1, blocks));
                paragraphs.AddRange(blocks.OfType<ParagraphBlock>().Select(block => block.Text));
            }

            parts.Add(part);
            counter.Completed++;
        }

        var metadata = BuildMetadata(input, resolution);
        metadata.Language = LanguageDetector.Detect(paragraphs) ?? DocumentMetadata.NotInformed;

        return new LogicalDocument(fileName, metadata, parts);
    }

    private static DocumentMetadata BuildMetadata(DocumentInput input, NameResolution resolution)
    {
        var dependency = string.IsNullOrWhiteSpace(input.DependsOn)
            ? null
            : resolution.Map.GetValueOrDefault(input.DependsOn, input.DependsOn);

        return new DocumentMetadata(
            Confirmed(input.Title, input.TitleConfirmed),
            Confirmed(input.Version, input.VersionConfirmed),
            Filled(input.SourceUrl),
            Filled(input.Kind),
            Filled(input.Scope),
            dependency);
    }

    /// <summary>A suggested value that the user neither confirmed nor edited is not written as fact.</summary>
    private static string Confirmed(string value, bool confirmed) =>
        confirmed && !string.IsNullOrWhiteSpace(value) ? value.Trim() : DocumentMetadata.NotInformed;

    private static string Filled(string value) =>
        string.IsNullOrWhiteSpace(value) ? DocumentMetadata.NotInformed : value.Trim();

    private static IReadOnlyList<string> DocumentWarnings(LogicalDocument document)
    {
        var warnings = new List<string>();

        foreach (var part in document.Parts.Where(part => part.PageCount == 0))
        {
            warnings.Add($"a parte \"{part.FileName}\" não produziu páginas.");
        }

        var empty = document.Pages.Count(page => page.Blocks.Count == 0);
        if (empty > 0)
        {
            warnings.Add($"{empty} página(s) sem conteúdo extraído.");
        }

        return warnings;
    }

    private sealed record NameResolution(IReadOnlyList<string> Names, IReadOnlyDictionary<string, string> Map);

    private sealed class ProgressCounter
    {
        public int Completed { get; set; }
    }
}
