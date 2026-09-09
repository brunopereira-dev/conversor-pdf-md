using System.ComponentModel;

namespace PdfToMarkdown.Presentation;

/// <summary>
/// A text field that can hold a value suggested from the PDF. While the suggestion is not
/// confirmed or edited it stays visually marked, and the value is not treated as informed.
/// </summary>
public sealed class SuggestionField : UserControl
{
    private static readonly Color SuggestionColor = Color.FromArgb(120, 120, 120);

    private readonly TextBox textBox = new();
    private readonly Button confirmButton = new();
    private readonly ToolTip toolTip = new();
    private Font? suggestionFont;
    private Font? confirmedFont;
    private bool updating;

    public SuggestionField()
    {
        BuildLayout();
    }

    public event EventHandler? ValueChanged;

    /// <summary>True when the user typed the value or explicitly confirmed the suggestion.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Confirmed { get; private set; } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Value
    {
        get => textBox.Text.Trim();
        set
        {
            updating = true;
            textBox.Text = value;
            updating = false;
            Confirmed = true;
            ApplyStyle();
        }
    }

    /// <summary>Fills the field with a value suggested from the PDF, still pending confirmation.</summary>
    public void Suggest(string value)
    {
        updating = true;
        textBox.Text = value;
        updating = false;
        Confirmed = value.Length == 0;
        ApplyStyle();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Confirm()
    {
        Confirmed = true;
        ApplyStyle();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuildLayout()
    {
        Height = 26;
        Margin = new Padding(0, 1, 0, 1);

        confirmedFont = new Font(Font, FontStyle.Regular);
        suggestionFont = new Font(Font, FontStyle.Italic);

        confirmButton.Text = "✓";
        confirmButton.Dock = DockStyle.Right;
        confirmButton.Width = 30;
        confirmButton.Visible = false;
        confirmButton.Click += (_, _) => Confirm();
        toolTip.SetToolTip(confirmButton, "Confirmar a sugestão");

        textBox.Dock = DockStyle.Fill;
        textBox.TextChanged += (_, _) =>
        {
            if (updating)
            {
                return;
            }

            Confirmed = true;
            ApplyStyle();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };

        Controls.Add(textBox);
        Controls.Add(confirmButton);
        ApplyStyle();
    }

    private void ApplyStyle()
    {
        textBox.ForeColor = Confirmed ? SystemColors.WindowText : SuggestionColor;
        textBox.Font = (Confirmed ? confirmedFont : suggestionFont) ?? Font;
        confirmButton.Visible = !Confirmed;
        toolTip.SetToolTip(
            textBox,
            Confirmed ? string.Empty : "Sugestão extraída do PDF — confirme ou edite para usar o valor.");
    }
}
