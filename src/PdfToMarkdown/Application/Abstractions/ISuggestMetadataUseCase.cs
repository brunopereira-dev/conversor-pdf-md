using PdfToMarkdown.Application.Documents;

namespace PdfToMarkdown.Application.Abstractions;

/// <summary>Suggests title and version from a PDF, for the user to confirm or change.</summary>
public interface ISuggestMetadataUseCase
{
    Task<MetadataSuggestion> ExecuteAsync(string pdfFilePath, CancellationToken cancellationToken);
}
