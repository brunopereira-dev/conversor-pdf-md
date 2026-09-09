namespace PdfToMarkdown.Domain;

/// <summary>Metadata describing a logical document, as declared by the user.</summary>
public sealed class DocumentMetadata(
    string title,
    string version,
    string sourceUrl,
    string kind,
    string scope,
    string? dependsOn)
{
    /// <summary>Literal written for any field the user neither filled nor confirmed.</summary>
    public const string NotInformed = "(não informado)";

    public string Title { get; } = title;

    public string Version { get; } = version;

    public string SourceUrl { get; } = sourceUrl;

    public string Kind { get; } = kind;

    public string Scope { get; } = scope;

    /// <summary>Output file name of the document this one depends on, or <c>null</c>.</summary>
    public string? DependsOn { get; } = dependsOn;

    public string Language { get; set; } = NotInformed;
}
