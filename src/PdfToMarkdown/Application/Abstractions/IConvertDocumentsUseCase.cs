using PdfToMarkdown.Application.Documents;

namespace PdfToMarkdown.Application.Abstractions;

/// <summary>Converts every logical document of a run and validates the generated output.</summary>
public interface IConvertDocumentsUseCase
{
    Task<ConversionOutcome> ExecuteAsync(
        ConversionRequest request,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken);
}
