using System.Text;
using PdfToMarkdown.Application.Abstractions;

namespace PdfToMarkdown.Infrastructure.Storage;

/// <summary>Writes the generated files to disk in UTF-8 and reads them back for validation.</summary>
public sealed class FileSystemOutputStore : IOutputStore
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public async Task WriteAsync(string directory, string fileName, string content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, fileName), content, Utf8, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string> ReadAsync(string directory, string fileName, CancellationToken cancellationToken) =>
        await File.ReadAllTextAsync(Path.Combine(directory, fileName), Utf8, cancellationToken).ConfigureAwait(false);

    public bool Exists(string directory, string fileName) => File.Exists(Path.Combine(directory, fileName));
}
