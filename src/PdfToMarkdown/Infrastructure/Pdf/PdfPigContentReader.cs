using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Util;
using PdfToMarkdown.Application.Abstractions;
using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Infrastructure.Pdf;

/// <summary>Reads PDF files with PdfPig, keeping the geometry the analysis services need.</summary>
public sealed class PdfPigContentReader : IPdfContentReader
{
    private static readonly ParsingOptions Options = new()
    {
        UseLenientParsing = true,
        SkipMissingFonts = true
    };

    public Task<RawPdfDocument> ReadAsync(string filePath, CancellationToken cancellationToken) =>
        Task.Run(() => Read(filePath, null, cancellationToken), cancellationToken);

    public Task<RawPdfDocument> ReadPrefixAsync(string filePath, int pageCount, CancellationToken cancellationToken) =>
        Task.Run(() => Read(filePath, pageCount, cancellationToken), cancellationToken);

    private static RawPdfDocument Read(string filePath, int? limit, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(filePath, Options);

        var count = limit is null ? document.NumberOfPages : Math.Min(limit.Value, document.NumberOfPages);
        var pages = new List<RawPage>(count);

        for (var number = 1; number <= count; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages.Add(ReadPage(document, number));
        }

        return new RawPdfDocument(filePath, document.Information.Title, ReadBookmarks(document), pages);
    }

    private static RawPage ReadPage(PdfDocument document, int number)
    {
        try
        {
            var page = document.GetPage(number);
            var words = page.GetWords(DefaultWordExtractor.Instance).Select(ToRawWord).ToList();

            return new RawPage(number, page.Width, page.Height, words, ReadImages(page), null);
        }
        catch (Exception exception)
        {
            return new RawPage(number, 612, 792, [], [], $"falha ao extrair a página: {exception.Message}");
        }
    }

    private static List<RawImage> ReadImages(Page page)
    {
        try
        {
            return page.GetImages()
                .Select(image => image.BoundingBox)
                .Select(bounds => new RawImage(bounds.Left, bounds.Right, bounds.Bottom, bounds.Top))
                .Where(image => image.Width > 0 && image.Height > 0)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static RawWord ToRawWord(Word word)
    {
        var box = word.BoundingBox;
        var size = word.Letters.Count == 0 ? 0 : word.Letters.Max(letter => letter.PointSize);
        if (size <= 0)
        {
            size = Math.Abs(box.Top - box.Bottom);
        }

        var fontName = word.FontName ?? string.Empty;
        var bold = word.Letters.Count > 0
            && word.Letters.Count(letter => letter.FontDetails.IsBold) * 2 >= word.Letters.Count;
        var italic = word.Letters.Count > 0
            && word.Letters.Count(letter => letter.FontDetails.IsItalic) * 2 >= word.Letters.Count;

        return new RawWord(
            word.Text,
            Math.Min(box.Left, box.Right),
            Math.Max(box.Left, box.Right),
            Math.Min(box.Bottom, box.Top),
            Math.Max(box.Bottom, box.Top),
            size,
            fontName,
            bold || fontName.Contains("bold", StringComparison.OrdinalIgnoreCase),
            italic || fontName.Contains("italic", StringComparison.OrdinalIgnoreCase),
            IsMonospace(fontName));
    }

    private static bool IsMonospace(string fontName) =>
        fontName.Contains("mono", StringComparison.OrdinalIgnoreCase)
        || fontName.Contains("courier", StringComparison.OrdinalIgnoreCase)
        || fontName.Contains("consol", StringComparison.OrdinalIgnoreCase);

    private static List<RawBookmark> ReadBookmarks(PdfDocument document)
    {
        if (!document.TryGetBookmarks(out var bookmarks, false))
        {
            return [];
        }

        return bookmarks.GetNodes()
            .OfType<DocumentBookmarkNode>()
            .Select(node => new RawBookmark(node.Title ?? string.Empty, node.Level, node.PageNumber))
            .Where(bookmark => bookmark.Title.Length > 0)
            .ToList();
    }
}
