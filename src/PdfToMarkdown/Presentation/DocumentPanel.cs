using System.ComponentModel;

namespace PdfToMarkdown.Presentation;

/// <summary>A logical document on the form: its metadata and its ordered list of PDF files.</summary>
public sealed class DocumentPanel : UserControl
{
    public const string NoDependency = "nenhum";

    private const int HeaderHeight = 30;
    private const int MetadataHeight = 196;
    private const int FilesHeaderHeight = 22;

    private readonly List<string> files = [];
    private readonly ToolTip toolTip = new();
    private readonly Button upButton = new();
    private readonly Button downButton = new();
    private readonly Button removeButton = new();
    private readonly TableLayoutPanel root = new();
    private readonly Panel headerPanel = new();
    private readonly Label headerLabel = new();
    private readonly TableLayoutPanel metadataTable = new();
    private readonly TableLayoutPanel filesTable = new();
    private readonly TextBox outputBox = new();
    private readonly SuggestionField titleField = new();
    private readonly SuggestionField versionField = new();
    private readonly TextBox kindBox = new();
    private readonly TextBox scopeBox = new();
    private readonly TextBox urlBox = new();
    private readonly ComboBox dependsBox = new();
    private bool outputEditedByUser;
    private bool updatingOutput;

    public DocumentPanel()
    {
        BuildLayout();
        RefreshRows();
    }

    /// <summary>Raised whenever the content of the document changes in a way the form must react to.</summary>
    public event EventHandler? Changed;

    public event EventHandler? MoveUpRequested;

    public event EventHandler? MoveDownRequested;

    public event EventHandler? RemoveRequested;

    /// <summary>Tells whether a file is already used by this or any other document of the run.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, DocumentPanel, bool>? IsFileAlreadyUsed { get; set; }

    /// <summary>Derives the output file name from the title, using the rule of the Application layer.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string?, string>? OutputNameFromTitle { get; set; }

    /// <summary>Asks the Application layer for the title and version suggested by the first PDF.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, CancellationToken, Task<(string Title, string Version)>>? RequestSuggestion { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Files => files;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string OutputFileName => outputBox.Text.Trim();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title => titleField.Value;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool TitleConfirmed => titleField.Confirmed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Version => versionField.Value;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool VersionConfirmed => versionField.Confirmed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Kind => kindBox.Text.Trim();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Scope => scopeBox.Text.Trim();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SourceUrl => urlBox.Text.Trim();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? DependsOn =>
        dependsBox.SelectedIndex <= 0 ? null : dependsBox.SelectedItem?.ToString();

    public void SetPosition(int position, bool canMoveUp, bool canMoveDown)
    {
        headerLabel.Text = $"Documento {position}";
        upButton.Enabled = canMoveUp;
        downButton.Enabled = canMoveDown;
    }

    /// <summary>Refreshes the options of "depende de" with the other documents of the run.</summary>
    public void SetDependencyOptions(IEnumerable<string> otherDocuments)
    {
        var current = DependsOn;

        dependsBox.BeginUpdate();
        dependsBox.Items.Clear();
        dependsBox.Items.Add(NoDependency);
        foreach (var name in otherDocuments)
        {
            dependsBox.Items.Add(name);
        }

        var index = current is null ? 0 : dependsBox.Items.IndexOf(current);
        dependsBox.SelectedIndex = index < 0 ? 0 : index;
        dependsBox.EndUpdate();
    }

    /// <summary>Rebuilds the file lines: one line per file plus a single empty button at the end.</summary>
    private void RefreshRows()
    {
        filesTable.SuspendLayout();
        foreach (Control control in filesTable.Controls)
        {
            control.Dispose();
        }

        filesTable.Controls.Clear();
        filesTable.RowStyles.Clear();
        filesTable.RowCount = files.Count + 1;

        for (var index = 0; index < files.Count; index++)
        {
            var position = index;
            var row = new FileRow { Dock = DockStyle.Fill, Margin = new Padding(0) };
            row.ShowFile(files[position], position > 0, position < files.Count - 1);
            row.ChooseRequested += (_, _) => ChooseFile(position);
            row.MoveUpRequested += (_, _) => MoveFile(position, position - 1);
            row.MoveDownRequested += (_, _) => MoveFile(position, position + 1);
            row.RemoveRequested += (_, _) => Remove(position);

            filesTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            filesTable.Controls.Add(row, 0, position);
        }

        var empty = new FileRow { Dock = DockStyle.Fill, Margin = new Padding(0) };
        empty.ShowFile(null, false, false);
        empty.ChooseRequested += (_, _) => ChooseFile(files.Count);
        filesTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        filesTable.Controls.Add(empty, 0, files.Count);

        filesTable.ResumeLayout();
        UpdateHeight();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ChooseFile(int position)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Selecionar arquivo PDF",
            Filter = "Arquivos PDF (*.pdf)|*.pdf",
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var chosen = dialog.FileName;
        var replacing = position < files.Count ? files[position] : null;

        if (!string.Equals(chosen, replacing, StringComparison.OrdinalIgnoreCase)
            && IsFileAlreadyUsed?.Invoke(chosen, this) == true)
        {
            MessageBox.Show(
                this,
                $"O arquivo \"{Path.GetFileName(chosen)}\" já foi selecionado em algum documento desta execução.",
                "Arquivo repetido",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        var first = files.Count == 0;
        if (position < files.Count)
        {
            files[position] = chosen;
        }
        else
        {
            files.Add(chosen);
        }

        RefreshRows();

        if (first)
        {
            SuggestFromFirstFile(chosen);
        }
    }

    private void MoveFile(int from, int to)
    {
        if (to < 0 || to >= files.Count)
        {
            return;
        }

        (files[from], files[to]) = (files[to], files[from]);
        RefreshRows();
    }

    private void Remove(int position)
    {
        files.RemoveAt(position);
        RefreshRows();
    }

    private async void SuggestFromFirstFile(string filePath)
    {
        try
        {
            if (RequestSuggestion is null)
            {
                return;
            }

            var (title, version) = await RequestSuggestion(filePath, CancellationToken.None).ConfigureAwait(true);

            if (titleField.Value.Length == 0)
            {
                titleField.Suggest(title);
            }

            if (versionField.Value.Length == 0)
            {
                versionField.Suggest(version);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"Não foi possível sugerir metadados a partir de \"{Path.GetFileName(filePath)}\": {exception.Message}",
                "Sugestão de metadados",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void UpdateHeight()
    {
        Height = HeaderHeight + MetadataHeight + FilesHeaderHeight + ((files.Count + 1) * 30) + 12;
    }

    private void BuildLayout()
    {
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(6);

        BuildHeader();
        BuildMetadata();

        filesTable.ColumnCount = 1;
        filesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filesTable.Dock = DockStyle.Fill;
        filesTable.Margin = new Padding(0);

        var filesLabel = new Label
        {
            Text = "Arquivos (de cima para baixo, na ordem de concatenação):",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        root.Dock = DockStyle.Fill;
        root.ColumnCount = 1;
        root.RowCount = 4;
        root.Margin = new Padding(0);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, MetadataHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, FilesHeaderHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(headerPanel, 0, 0);
        root.Controls.Add(metadataTable, 0, 1);
        root.Controls.Add(filesLabel, 0, 2);
        root.Controls.Add(filesTable, 0, 3);

        Controls.Add(root);
    }

    private void BuildHeader()
    {
        headerLabel.Text = "Documento";
        headerLabel.Dock = DockStyle.Left;
        headerLabel.AutoSize = false;
        headerLabel.Width = 200;
        headerLabel.TextAlign = ContentAlignment.MiddleLeft;
        headerLabel.Font = new Font(Font, FontStyle.Bold);

        ConfigureHeaderButton(upButton, "↑", "Mover o documento para cima", (_, _) => MoveUpRequested?.Invoke(this, EventArgs.Empty));
        ConfigureHeaderButton(downButton, "↓", "Mover o documento para baixo", (_, _) => MoveDownRequested?.Invoke(this, EventArgs.Empty));
        ConfigureHeaderButton(removeButton, "✕", "Remover o documento", (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty));

        headerPanel.Dock = DockStyle.Fill;
        headerPanel.Margin = new Padding(0);
        headerPanel.Controls.Add(headerLabel);
        headerPanel.Controls.Add(upButton);
        headerPanel.Controls.Add(downButton);
        headerPanel.Controls.Add(removeButton);
    }

    private void ConfigureHeaderButton(Button button, string text, string hint, EventHandler handler)
    {
        button.Text = text;
        button.Dock = DockStyle.Right;
        button.Width = 32;
        button.Margin = new Padding(1);
        button.Click += handler;
        toolTip.SetToolTip(button, hint);
    }

    private void BuildMetadata()
    {
        metadataTable.ColumnCount = 2;
        metadataTable.RowCount = 7;
        metadataTable.Dock = DockStyle.Fill;
        metadataTable.Margin = new Padding(0);
        metadataTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        metadataTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        dependsBox.DropDownStyle = ComboBoxStyle.DropDownList;
        dependsBox.Items.Add(NoDependency);
        dependsBox.SelectedIndex = 0;

        AddMetadataRow(0, "Saída (.md)", outputBox);
        AddMetadataRow(1, "Título", titleField);
        AddMetadataRow(2, "Versão", versionField);
        AddMetadataRow(3, "Tipo", kindBox);
        AddMetadataRow(4, "Escopo", scopeBox);
        AddMetadataRow(5, "Fonte (URL)", urlBox);
        AddMetadataRow(6, "Depende de", dependsBox);

        outputBox.TextChanged += (_, _) =>
        {
            if (!updatingOutput)
            {
                outputEditedByUser = true;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        };

        titleField.ValueChanged += (_, _) =>
        {
            ApplyDerivedOutputName();
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    private void AddMetadataRow(int row, string label, Control field)
    {
        metadataTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        metadataTable.Controls.Add(
            new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft },
            0,
            row);

        field.Dock = DockStyle.Fill;
        field.Margin = new Padding(0, 2, 4, 2);
        metadataTable.Controls.Add(field, 1, row);
    }

    /// <summary>Keeps the output name derived from the title while the user has not typed one.</summary>
    private void ApplyDerivedOutputName()
    {
        if (outputEditedByUser || OutputNameFromTitle is null)
        {
            return;
        }

        updatingOutput = true;
        outputBox.Text = OutputNameFromTitle(titleField.Value);
        updatingOutput = false;
    }
}
