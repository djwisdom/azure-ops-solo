using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Transparent overlay panel that manages a ColumnGuideManager.
/// Maintains compatibility with existing code while providing high-performance drawing.
/// </summary>
public sealed class ColumnGuidePanel : Panel
{
    private RichTextBox? linkedEditor;
    private ColumnGuideManager? _guideManager;
    private int guideColumn = 80;
    private Color guideColor = Color.FromArgb(60, 60, 60);
    private bool showGuide = true;

    public ColumnGuidePanel()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        Enabled = false;
        TabStop = false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Do not erase background — keep fully transparent
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_ERASEBKGND = 0x0014;
        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = IntPtr.Zero; // indicate handled, no erase
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>
    /// The RichTextBox editor to track.
    /// </summary>
    public RichTextBox? LinkedEditor
    {
        get => linkedEditor;
        set
        {
            if (linkedEditor != null && _guideManager != null)
            {
                _guideManager.Dispose();
            }

            linkedEditor = value;

            if (linkedEditor != null)
            {
                _guideManager = new ColumnGuideManager(linkedEditor, this);
                _guideManager.GuideColumns = new[] { guideColumn - 1 }; // manager uses 0-based
                _guideManager.GuideColor = guideColor;
                _guideManager.Style = GuideStyle.Strong; // ~60% opacity to match legacy
                _guideManager.Placement = GuidePlacement.OverText;
                _guideManager.Smooth = true;

                if (showGuide) _guideManager.Attach();
            }

            Invalidate();
        }
    }

    /// <summary>
    /// Temporarily suspend column guide updates during heavy operations.
    /// </summary>
    public void SuspendRequests()
    {
        _guideManager?.SuspendRequests();
    }

    /// <summary>
    /// Resume column guide updates after suspension.
    /// </summary>
    public void ResumeRequests()
    {
        _guideManager?.ResumeRequests();
    }

    /// <summary>
    /// Column number (1-based) to draw the vertical line.
    /// </summary>
    public int GuideColumn
    {
        get => guideColumn;
        set
        {
            if (value < 1) value = 1;
            guideColumn = value;
            if (_guideManager != null)
            {
                _guideManager.SetGuides(value - 1); // manager uses 0-based
            }
            Invalidate();
        }
    }

    /// <summary>
    /// Color of the guide line.
    /// </summary>
    public Color GuideColor
    {
        get => guideColor;
        set { guideColor = value; if (_guideManager != null) _guideManager.GuideColor = value; Invalidate(); }
    }

    /// <summary>
    /// Whether the guide is shown.
    /// </summary>
    public bool ShowGuide
    {
        get => showGuide;
        set
        {
            showGuide = value;
            if (_guideManager != null)
            {
                if (value) _guideManager.Attach(); else _guideManager.Detach();
            }
            Visible = value; // hide panel entirely when guide is off
            Invalidate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _guideManager?.Dispose();
            _guideManager = null;
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ShowGuide && _guideManager != null)
        {
            _guideManager.DrawGuides(e.Graphics);
        }
    }
}
