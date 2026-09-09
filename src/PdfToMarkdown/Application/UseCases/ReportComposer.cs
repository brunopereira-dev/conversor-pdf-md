using System.Globalization;
using System.Text;
using PdfToMarkdown.Application.Documents;

namespace PdfToMarkdown.Application.UseCases;

/// <summary>Builds the end of run report shown on screen.</summary>
public sealed class ReportComposer
{
    public string Compose(
        string outputDirectory,
        IReadOnlyList<DocumentOutcome> documents,
        IReadOnlyList<ValidationFinding> validations,
        IReadOnlyList<string> warnings)
    {
        var builder = new StringBuilder();
        var succeeded = documents.Where(document => document.Succeeded).ToList();

        builder.AppendLine("RELATÓRIO DA CONVERSÃO");
        builder.AppendLine(new string('=', 60));
        builder.Append("Pasta de saída: ").AppendLine(outputDirectory);
        builder.AppendLine();
        builder.Append("Documentos gerados: ").AppendLine(Number(succeeded.Count));
        builder.Append("Documentos com falha: ").AppendLine(Number(documents.Count - succeeded.Count));
        builder.Append("Páginas processadas: ").AppendLine(Number(succeeded.Sum(document => document.Pages)));
        builder.Append("Tabelas: ").AppendLine(Number(succeeded.Sum(document => document.Tables)));
        builder.Append("Figuras: ").AppendLine(Number(succeeded.Sum(document => document.Figures)));
        builder.Append("Trechos ilegíveis: ").AppendLine(Number(succeeded.Sum(document => document.UnreadableFragments)));
        builder.AppendLine();

        builder.AppendLine("DOCUMENTOS");
        builder.AppendLine(new string('-', 60));
        foreach (var document in documents)
        {
            if (document.Succeeded)
            {
                builder
                    .Append("[OK]    ")
                    .Append(document.OutputFileName)
                    .Append(" — ")
                    .Append(Number(document.Pages)).Append(" página(s), ")
                    .Append(Number(document.Tables)).Append(" tabela(s), ")
                    .Append(Number(document.Figures)).Append(" figura(s), ")
                    .Append(Number(document.UnreadableFragments)).AppendLine(" trecho(s) ilegível(is)");
            }
            else
            {
                builder
                    .Append("[FALHA] ")
                    .Append(document.OutputFileName)
                    .Append(" — ")
                    .AppendLine(document.Error);
            }

            foreach (var warning in document.Warnings)
            {
                builder.Append("        aviso: ").AppendLine(warning);
            }
        }

        builder.AppendLine();
        builder.AppendLine("AVISOS");
        builder.AppendLine(new string('-', 60));
        if (warnings.Count == 0)
        {
            builder.AppendLine("(nenhum)");
        }

        foreach (var warning in warnings)
        {
            builder.Append("- ").AppendLine(warning);
        }

        builder.AppendLine();
        builder.AppendLine("VALIDAÇÃO OBRIGATÓRIA");
        builder.AppendLine(new string('-', 60));
        foreach (var finding in validations)
        {
            builder
                .Append(finding.Passed ? "[OK]    " : "[FALHA] ")
                .Append(finding.RuleNumber.ToString(CultureInfo.InvariantCulture))
                .Append(". ")
                .Append(finding.RuleName)
                .Append(" (")
                .Append(finding.Target)
                .Append("): ")
                .AppendLine(finding.Detail);
        }

        return builder.ToString();
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
