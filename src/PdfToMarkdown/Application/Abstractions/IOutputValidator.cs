using PdfToMarkdown.Application.Documents;

namespace PdfToMarkdown.Application.Abstractions;

/// <summary>Runs the mandatory checks over the files already written to disk.</summary>
public interface IOutputValidator
{
    Task<IReadOnlyList<ValidationFinding>> ValidateAsync(
        string directory,
        IReadOnlyList<string> generatedFiles,
        IReadOnlyList<string> selectedFiles,
        CancellationToken cancellationToken);
}
