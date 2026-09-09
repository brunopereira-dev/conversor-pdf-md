namespace PdfToMarkdown.Application.Markdown;

/// <summary>Identifies a page for markers and citations: page number plus part label when merged.</summary>
public sealed record PageReference(int Page, string PartLabel, bool Merged)
{
    /// <summary>Page marker written at the end of the page content.</summary>
    public string Marker => Merged ? $"<!-- p.{Page} | {PartLabel} -->" : $"<!-- p.{Page} -->";

    /// <summary>Human readable citation, as used in the table of contents and in INDEX.md.</summary>
    public string Citation => Merged ? $"p.{Page}, {PartLabel}" : $"p.{Page}";
}
