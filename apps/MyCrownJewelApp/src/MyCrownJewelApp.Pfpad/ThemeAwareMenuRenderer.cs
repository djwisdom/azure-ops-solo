using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class ThemeAwareMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly Theme _theme;

    public ThemeAwareMenuRenderer(Theme theme) : base(new ThemeColorTable(theme))
    {
        _theme = theme.Equals(default) ? throw new ArgumentNullException(nameof(theme)) : theme;
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height - 1);

        if (e.Item.Pressed)
        {
            using (var brush = new SolidBrush(_theme.Highlight))
                g.FillRectangle(brush, rect);
            using (var pen = new Pen(_theme.Accent, 1))
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        }
        else if (e.Item.Selected)
        {
            using (var brush = new SolidBrush(Color.FromArgb(60, _theme.Highlight)))
                g.FillRectangle(brush, rect);
            using (var pen = new Pen(_theme.Accent, 1))
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        }
        else
        {
            e.Item.BackColor = _theme.MenuBackground;
            e.Item.ForeColor = _theme.Text;
        }
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using (var brush = new SolidBrush(_theme.MenuBackground))
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var g = e.Graphics;
        var y = 4;
        var h = e.Item.Height - 8;
        using (var pen = new Pen(_theme.Border, 1))
            g.DrawLine(pen, 25, y + h / 2, e.Item.Width - 4, y + h / 2);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is MenuStrip or StatusStrip) return;
        base.OnRenderToolStripBorder(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = _theme.Text;
        base.OnRenderArrow(e);
    }

    /// <summary>
    /// WinForms ignores <see cref="ToolStripItem.ForeColor"/> for <c>IsLink = true</c> items
    /// and draws text in the system link colour (blue), which is invisible on dark themes.
    /// Override here to force all status-strip labels to use the theme text colour, with
    /// the theme accent colour used on hover for clickable (IsLink) items.
    /// </summary>
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item.Owner is StatusStrip && e.Item is ToolStripStatusLabel lbl)
        {
            bool isHot = lbl.Selected || lbl.Pressed;
            e.TextColor = isHot && lbl.IsLink
                ? _theme.Accent   // hovered clickable → accent colour
                : _theme.Text;    // default: always readable on current background
        }
        base.OnRenderItemText(e);
    }

    /// <summary>
    /// Draws a subtle highlight behind hovered/pressed IsLink status labels
    /// (replaces the invisible default hover state in dark themes).
    /// </summary>
    protected override void OnRenderLabelBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Owner is StatusStrip
            && e.Item is ToolStripStatusLabel { IsLink: true } lbl
            && (lbl.Selected || lbl.Pressed))
        {
            var rect = new Rectangle(1, 1, e.Item.Width - 2, e.Item.Height - 2);
            int alpha = lbl.Pressed ? 70 : 35;
            using var brush = new SolidBrush(Color.FromArgb(alpha, _theme.Highlight));
            e.Graphics.FillRectangle(brush, rect);
            return;
        }
        base.OnRenderLabelBackground(e);
    }

    private sealed class ThemeColorTable : ProfessionalColorTable
    {
        private readonly Theme _theme;

        public ThemeColorTable(Theme theme) => _theme = theme.Equals(default) ? throw new ArgumentNullException(nameof(theme)) : theme;

        public override Color ToolStripDropDownBackground => _theme.MenuBackground;
        public override Color ToolStripBorder => _theme.Border;
        public override Color MenuBorder => _theme.Border;
        public override Color MenuItemBorder => _theme.Accent;
        public override Color MenuItemSelected => Color.FromArgb(60, _theme.Highlight);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(60, _theme.Highlight);
        public override Color MenuItemPressedGradientMiddle => Color.FromArgb(60, _theme.Highlight);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(60, _theme.Highlight);
        public override Color MenuStripGradientBegin => _theme.MenuBackground;
        public override Color MenuStripGradientEnd => _theme.MenuBackground;
        public override Color ImageMarginGradientBegin => _theme.MenuBackground;
        public override Color ImageMarginGradientMiddle => _theme.MenuBackground;
        public override Color ImageMarginGradientEnd => _theme.MenuBackground;
    }
}
