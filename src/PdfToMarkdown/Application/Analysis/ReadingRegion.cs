namespace PdfToMarkdown.Application.Analysis;

/// <summary>A block of lines that must be read as a unit, in the order they appear here.</summary>
public sealed record ReadingRegion(IReadOnlyList<TextLine> Lines, double Left, double Right)
{
    public double Width => Right - Left;
}
