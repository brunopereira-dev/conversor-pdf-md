namespace PdfToMarkdown.Application.Model;

/// <summary>One physical page of a PDF, as read from disk.</summary>
public sealed record RawPage(
    int Number,
    double Width,
    double Height,
    IReadOnlyList<RawWord> Words,
    IReadOnlyList<RawImage> Images,
    string? ExtractionError);
