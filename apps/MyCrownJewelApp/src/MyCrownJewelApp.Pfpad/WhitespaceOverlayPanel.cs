using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Stub retained for Designer.cs field compatibility only.
/// Whitespace glyph rendering is now performed directly inside
/// <see cref="HighlightRichTextBox"/> via its <c>ShowWhitespace</c> property,
/// which draws crisp geometric glyphs (dots and arrows) in the WM_PAINT pass.
/// </summary>
public sealed class WhitespaceOverlayForm : Form
{
    public WhitespaceOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        Visible         = false;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Form? OwnerForm { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RichTextBox? LinkedEditor { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color GlyphColor { get; set; } = Color.Gray;

    /// <summary>No-op: rendering is handled by HighlightRichTextBox.ShowWhitespace.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowGlyphs
    {
        get => false;
        set { /* delegated to HighlightRichTextBox.ShowWhitespace */ }
    }
}

