using System.ComponentModel.DataAnnotations;

namespace PdfToMarkdown.Application.Documents;

/// <summary>One logical document as filled in by the user on the form.</summary>
public sealed record DocumentInput(
    [property: Required(ErrorMessage = "O nome do arquivo de saída é obrigatório.")]
    [property: RegularExpression(
        @"^[a-z0-9]+(-[a-z0-9]+)*\.md$",
        ErrorMessage = "O nome de saída deve estar em kebab-case, sem acentos, terminado em .md.")]
    [property: StringLength(120, ErrorMessage = "O nome de saída deve ter no máximo 120 caracteres.")]
    string OutputFileName,
    [property: StringLength(300, ErrorMessage = "O título deve ter no máximo 300 caracteres.")]
    string Title,
    bool TitleConfirmed,
    [property: StringLength(60, ErrorMessage = "A versão deve ter no máximo 60 caracteres.")]
    string Version,
    bool VersionConfirmed,
    [property: OptionalUrl(ErrorMessage = "A fonte deve ser uma URL absoluta (http, https ou ftp).")]
    [property: StringLength(500, ErrorMessage = "A URL de origem deve ter no máximo 500 caracteres.")]
    string SourceUrl,
    [property: StringLength(120, ErrorMessage = "O tipo deve ter no máximo 120 caracteres.")]
    string Kind,
    [property: StringLength(500, ErrorMessage = "O escopo deve ter no máximo 500 caracteres.")]
    string Scope,
    string? DependsOn,
    [property: MinLength(1, ErrorMessage = "O documento precisa de pelo menos um arquivo PDF.")]
    IReadOnlyList<string> PartFilePaths);
