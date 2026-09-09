namespace PdfToMarkdown.Domain;

/// <summary>An ordered set of PDF parts plus its metadata; produces exactly one .md file.</summary>
public sealed class LogicalDocument(string outputFileName, DocumentMetadata metadata, IReadOnlyList<DocumentPart> parts)
{
    public string OutputFileName { get; } = outputFileName;

    public DocumentMetadata Metadata { get; } = metadata;

    public IReadOnlyList<DocumentPart> Parts { get; } = parts;

    /// <summary>A document built from a single PDF uses the simple page marker format.</summary>
    public bool IsSinglePart => Parts.Count == 1;

    public int TotalPages => Parts.Sum(part => part.PageCount);

    public IEnumerable<PageContent> Pages => Parts.SelectMany(part => part.Pages);
}
