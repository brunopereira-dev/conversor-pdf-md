namespace PdfToMarkdown.Application.Markdown;

/// <summary>One entry of the table of contents of a generated document.</summary>
public sealed record TocEntry(int Level, string Text, string Anchor, string PageReference);
