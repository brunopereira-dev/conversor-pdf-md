using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Abstractions;

/// <summary>Reads a PDF file from disk into the raw model consumed by the analysis services.</summary>
public interface IPdfContentReader
{
    Task<RawPdfDocument> ReadAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>Reads only the first pages, enough to suggest metadata.</summary>
    Task<RawPdfDocument> ReadPrefixAsync(string filePath, int pageCount, CancellationToken cancellationToken);
}
