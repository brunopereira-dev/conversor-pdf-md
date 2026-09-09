using PdfToMarkdown.Application.Abstractions;
using PdfToMarkdown.Application.Documents;

namespace PdfToMarkdown.Presentation;

/// <summary>Main window: the list of logical documents, the output folder and the run controls.</summary>
public sealed class MainForm : Form
{
    private readonly List<DocumentPanel> documents = [];
    private readonly FlowLayoutPanel documentsArea = new();
    private readonly TextBox outputFolderBox = new();
    private readonly Button selectFolderButton = new();
    private readonly Button addDocumentButton = new();
    private readonly Button convertButton = new();
    private readonly Button cancelButton = new();
    private readonly ProgressBar progressBar = new();
    private readonly Label statusLabel = new();

    private IConvertDocumentsUseCase? converter;
    private ISuggestMetadataUseCase? suggester;
    private IDocumentInputValidator? inputValidator;
    private IOutputFileNameFactory? fileNames;
    private CancellationTokenSource? cancellation;
    private bool refreshing;

    public MainForm()
    {
        BuildLayout();
        AddDocument();
    }

    /// <summary>Receives the abstractions of the Application layer from the composition root.</summary>
    public void Initialize(
        IConvertDocumentsUseCase convertDocuments,
        ISuggestMetadataUseCase suggestMetadata,
        IDocumentInputValidator validator,
        IOutputFileNameFactory outputFileNames)
    {
        converter = convertDocuments;
        suggester = suggestMetadata;
        inputValidator = validator;
        fileNames = outputFileNames;
    }

    private void AddDocument()
    {
        var panel = new DocumentPanel
        {
            IsFileAlreadyUsed = IsFileAlreadyUsed,
            OutputNameFromTitle = title => fileNames?.FromTitle(title) ?? string.Empty,
            RequestSuggestion = SuggestAsync
        };

        panel.Changed += (_, _) => RefreshState();
        panel.MoveUpRequested += (sender, _) => MoveDocument(sender, -1);
        panel.MoveDownRequested += (sender, _) => MoveDocument(sender, 1);
        panel.RemoveRequested += (sender, _) => RemoveDocument(sender);

        documents.Add(panel);
        RebuildDocumentArea();
    }

    private void MoveDocument(object? sender, int offset)
    {
        if (sender is not DocumentPanel panel)
        {
            return;
        }

        var index = documents.IndexOf(panel);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= documents.Count)
        {
            return;
        }

        (documents[index], documents[target]) = (documents[target], documents[index]);
        RebuildDocumentArea();
    }

    private void RemoveDocument(object? sender)
    {
        if (sender is not DocumentPanel panel)
        {
            return;
        }

        if (documents.Count == 1)
        {
            MessageBox.Show(
                this,
                "A execução precisa de pelo menos um documento.",
                "Remover documento",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        documents.Remove(panel);
        documentsArea.Controls.Remove(panel);
        panel.Dispose();
        RebuildDocumentArea();
    }

    private void RebuildDocumentArea()
    {
        documentsArea.SuspendLayout();
        documentsArea.Controls.Clear();

        foreach (var panel in documents)
        {
            documentsArea.Controls.Add(panel);
        }

        documentsArea.ResumeLayout();
        RefreshState();
    }

    private bool IsFileAlreadyUsed(string filePath, DocumentPanel owner) =>
        documents.Any(panel => panel.Files.Contains(filePath, StringComparer.OrdinalIgnoreCase));

    private async Task<(string Title, string Version)> SuggestAsync(string filePath, CancellationToken cancellationToken)
    {
        if (suggester is null)
        {
            return (string.Empty, string.Empty);
        }

        statusLabel.Text = $"Lendo metadados de {Path.GetFileName(filePath)}...";
        var suggestion = await suggester.ExecuteAsync(filePath, cancellationToken).ConfigureAwait(true);
        statusLabel.Text = "Pronto.";

        return (suggestion.Title, suggestion.Version);
    }

    /// <summary>Keeps positions, dependency options and the state of the Converter button in sync.</summary>
    private void RefreshState()
    {
        if (refreshing)
        {
            return;
        }

        refreshing = true;

        try
        {
            var width = documentsArea.ClientSize.Width - 24;

            for (var index = 0; index < documents.Count; index++)
            {
                var panel = documents[index];
                panel.Width = Math.Max(width, 480);
                panel.SetPosition(index + 1, index > 0, index < documents.Count - 1);
                panel.SetDependencyOptions(documents
                    .Where(other => !ReferenceEquals(other, panel))
                    .Select(other => other.OutputFileName)
                    .Where(name => name.Length > 0));
            }

            var running = cancellation is not null;
            var ready = documents.Count > 0
                && documents.All(panel => panel.Files.Count > 0)
                && outputFolderBox.Text.Trim().Length > 0;

            convertButton.Enabled = ready && !running;
            addDocumentButton.Enabled = !running;
            selectFolderButton.Enabled = !running;
            cancelButton.Enabled = running;
        }
        finally
        {
            refreshing = false;
        }
    }

    private void SelectOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Pasta de saída dos arquivos .md e do INDEX.md",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            outputFolderBox.Text = dialog.SelectedPath;
            RefreshState();
        }
    }

    private ConversionRequest BuildRequest()
    {
        var inputs = documents
            .Select(panel => new DocumentInput(
                panel.OutputFileName,
                panel.Title,
                panel.TitleConfirmed,
                panel.Version,
                panel.VersionConfirmed,
                panel.SourceUrl,
                panel.Kind,
                panel.Scope,
                panel.DependsOn,
                panel.Files.ToList()))
            .ToList();

        var selected = documents.SelectMany(panel => panel.Files).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return new ConversionRequest(outputFolderBox.Text.Trim(), inputs, selected);
    }

    private async void ConvertAsync(object? sender, EventArgs args)
    {
        if (converter is null || inputValidator is null)
        {
            return;
        }

        try
        {
            var request = BuildRequest();
            var errors = inputValidator.Validate(request);

            if (errors.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, errors),
                    "Dados inválidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            cancellation = new CancellationTokenSource();
            RefreshState();

            progressBar.Value = 0;
            progressBar.Maximum = Math.Max(request.Documents.Sum(document => document.PartFilePaths.Count) + 1, 1);
            statusLabel.Text = "Convertendo...";

            var progress = new Progress<ConversionProgress>(ReportProgress);
            var outcome = await converter.ExecuteAsync(request, progress, cancellation.Token).ConfigureAwait(true);

            statusLabel.Text = "Conversão concluída.";
            progressBar.Value = progressBar.Maximum;

            using var report = new ReportForm();
            report.ShowReport(outcome.Report, outcome.OutputDirectory);
            report.ShowDialog(this);
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "Conversão cancelada.";
        }
        catch (Exception exception)
        {
            statusLabel.Text = "Falha na conversão.";
            MessageBox.Show(this, exception.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            cancellation?.Dispose();
            cancellation = null;
            RefreshState();
        }
    }

    private void ReportProgress(ConversionProgress progress)
    {
        progressBar.Maximum = Math.Max(progress.Total, 1);
        progressBar.Value = Math.Clamp(progress.Completed, 0, progressBar.Maximum);
        statusLabel.Text = progress.Step;
    }

    private void BuildLayout()
    {
        Text = "Conversor PDF → Markdown";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 640);
        Size = new Size(1040, 780);

        var top = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 8, 10, 8) };
        var folderLabel = new Label
        {
            Text = "Pasta de saída:",
            Dock = DockStyle.Left,
            Width = 100,
            TextAlign = ContentAlignment.MiddleLeft
        };

        selectFolderButton.Text = "Selecionar...";
        selectFolderButton.Dock = DockStyle.Right;
        selectFolderButton.Width = 120;
        selectFolderButton.Click += (_, _) => SelectOutputFolder();

        outputFolderBox.Dock = DockStyle.Fill;
        outputFolderBox.ReadOnly = true;
        outputFolderBox.PlaceholderText = "Obrigatório: onde os .md e o INDEX.md serão gravados";

        top.Controls.Add(outputFolderBox);
        top.Controls.Add(folderLabel);
        top.Controls.Add(selectFolderButton);

        documentsArea.Dock = DockStyle.Fill;
        documentsArea.AutoScroll = true;
        documentsArea.FlowDirection = FlowDirection.TopDown;
        documentsArea.WrapContents = false;
        documentsArea.Padding = new Padding(10);
        documentsArea.Resize += (_, _) => RefreshState();

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 88, Padding = new Padding(10, 6, 10, 10) };

        addDocumentButton.Text = "Adicionar documento";
        addDocumentButton.Width = 180;
        addDocumentButton.Height = 30;
        addDocumentButton.Location = new Point(10, 8);
        addDocumentButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        addDocumentButton.Click += (_, _) => AddDocument();

        convertButton.Text = "Converter";
        convertButton.Width = 140;
        convertButton.Height = 30;
        convertButton.Enabled = false;
        convertButton.Click += ConvertAsync;

        cancelButton.Text = "Cancelar";
        cancelButton.Width = 120;
        cancelButton.Height = 30;
        cancelButton.Enabled = false;
        cancelButton.Click += (_, _) => cancellation?.Cancel();

        progressBar.Height = 18;
        progressBar.Left = 10;

        statusLabel.Text = "Pronto.";
        statusLabel.AutoSize = false;
        statusLabel.Height = 20;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        bottom.Controls.Add(addDocumentButton);
        bottom.Controls.Add(convertButton);
        bottom.Controls.Add(cancelButton);
        bottom.Controls.Add(progressBar);
        bottom.Controls.Add(statusLabel);
        bottom.Resize += (_, _) => LayoutBottom(bottom);

        Controls.Add(documentsArea);
        Controls.Add(top);
        Controls.Add(bottom);

        Load += (_, _) => LayoutBottom(bottom);
    }

    private void LayoutBottom(Panel bottom)
    {
        var right = bottom.ClientSize.Width - bottom.Padding.Right;

        convertButton.Location = new Point(right - convertButton.Width, 8);
        cancelButton.Location = new Point(convertButton.Left - cancelButton.Width - 8, 8);

        progressBar.Location = new Point(bottom.Padding.Left, 46);
        progressBar.Width = Math.Max(bottom.ClientSize.Width - bottom.Padding.Horizontal, 100);

        statusLabel.Location = new Point(bottom.Padding.Left, 68);
        statusLabel.Width = progressBar.Width;
    }
}
