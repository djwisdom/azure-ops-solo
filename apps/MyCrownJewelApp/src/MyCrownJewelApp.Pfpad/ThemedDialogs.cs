using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal static class NativeThemed
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    internal static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    internal static void ApplyDarkModeToWindow(IntPtr hWnd)
    {
        SetWindowTheme(hWnd, "DarkMode_Explorer", null);
        int dark = 1;
        DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
    }

    // CBT hook for file dialog dark mode
    private delegate IntPtr CbtProcDelegate(int nCode, IntPtr wParam, IntPtr lParam);

    private static readonly CbtProcDelegate CbtProc = CbtHookProc;
    private static IntPtr _hook = IntPtr.Zero;
    private static readonly IntPtr _hookHandle = IntPtr.Zero;

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, CbtProcDelegate lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private const int WH_CBT = 5;
    private const int HCBT_ACTIVATE = 5;

    private static IntPtr CbtHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HCBT_ACTIVATE)
        {
            var sb = new StringBuilder(256);
            GetClassNameW(wParam, sb, sb.Capacity);
            if (sb.ToString() == "#32770")
            {
                ApplyDarkModeToWindow(wParam);
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    internal static DialogResult ShowDialogThemed(Func<DialogResult> show)
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        if (!theme.IsLight)
        {
            _hook = SetWindowsHookEx(WH_CBT, CbtProc, IntPtr.Zero, GetCurrentThreadId());
        }
        try { return show(); }
        finally
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}

public static class ThemedFileDialog
{
    public static DialogResult ShowThemed(this OpenFileDialog dlg) =>
        NativeThemed.ShowDialogThemed(() => dlg.ShowDialog());

    public static DialogResult ShowThemed(this SaveFileDialog dlg) =>
        NativeThemed.ShowDialogThemed(() => dlg.ShowDialog());
}

public static class ThemedMessageBox
{
    public static DialogResult Show(string text) =>
        ShowCore(text, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, null);

    public static DialogResult Show(string text, string caption) =>
        ShowCore(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, null);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) =>
        ShowCore(text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, null);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        ShowCore(text, caption, buttons, icon, MessageBoxDefaultButton.Button1, null);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton) =>
        ShowCore(text, caption, buttons, icon, defaultButton, null);

    public static DialogResult Show(IWin32Window? owner, string text) =>
        ShowCore(text, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, owner);

    public static DialogResult Show(IWin32Window? owner, string text, string caption) =>
        ShowCore(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, owner);

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons) =>
        ShowCore(text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, owner);

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        ShowCore(text, caption, buttons, icon, MessageBoxDefaultButton.Button1, owner);

    private static DialogResult ShowCore(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, IWin32Window? owner)
    {
        using var form = new ThemedMessageBoxForm(text, caption, buttons, icon, defaultButton);
        DialogResult result;
        if (owner != null)
            result = form.ShowDialog(owner);
        else
            result = form.ShowDialog();
        return result;
    }
}

internal sealed class ThemedMessageBoxForm : Form
{
    private DialogResult _result = DialogResult.None;
    private readonly List<Button> _buttons = new();

    public ThemedMessageBoxForm(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;
        Text = caption;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ShowIcon = false;

        int padding = 16;
        int iconTextGap = 10;
        Font msgFont = new Font("Segoe UI", 10, FontStyle.Regular);

        string? iconSymbol = icon switch
        {
            MessageBoxIcon.Error or MessageBoxIcon.Hand or MessageBoxIcon.Stop => "\u26D4",
            MessageBoxIcon.Question => "\u2753",
            MessageBoxIcon.Warning or MessageBoxIcon.Exclamation => "\u26A0",
            MessageBoxIcon.Asterisk or MessageBoxIcon.Information => "\u2139",
            _ => null
        };

        Color iconColor = icon switch
        {
            MessageBoxIcon.Error or MessageBoxIcon.Hand or MessageBoxIcon.Stop => Color.FromArgb(228, 59, 68),
            MessageBoxIcon.Question => theme.Accent,
            MessageBoxIcon.Warning or MessageBoxIcon.Exclamation => Color.FromArgb(215, 175, 0),
            MessageBoxIcon.Asterisk or MessageBoxIcon.Information => theme.Accent,
            _ => theme.Muted
        };

        bool hasIcon = iconSymbol != null;

        int iconWidth = 0;
        Label? iconLabel = null;
        if (hasIcon)
        {
            iconLabel = new Label
            {
                Text = iconSymbol,
                Font = new Font("Segoe UI", 24, FontStyle.Regular),
                ForeColor = iconColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter
            };
            iconWidth = iconLabel.PreferredWidth;
        }

        int iconOffset = hasIcon ? padding + iconWidth + iconTextGap : padding;

        Size textSize = TextRenderer.MeasureText(text, msgFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.WordBreak);
        int maxTextWidth = Math.Min(textSize.Width, 600);
        int desiredFormWidth = maxTextWidth + iconOffset + padding;
        string[] btnLabels = GetButtonLabels(buttons);
        int btnW = 85;
        int totalBtnWidth = btnLabels.Length * btnW + (btnLabels.Length - 1) * 6 + padding * 2;
        int formW = Math.Max(desiredFormWidth, Math.Max(totalBtnWidth, 280));
        formW = Math.Min(formW, 700);

        int textAreaWidth = formW - iconOffset - padding;
        Size wrappedTextSize = TextRenderer.MeasureText(text, msgFont, new Size(textAreaWidth, int.MaxValue), TextFormatFlags.WordBreak);

        int currentY = padding;

        if (hasIcon && iconLabel != null)
        {
            iconLabel.Location = new Point(padding, currentY + 2);
            Controls.Add(iconLabel);

            var msgLabel = new Label
            {
                Text = text,
                Font = msgFont,
                ForeColor = theme.Text,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(textAreaWidth, wrappedTextSize.Height),
                Location = new Point(padding + iconWidth + iconTextGap, currentY)
            };
            Controls.Add(msgLabel);
            currentY = Math.Max(iconLabel.Bottom, msgLabel.Bottom) + padding;
        }
        else
        {
            var msgLabel = new Label
            {
                Text = text,
                Font = msgFont,
                ForeColor = theme.Text,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(textAreaWidth, wrappedTextSize.Height),
                Location = new Point(padding, currentY)
            };
            Controls.Add(msgLabel);
            currentY = msgLabel.Bottom + padding;
        }

        int btnH = 28;
        int btnStartX = formW - padding - btnLabels.Length * btnW - (btnLabels.Length - 1) * 6;
        if (btnStartX < padding) btnStartX = padding;

        for (int i = 0; i < btnLabels.Length; i++)
        {
            int idx = i;
            var btn = new Button
            {
                Text = btnLabels[i],
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                FlatStyle = FlatStyle.Flat,
                BackColor = theme.PanelBackground,
                ForeColor = theme.Text,
                Size = new Size(btnW, btnH),
                Location = new Point(btnStartX + i * (btnW + 6), currentY),
                Cursor = Cursors.Hand,
                TabIndex = i
            };
            btn.FlatAppearance.BorderColor = theme.Border;
            btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
            btn.Click += (s, e) =>
            {
                _result = GetResultForButton(buttons, idx);
                DialogResult = _result;
                Close();
            };
            _buttons.Add(btn);
            Controls.Add(btn);
        }

        int defaultIndex = defaultButton switch
        {
            MessageBoxDefaultButton.Button2 => 1,
            MessageBoxDefaultButton.Button3 => 2,
            _ => 0
        };
        if (defaultIndex < _buttons.Count)
        {
            AcceptButton = _buttons[defaultIndex];
            _buttons[defaultIndex].Select();
        }

        currentY += btnH + padding;
        ClientSize = new Size(formW, currentY);

        Paint += (s, e) =>
        {
            using var p = new Pen(theme.Border, 1);
            e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        };
    }

    private static string[] GetButtonLabels(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.OK => new[] { "OK" },
        MessageBoxButtons.OKCancel => new[] { "OK", "Cancel" },
        MessageBoxButtons.YesNo => new[] { "Yes", "No" },
        MessageBoxButtons.YesNoCancel => new[] { "Yes", "No", "Cancel" },
        MessageBoxButtons.RetryCancel => new[] { "Retry", "Cancel" },
        MessageBoxButtons.AbortRetryIgnore => new[] { "Abort", "Retry", "Ignore" },
        _ => new[] { "OK" }
    };

    private static DialogResult GetResultForButton(MessageBoxButtons buttons, int index) => buttons switch
    {
        MessageBoxButtons.OK => DialogResult.OK,
        MessageBoxButtons.OKCancel => index == 0 ? DialogResult.OK : DialogResult.Cancel,
        MessageBoxButtons.YesNo => index == 0 ? DialogResult.Yes : DialogResult.No,
        MessageBoxButtons.YesNoCancel => index switch { 0 => DialogResult.Yes, 1 => DialogResult.No, _ => DialogResult.Cancel },
        MessageBoxButtons.RetryCancel => index == 0 ? DialogResult.Retry : DialogResult.Cancel,
        MessageBoxButtons.AbortRetryIgnore => index switch { 0 => DialogResult.Abort, 1 => DialogResult.Retry, _ => DialogResult.Ignore },
        _ => DialogResult.OK
    };
}
