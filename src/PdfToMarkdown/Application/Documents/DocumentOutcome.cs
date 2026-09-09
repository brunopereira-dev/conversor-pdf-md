namespace PdfToMarkdown.Application.Documents;

/// <summary>What happened to one logical document during the run.</summary>
public sealed record DocumentOutcome(
    string OutputFileName,
    string Title,
    bool Succeeded,
    int Pages,
    int Tables,
    int Figures,
    int UnreadableFragments,
    IReadOnlyList<string> Warnings,
    string? Error);
