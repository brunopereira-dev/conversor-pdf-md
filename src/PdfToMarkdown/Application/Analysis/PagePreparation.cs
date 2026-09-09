using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>A page with its words already split between flowing text and text printed over images.</summary>
public sealed record PagePreparation(RawPage Page, List<TextLine> Lines, List<FigurePlacement> Figures);

/// <summary>An image of the page together with the text printed inside it.</summary>
public sealed record FigurePlacement(RawImage Image, List<TextLine> EmbeddedLines);
