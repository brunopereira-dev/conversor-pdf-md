namespace PdfToMarkdown.Application.Model;

/// <summary>An entry of the PDF outline (bookmarks), used to corroborate detected headings.</summary>
public sealed record RawBookmark(string Title, int Level, int PageNumber);
