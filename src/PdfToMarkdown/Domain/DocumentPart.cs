namespace PdfToMarkdown.Domain;

/// <summary>One PDF file taking part in a logical document.</summary>
public sealed class DocumentPart(string filePath)
{
    public string FilePath { get; } = filePath;

    public string FileName { get; } = Path.GetFileName(filePath);

    /// <summary>Part label used on page markers: file name without extension.</summary>
    public string Label { get; } = Path.GetFileNameWithoutExtension(filePath);

    public int PageCount { get; private set; }

    public List<PageContent> Pages { get; } = [];

    public void AddPage(PageContent page)
    {
        Pages.Add(page);
        PageCount = Pages.Count;
    }
}
