namespace PdfToMarkdown.Application.Documents;

/// <summary>Full result of a conversion run, including the report shown on screen.</summary>
public sealed record ConversionOutcome(
    string OutputDirectory,
    IReadOnlyList<DocumentOutcome> Documents,
    IReadOnlyList<ValidationFinding> Validations,
    IReadOnlyList<string> Warnings,
    string Report);
