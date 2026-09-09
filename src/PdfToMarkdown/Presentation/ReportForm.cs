namespace PdfToMarkdown.Presentation;

/// <summary>Shows the end of run report produced by the Application layer.</summary>
public sealed class ReportForm : Form
{
    private readonly TextBox reportBox = new();
    private readonly Button copyButton = new();
    private readonly Button openFolderButton = new();
    private readonly Button closeButton = new();
    private string outputDirectory = string.Empty;

    public ReportForm()
    {
        BuildLayout();
    }

    public void ShowReport(string report, string directory)
    {
        outputDirectory = directory;
        reportBox.Text = report.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
        reportBox.SelectionStart = 0;
        reportBox.SelectionLength = 0;
    }

    private void BuildLayout()
    {
        Text = "Relatório da conversão";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        Size = new Size(900, 640);

        reportBox.Multiline = true;
        reportBox.ReadOnly = true;
        reportBox.ScrollBars = ScrollBars.Both;
        reportBox.WordWrap = false;
        reportBox.Dock = DockStyle.Fill;
        reportBox.Font = new Font(FontFamily.GenericMonospace, 9f);
        reportBox.BackColor = Color.White;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8, 6, 8, 6)
        };

        closeButton.Text = "Fechar";
        closeButton.Width = 110;
        closeButton.Click += (_, _) => Close();

        openFolderButton.Text = "Abrir pasta";
        openFolderButton.Width = 110;
        openFolderButton.Click += (_, _) => OpenFolder();

        copyButton.Text = "Copiar";
        copyButton.Width = 110;
        copyButton.Click += (_, _) => Clipboard.SetText(reportBox.Text);

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(openFolderButton);
        buttons.Controls.Add(copyButton);

        Controls.Add(reportBox);
        Controls.Add(buttons);
        AcceptButton = closeButton;
    }

    private void OpenFolder()
    {
        if (!Directory.Exists(outputDirectory))
        {
            return;
        }

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo(outputDirectory) { UseShellExecute = true }
        };

        process.Start();
    }
}
