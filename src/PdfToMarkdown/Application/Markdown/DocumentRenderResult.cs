namespace PdfToMarkdown.Application.Markdown;

/// <summary>The Markdown of one logical document plus the counters shown in the report.</summary>
public sealed record DocumentRenderResult(
    string Markdown,
    IReadOnlyList<TocEntry> TableOfContents,
    int Tables,
    int Figures,
    int UnreadableFragments);
