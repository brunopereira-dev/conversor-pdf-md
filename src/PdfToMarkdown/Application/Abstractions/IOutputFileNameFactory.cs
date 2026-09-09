namespace PdfToMarkdown.Application.Abstractions;

/// <summary>Derives the .md file name of a document from its title.</summary>
public interface IOutputFileNameFactory
{
    /// <summary>Kebab case name without accents, ending in .md.</summary>
    string FromTitle(string? title);

    /// <summary>Adds an incremental suffix while the name is already taken in the same run.</summary>
    string EnsureUnique(string fileName, ISet<string> taken);
}
