using PdfToMarkdown.Application.Documents;

namespace PdfToMarkdown.Application.Abstractions;

/// <summary>Validates the data typed on the form before a run starts.</summary>
public interface IDocumentInputValidator
{
    /// <summary>Returns one message per problem found; an empty list means the request is valid.</summary>
    IReadOnlyList<string> Validate(ConversionRequest request);
}
