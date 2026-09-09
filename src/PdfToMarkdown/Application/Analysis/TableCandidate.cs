namespace PdfToMarkdown.Application.Analysis;

/// <summary>A table recovered from a run of aligned lines.</summary>
public sealed record TableCandidate(
    IReadOnlyList<string> Header,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int ConsumedLines);
