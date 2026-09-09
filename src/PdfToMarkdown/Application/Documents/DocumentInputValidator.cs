using System.ComponentModel.DataAnnotations;
using PdfToMarkdown.Application.Abstractions;

namespace PdfToMarkdown.Application.Documents;

/// <inheritdoc />
public sealed class DocumentInputValidator : IDocumentInputValidator
{
    public IReadOnlyList<string> Validate(ConversionRequest request)
    {
        var messages = new List<string>();
        messages.AddRange(Annotations(request, "Execução"));

        if (!string.IsNullOrWhiteSpace(request.OutputDirectory) && !Directory.Exists(request.OutputDirectory))
        {
            messages.Add("Execução: a pasta de saída não existe.");
        }

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in request.Documents)
        {
            var label = document.OutputFileName is { Length: > 0 } name ? name : "documento sem nome";
            messages.AddRange(Annotations(document, label));

            foreach (var path in document.PartFilePaths)
            {
                if (!files.Add(path))
                {
                    messages.Add($"{label}: o arquivo \"{Path.GetFileName(path)}\" está em mais de um documento.");
                }

                if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    messages.Add($"{label}: \"{Path.GetFileName(path)}\" não é um arquivo .pdf.");
                }
                else if (!File.Exists(path))
                {
                    messages.Add($"{label}: o arquivo \"{Path.GetFileName(path)}\" não foi encontrado.");
                }
            }

            if (document.DependsOn is { Length: > 0 } dependency && dependency == document.OutputFileName)
            {
                messages.Add($"{label}: um documento não pode depender de si mesmo.");
            }
        }

        return messages;
    }

    private static IEnumerable<string> Annotations(object instance, string label)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

        return results.Select(result => $"{label}: {result.ErrorMessage}");
    }
}
