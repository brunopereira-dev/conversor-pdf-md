namespace PdfToMarkdown.Application.Model;

/// <summary>The raw content of a single PDF file.</summary>
public sealed record RawPdfDocument(
    string FilePath,
    string? Title,
    IReadOnlyList<RawBookmark> Bookmarks,
    IReadOnlyList<RawPage> Pages);
