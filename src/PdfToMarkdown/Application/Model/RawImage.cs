namespace PdfToMarkdown.Application.Model;

/// <summary>The placement of an image drawn on a PDF page.</summary>
public sealed record RawImage(double Left, double Right, double Bottom, double Top)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;

    public double Area => Width * Height;
}
