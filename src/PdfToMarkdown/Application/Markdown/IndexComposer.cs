using System.Globalization;
using System.Text;

namespace PdfToMarkdown.Application.Markdown;

/// <summary>Writes INDEX.md, the entry point the LLM reads before deciding where to look.</summary>
public sealed class IndexComposer(TimeProvider clock)
{
    public const string FileName = "INDEX.md";

    private const int TopicLimit = 40;

    public string Compose(IReadOnlyList<IndexEntry> entries)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Índice da Base de Conhecimento");
        builder.AppendLine();
        builder
            .Append("Gerado em: ")
            .AppendLine(clock.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        builder
            .Append("Documentos: ")
            .Append(entries.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" | Páginas totais: ")
            .AppendLine(entries.Sum(entry => entry.Pages).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine("## Documentos");
        builder.AppendLine();

        foreach (var entry in entries)
        {
            AppendDocument(builder, entry);
        }

        AppendHowTo(builder);
        AppendTopicMap(builder, entries);

        return builder.ToString().TrimEnd('\n') + "\n";
    }

    private static void AppendDocument(StringBuilder builder, IndexEntry entry)
    {
        builder
            .Append("### [")
            .Append(entry.FileName)
            .Append("](./")
            .Append(entry.FileName)
            .AppendLine(")");
        builder.Append("- **Tipo:** ").AppendLine(entry.Kind);
        builder
            .Append("- **Versão:** ")
            .Append(entry.Version)
            .Append(" | **Páginas:** ")
            .AppendLine(entry.Pages.ToString(CultureInfo.InvariantCulture));
        builder.Append("- **Escopo:** ").AppendLine(entry.Scope);
        builder
            .Append("- **Origem:** ")
            .AppendLine(string.Join(", ", entry.SourceFiles.Select(file => "`" + file + "`")));
        builder.AppendLine("- **Seções de nível 2:**");

        var sections = entry.TableOfContents.Where(item => item.Level == 2).ToList();
        if (sections.Count == 0)
        {
            builder.AppendLine("  - (nenhuma)");
        }

        foreach (var section in sections)
        {
            builder.Append("  - ").AppendLine(section.Text);
        }

        builder
            .Append("- **Depende de:** ")
            .AppendLine(entry.DependsOn is { Length: > 0 } dependency ? "`" + dependency + "`" : "—");
        builder.AppendLine();
    }

    private static void AppendHowTo(StringBuilder builder)
    {
        builder.AppendLine("## Como consultar");
        builder.AppendLine();
        builder.AppendLine("1. Identifique o documento pelo escopo acima.");
        builder.AppendLine("2. Para addenda, consultar **também** o documento base indicado em \"Depende de\".");
        builder.AppendLine("3. Em caso de conflito, o documento de maior versão / addendum prevalece sobre o manual base.");
        builder.AppendLine("4. Citar sempre a fonte usando o marcador de página: `(p.12, parte1)`.");
        builder.AppendLine();
    }

    /// <summary>
    /// Topic map built from the level 3 headings, spread across the documents so that every
    /// document is represented. Omitted entirely when there is no level 3 heading to list.
    /// </summary>
    private static void AppendTopicMap(StringBuilder builder, IReadOnlyList<IndexEntry> entries)
    {
        var queues = entries
            .Select(entry => (entry.FileName, Items: new Queue<TocEntry>(entry.TableOfContents.Where(item => item.Level == 3))))
            .Where(candidate => candidate.Items.Count > 0)
            .ToList();

        if (queues.Count == 0)
        {
            return;
        }

        var rows = new List<(string Topic, string Document, string Section, string Page)>();
        while (rows.Count < TopicLimit && queues.Any(queue => queue.Items.Count > 0))
        {
            foreach (var queue in queues)
            {
                if (queue.Items.Count == 0 || rows.Count >= TopicLimit)
                {
                    continue;
                }

                var item = queue.Items.Dequeue();
                rows.Add((StripNumbering(item.Text), queue.FileName, item.Text, item.PageReference));
            }
        }

        builder.AppendLine("## Mapa de tópicos");
        builder.AppendLine();
        builder.AppendLine("| Tópico | Documento | Seção | Página |");
        builder.AppendLine("|---|---|---|---|");

        foreach (var row in rows)
        {
            builder
                .Append("| ").Append(row.Topic)
                .Append(" | ").Append(row.Document)
                .Append(" | ").Append(row.Section)
                .Append(" | ").Append(row.Page)
                .AppendLine(" |");
        }

        builder.AppendLine();
    }

    private static string StripNumbering(string heading)
    {
        var index = 0;
        while (index < heading.Length && (char.IsDigit(heading[index]) || heading[index] == '.'))
        {
            index++;
        }

        var stripped = heading[index..].TrimStart(' ', ')', '-', '–', '—');

        return stripped.Length == 0 ? heading : stripped;
    }
}
