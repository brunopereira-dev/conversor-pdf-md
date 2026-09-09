namespace PdfToMarkdown.Domain;

/// <summary>The interpreted content of one physical page of one PDF part.</summary>
public sealed class PageContent(string partLabel, int pageNumber, IReadOnlyList<ContentBlock> blocks)
{
    public string PartLabel { get; } = partLabel;

    /// <summary>Page number inside its own part; restarts at 1 for every part.</summary>
    public int PageNumber { get; } = pageNumber;

    public IReadOnlyList<ContentBlock> Blocks { get; } = blocks;
}
