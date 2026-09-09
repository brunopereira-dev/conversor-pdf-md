using PdfToMarkdown.Application.Abstractions;
using PdfToMarkdown.Application.Analysis;
using PdfToMarkdown.Application.Documents;

namespace PdfToMarkdown.Application.UseCases;

/// <inheritdoc />
public sealed class SuggestMetadataUseCase(IPdfContentReader reader) : ISuggestMetadataUseCase
{
    public async Task<MetadataSuggestion> ExecuteAsync(string pdfFilePath, CancellationToken cancellationToken)
    {
        var document = await reader.ReadPrefixAsync(pdfFilePath, 3, cancellationToken).ConfigureAwait(false);

        return new MetadataSuggestion(
            MetadataSuggester.SuggestTitle(document),
            MetadataSuggester.SuggestVersion(document));
    }
}
