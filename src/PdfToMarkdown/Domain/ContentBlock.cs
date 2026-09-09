namespace PdfToMarkdown.Domain;

/// <summary>A single piece of page content already interpreted from the PDF layout.</summary>
public abstract class ContentBlock
{
}

public sealed class HeadingBlock(int level, string text) : ContentBlock
{
    public int Level { get; } = level;

    public string Text { get; } = text;
}

public sealed class ParagraphBlock(string text) : ContentBlock
{
    public string Text { get; } = text;
}

public sealed class ListBlock(bool ordered, IReadOnlyList<string> items, int start) : ContentBlock
{
    public bool Ordered { get; } = ordered;

    public IReadOnlyList<string> Items { get; } = items;

    /// <summary>First number of an ordered list, as printed in the PDF.</summary>
    public int Start { get; } = start;
}

public sealed class CalloutBlock(CalloutKind kind, string text) : ContentBlock
{
    public CalloutKind Kind { get; } = kind;

    public string Text { get; } = text;
}

public sealed class CodeBlock(IReadOnlyList<string> lines) : ContentBlock
{
    public IReadOnlyList<string> Lines { get; } = lines;
}

public sealed class TableBlock(
    IReadOnlyList<string> header,
    IReadOnlyList<IReadOnlyList<string>> rows,
    bool oversized,
    string description) : ContentBlock
{
    public IReadOnlyList<string> Header { get; } = header;

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; } = rows;

    /// <summary>Table too wide for the Markdown pipe format; rendered as a fixed width code block.</summary>
    public bool Oversized { get; } = oversized;

    public string Description { get; } = description;
}

public sealed class FigureBlock(string caption, IReadOnlyList<string> embeddedText) : ContentBlock
{
    public string Caption { get; } = caption;

    public IReadOnlyList<string> EmbeddedText { get; } = embeddedText;
}

public sealed class FootnoteBlock(string text) : ContentBlock
{
    public string Text { get; } = text;
}

public sealed class UnreadableBlock(string reason) : ContentBlock
{
    public string Reason { get; } = reason;
}
