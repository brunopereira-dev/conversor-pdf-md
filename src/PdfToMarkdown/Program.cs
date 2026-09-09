using PdfToMarkdown.Application.Documents;
using PdfToMarkdown.Application.Markdown;
using PdfToMarkdown.Application.UseCases;
using PdfToMarkdown.Infrastructure.Pdf;
using PdfToMarkdown.Infrastructure.Storage;
using PdfToMarkdown.Presentation;

namespace PdfToMarkdown;

/// <summary>Composition root: the only place that knows the Infrastructure implementations.</summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var clock = TimeProvider.System;
        var reader = new PdfPigContentReader();
        var store = new FileSystemOutputStore();
        var fileNames = new OutputFileNameFactory();
        var inputValidator = new DocumentInputValidator();
        var outputValidator = new OutputValidator(store);

        var convert = new ConvertDocumentsUseCase(
            reader,
            store,
            outputValidator,
            inputValidator,
            fileNames,
            new MarkdownComposer(clock),
            new IndexComposer(clock),
            new ReportComposer());

        var suggest = new SuggestMetadataUseCase(reader);

        var form = new MainForm();
        form.Initialize(convert, suggest, inputValidator, fileNames);

        System.Windows.Forms.Application.Run(form);
    }
}
