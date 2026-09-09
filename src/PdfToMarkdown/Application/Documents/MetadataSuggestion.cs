namespace PdfToMarkdown.Application.Documents;

/// <summary>Values suggested from the first PDF of a document; never assumed as confirmed.</summary>
public sealed record MetadataSuggestion(string Title, string Version);
