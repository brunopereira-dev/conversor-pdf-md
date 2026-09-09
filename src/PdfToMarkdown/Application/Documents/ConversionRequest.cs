using System.ComponentModel.DataAnnotations;

namespace PdfToMarkdown.Application.Documents;

/// <summary>Everything the form gathered for one conversion run.</summary>
public sealed record ConversionRequest(
    [property: Required(ErrorMessage = "A pasta de saída é obrigatória.")]
    string OutputDirectory,
    [property: MinLength(1, ErrorMessage = "É preciso ao menos um documento.")]
    IReadOnlyList<DocumentInput> Documents,
    IReadOnlyList<string> SelectedFiles);
