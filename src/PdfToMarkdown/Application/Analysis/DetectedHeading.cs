namespace PdfToMarkdown.Application.Analysis;

/// <summary>A line recognised as a section title, with the level it should take in Markdown.</summary>
public sealed record DetectedHeading(int Level, string Text);
