namespace PdfToMarkdown.Application.Abstractions;

/// <summary>Writes and reads back the generated Markdown files.</summary>
public interface IOutputStore
{
    Task WriteAsync(string directory, string fileName, string content, CancellationToken cancellationToken);

    Task<string> ReadAsync(string directory, string fileName, CancellationToken cancellationToken);

    bool Exists(string directory, string fileName);
}
