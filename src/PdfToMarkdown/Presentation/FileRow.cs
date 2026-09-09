using System.ComponentModel;

namespace PdfToMarkdown.Presentation;

/// <summary>One line of the file list of a document: the button, the chosen file and its controls.</summary>
public sealed class FileRow : UserControl
{
    private readonly ToolTip toolTip = new();
    private readonly TableLayoutPanel layout = new();
    private readonly Button chooseButton = new();
    private readonly Label nameLabel = new();
    private readonly Button upButton = new();
    private readonly Button downButton = new();
    private readonly Button removeButton = new();

    public FileRow()
    {
        BuildLayout();
    }

    public event EventHandler? ChooseRequested;

    public event EventHandler? MoveUpRequested;

    public event EventHandler? MoveDownRequested;

    public event EventHandler? RemoveRequested;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? FilePath { get; private set; }

    public void ShowFile(string? filePath, bool canMoveUp, bool canMoveDown)
    {
        FilePath = filePath;

        var filled = !string.IsNullOrEmpty(filePath);
        nameLabel.Text = filled ? Path.GetFileName(filePath) : string.Empty;
        toolTip.SetToolTip(nameLabel, filePath ?? string.Empty);

        upButton.Visible = filled;
        downButton.Visible = filled;
        removeButton.Visible = filled;
        upButton.Enabled = canMoveUp;
        downButton.Enabled = canMoveDown;
    }

    private void BuildLayout()
    {
        Height = 30;
        Margin = new Padding(0);
        Dock = DockStyle.Top;

        chooseButton.Text = "Adicionar arquivo";
        chooseButton.Dock = DockStyle.Fill;
        chooseButton.Margin = new Padding(0, 1, 4, 1);
        chooseButton.Click += (_, _) => ChooseRequested?.Invoke(this, EventArgs.Empty);

        nameLabel.Dock = DockStyle.Fill;
        nameLabel.TextAlign = ContentAlignment.MiddleLeft;
        nameLabel.AutoEllipsis = true;

        ConfigureControl(upButton, "↑", "Mover uma posição acima", (_, _) => MoveUpRequested?.Invoke(this, EventArgs.Empty));
        ConfigureControl(downButton, "↓", "Mover uma posição abaixo", (_, _) => MoveDownRequested?.Invoke(this, EventArgs.Empty));
        ConfigureControl(removeButton, "✕", "Remover a linha", (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty));

        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 5;
        layout.RowCount = 1;
        layout.Margin = new Padding(0);
        layout.Padding = new Padding(0);
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));

        layout.Controls.Add(chooseButton, 0, 0);
        layout.Controls.Add(nameLabel, 1, 0);
        layout.Controls.Add(upButton, 2, 0);
        layout.Controls.Add(downButton, 3, 0);
        layout.Controls.Add(removeButton, 4, 0);

        Controls.Add(layout);
    }

    private void ConfigureControl(Button button, string text, string hint, EventHandler handler)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(1);
        button.Visible = false;
        button.Click += handler;
        toolTip.SetToolTip(button, hint);
    }
}
