namespace PdfToMarkdown.Application.Markdown;

/// <summary>One generated document as listed in INDEX.md.</summary>
public sealed record IndexEntry(
    string FileName,
    string Kind,
    string Version,
    int Pages,
    string Scope,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyList<TocEntry> TableOfContents,
    string? DependsOn);
