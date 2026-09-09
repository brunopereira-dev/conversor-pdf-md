namespace PdfToMarkdown.Application.Documents;

/// <summary>Progress of a conversion run, reported to the user interface.</summary>
public sealed record ConversionProgress(int Completed, int Total, string Step);
