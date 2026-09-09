namespace PdfToMarkdown.Application.Model;

/// <summary>A word extracted from a PDF page, with the geometry needed for layout analysis.</summary>
/// <remarks>Coordinates follow the PDF convention: the Y axis grows upwards.</remarks>
public sealed record RawWord(
    string Text,
    double Left,
    double Right,
    double Bottom,
    double Top,
    double FontSize,
    string FontName,
    bool IsBold,
    bool IsItalic,
    bool IsMonospace)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;

    public double CenterX => (Left + Right) / 2d;
}
