using PdfToMarkdown.Application.Model;

namespace PdfToMarkdown.Application.Analysis;

/// <summary>
/// Splits the words of a page between the flowing text and the labels printed inside figures,
/// so that neither is lost and neither is counted twice.
/// </summary>
public static class PagePreparer
{
    private const double MinimumImageAreaRatio = 0.015;
    private const double MaximumImageAreaRatio = 0.60;
    private const double MinimumImageSide = 28;

    public static PagePreparation Prepare(RawPage page)
    {
        var pageArea = page.Width * page.Height;
        var images = page.Images
            .Where(image => image.Width >= MinimumImageSide && image.Height >= MinimumImageSide)
            .Where(image => pageArea <= 0 || image.Area >= pageArea * MinimumImageAreaRatio)
            .DistinctBy(image => (Math.Round(image.Left, 1), Math.Round(image.Bottom, 1), Math.Round(image.Right, 1), Math.Round(image.Top, 1)))
            .OrderByDescending(image => image.Top)
            .ToList();

        var capturing = images
            .Where(image => pageArea <= 0 || image.Area <= pageArea * MaximumImageAreaRatio)
            .ToList();

        var embedded = new Dictionary<RawImage, List<RawWord>>();
        var body = new List<RawWord>();

        foreach (var word in page.Words)
        {
            var host = capturing.FirstOrDefault(image => Contains(image, word));
            if (host is null)
            {
                body.Add(word);
                continue;
            }

            if (!embedded.TryGetValue(host, out var words))
            {
                words = [];
                embedded[host] = words;
            }

            words.Add(word);
        }

        var figures = images
            .Select(image => new FigurePlacement(
                image,
                embedded.TryGetValue(image, out var words) ? TextLineBuilder.Build(words) : []))
            .ToList();

        return new PagePreparation(page, TextLineBuilder.Build(body), figures);
    }

    private static bool Contains(RawImage image, RawWord word) =>
        word.CenterX >= image.Left && word.CenterX <= image.Right
        && (word.Bottom + word.Top) / 2d >= image.Bottom && (word.Bottom + word.Top) / 2d <= image.Top;
}
