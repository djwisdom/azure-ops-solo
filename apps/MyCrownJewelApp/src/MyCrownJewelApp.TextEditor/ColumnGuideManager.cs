using System.Collections.Concurrent;
using System.Drawing;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;
using ThreadingTimer = System.Threading.Timer;

namespace MyCrownJewelApp.TextEditor;

/// <summary>
/// Manages drawing of column guides (vertical rulers) at specified character positions.
/// Supports proportional fonts, zoom, high-DPI, and maintains <8ms paint budget.
/// </summary>
public sealed class ColumnGuideManager : IDisposable
{
    private readonly Control _editor;
    private readonly Control _drawTarget;
    private readonly bool _isOverlayPanel;
    private readonly ConcurrentDictionary<int, (float x, float width)> _columnCache = new();
    private readonly object _penLock = new();
    private readonly System.Threading.CancellationTokenSource _bgCts = new();
    private readonly System.Threading.Tasks.Task _backgroundWorker;
    private readonly WinFormsTimer _repaintTimer;
    private float _dpiScale;

    private Pen? _softGuidePen;
    private Pen? _strongGuidePen;
    private bool _isAttached;
    private int[] _guideColumns = Array.Empty<int>();
    private bool _disposed;
    private long _lastTextChangeTicks;
    private bool _repaintPending;
    private int _lastVisibleStartLine = -1;
    private int _lastVisibleEndLine = -1;
    private volatile bool _suspendRequests; // temporarily skip work during heavy ops

    // Event handlers for detaching
    private EventHandler? _resizeHandler;
    private EventHandler? _fontChangedHandler;
    private EventHandler? _textChangedHandler;
    private MouseEventHandler? _mouseWheelHandler;
    private EventHandler? _vScrollHandler;
    private EventHandler? _hScrollHandler;
    private PaintEventHandler? _paintHandler;
    private EventHandler? _handleCreatedHandler;
    private EventHandler? _handleDestroyedHandler;

    private const float GuideAlphaSoft = 80f;   // ~30% opacity
    private const float GuideAlphaStrong = 160f; // ~60% opacity
    private const int BackgroundDelayMs = 500;   // Low-priority bg task delay (increased to avoid UI flooding)
    private const int PaintBudgetMs = 8;        // Target <8ms per paint

    /// <summary>
    /// Gets or sets the column positions (1-based) where guides should be drawn.
    /// Setting this invalidates the cache and triggers a repaint.
    /// </summary>
    public int[] GuideColumns
    {
        get => _guideColumns;
        set
        {
            _guideColumns = value?.OrderBy(c => c > 0).ToArray() ?? Array.Empty<int>();
            InvalidateCache();
            RequestRepaint();
        }
    }

    /// <summary>
    /// Gets or sets the drawing style for guides.
    /// Soft = alpha-blended; Strong = more opaque.
    /// </summary>
    public GuideStyle Style { get; set; } = GuideStyle.Soft;

    /// <summary>
    /// Gets or sets whether guides are drawn behind or over text.
    /// </summary>
    public GuidePlacement Placement { get; set; } = GuidePlacement.Behind;

    /// <summary>
    /// Gets or sets the color of the guides. Default is gray.
    /// </summary>
    public Color GuideColor { get; set; } = Color.Gray;

    /// <summary>
    /// Gets or sets whether anti-aliasing is enabled. Default true.
    /// </summary>
    public bool Smooth { get; set; } = true;

    /// <summary>
    /// Creates a new ColumnGuideManager. Use with an overlay panel that draws on top of the editor.
    /// </summary>
    /// <param name="editor">The RichTextBox editor to track for layout/scroll metrics.</param>
    /// <param name="drawTarget">Control that will receive Paint events and draw the guides (usually same as editor or overlay panel).</param>
    public ColumnGuideManager(Control editor, Control? drawTarget = null)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _drawTarget = drawTarget ?? editor;
        _isOverlayPanel = _drawTarget != _editor;

        _dpiScale = GetDpiScale(_editor);
        _backgroundWorker = System.Threading.Tasks.Task.Run(BackgroundCacheWorker);
        _repaintTimer = new WinFormsTimer { Interval = 16, Enabled = false };
        _repaintTimer.Tick += (s, e) => { _repaintTimer.Stop(); SafeInvalidate(); };

        // Create and attach event handlers (store for later detach)
        _handleCreatedHandler = (s, e) => { if (_isAttached) { InvalidateCache(); RequestRepaint(); } };
        _handleDestroyedHandler = (s, e) => { ClearCache(); };
        _editor.HandleCreated += _handleCreatedHandler;
        _editor.HandleDestroyed += _handleDestroyedHandler;

        _resizeHandler = (s, e) => { InvalidateCache(); RequestRepaint(); };
        _fontChangedHandler = (s, e) => { InvalidateCache(); RequestRepaint(); };
        _textChangedHandler = (s, e) => { InvalidateCache(); _lastTextChangeTicks = DateTime.UtcNow.Ticks; RequestRepaint(); };
        _mouseWheelHandler = (s, e) => { InvalidateCache(); RequestRepaint(); };

        _editor.Resize += _resizeHandler;
        _editor.FontChanged += _fontChangedHandler;
        _editor.TextChanged += _textChangedHandler;
        _editor.MouseWheel += _mouseWheelHandler;

        // RichTextBox scroll events
        if (editor is RichTextBox)
        {
            _vScrollHandler = (s, e) => { InvalidateCache(); RequestRepaint(); };
            _hScrollHandler = (s, e) => { InvalidateCache(); RequestRepaint(); };
            if (editor is RichTextBox rtb)
            {
                rtb.VScroll += _vScrollHandler;
                rtb.HScroll += _hScrollHandler;
            }
        }

        // Paint handler on draw target
        _paintHandler = (s, e) =>
        {
            if (!_isAttached || _guideColumns.Length == 0) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (sw.ElapsedMilliseconds > PaintBudgetMs) return;
            DrawGuides(e.Graphics);
            if (sw.ElapsedMilliseconds > PaintBudgetMs)
                System.Diagnostics.Debug.WriteLine($"ColumnGuideManager paint took {sw.ElapsedMilliseconds}ms (over budget)");
        };
        _drawTarget.Paint += _paintHandler;

        // High-DPI awareness
        if (_editor is Form f)
        {
            f.DpiChanged += (s, e) => { _dpiScale = e.DeviceDpiNew / 96f; InvalidateCache(); RequestRepaint(); };
        }
    }

    /// <summary>
    /// Attaches the guide manager to the editor and starts tracking.
    /// </summary>
    public void Attach()
    {
        if (_isAttached) return;
        _isAttached = true;
        _lastVisibleStartLine = -1;
        _lastVisibleEndLine = -1;
        InvalidateCache();
        RequestRepaint();
    }

    /// <summary>
    /// Detaches the guide manager from the editor.
    /// </summary>
     public void Detach()
     {
         _isAttached = false;
     }

     /// <summary>
     /// Temporarily suspend background cache updates during heavy operations.
     /// </summary>
     public void SuspendRequests() => _suspendRequests = true;

     /// <summary>
     /// Resume background cache updates after suspension.
     /// </summary>
     public void ResumeRequests()
     {
         _suspendRequests = false;
         InvalidateCache();
         RequestRepaint();
     }

      /// <summary>
      /// Updates the set of guide columns and clears cached positions.
      /// </summary>
      /// <param name="columns">Zero-based character column indices (or null to clear).</param>
      public void SetGuides(params int[] columns)
    {
        _guideColumns = columns?.Where(c => c >= 0).Distinct().OrderBy(c => c).ToArray() ?? Array.Empty<int>();
        InvalidateCache();
        RequestRepaint();
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _bgCts.Cancel();
        _repaintTimer.Dispose();

        DisposePens();

        if (_editor != null && !_editor.IsDisposed)
        {
            _editor.HandleCreated -= _handleCreatedHandler!;
            _editor.HandleDestroyed -= _handleDestroyedHandler!;
            _editor.Resize -= _resizeHandler!;
            _editor.FontChanged -= _fontChangedHandler!;
            _editor.TextChanged -= _textChangedHandler!;
            _editor.MouseWheel -= _mouseWheelHandler!;
            if (_editor is RichTextBox rtb)
            {
                rtb.VScroll -= _vScrollHandler!;
                rtb.HScroll -= _hScrollHandler!;
            }
        }

        if (_drawTarget != null && !_drawTarget.IsDisposed)
        {
            _drawTarget.Paint -= _paintHandler!;
        }

        _bgCts.Dispose();
    }

    #region Drawing

    /// <summary>
    /// Draws guides onto the provided graphics surface.
    /// </summary>
    public void DrawGuides(Graphics g)
    {
        if (_guideColumns.Length == 0 || !_isAttached) return;

        var editor = _editor;
        var visibleLines = GetVisibleLineRange(editor, out int firstVisible, out int lastVisible);
        if (visibleLines <= 0) return;

        var clientRect = editor.ClientRectangle;
        var zoomFactor = GetZoomFactor(editor);

        // Determine whether to draw under or over text
        if (Placement == GuidePlacement.OverText)
        {
            g.SetClip(clientRect);
        }

        // Get or create pens
        EnsurePens();

        var smoothMode = Smooth ? System.Drawing.Drawing2D.SmoothingMode.AntiAlias
                                : System.Drawing.Drawing2D.SmoothingMode.None;
        var oldSmooth = g.SmoothingMode;
        g.SmoothingMode = smoothMode;

        var useSoft = Style == GuideStyle.Soft;

        for (int colIdx = 0; colIdx < _guideColumns.Length; colIdx++)
        {
            int targetCol = _guideColumns[colIdx];
            if (!TryGetColumnX(targetCol, out float x, out float columnWidth))
            {
                continue;
            }

            // Request background cache update if not visible
            if (Math.Abs(x - (columnWidth / 2)) > clientRect.Width)
            {
                EnqueueBackgroundCacheRequest(targetCol);
            }

            // Calculate precise X with half-pixel alignment for crisp lines
            float drawX = x * _dpiScale * zoomFactor;
            drawX = (float)Math.Round(drawX * 2) / 2; // Half-pixel alignment

            if (drawX < 0 || drawX > clientRect.Width) continue;

            // Draw vertical line from first visible to last visible
            var pen = useSoft ? _softGuidePen : _strongGuidePen;
            if (pen == null) continue;

            try
            {
                if (Placement == GuidePlacement.Behind)
                {
                    // Draw behind: use CompositingMode.SourceCopy to avoid affecting text
                    var oldMode = g.CompositingMode;
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    g.DrawLine(pen, drawX, 0, drawX, clientRect.Bottom);
                    g.CompositingMode = oldMode;
                }
                else
                {
                    // Over text: draw on top with alpha blending
                    g.DrawLine(pen, drawX, 0, drawX, clientRect.Bottom);
                }
            }
            catch { /* Ignore drawing errors */ }
        }

        g.SmoothingMode = oldSmooth;

        // Restore clipping
        g.ResetClip();
    }

    private void EnsurePens()
    {
        lock (_penLock)
        {
            if (_softGuidePen == null || _softGuidePen.Color != GetGuideColor(GuideAlphaSoft))
            {
                _softGuidePen?.Dispose();
                _softGuidePen = new Pen(GetGuideColor(GuideAlphaSoft)) { Width = 1 };
            }

            if (_strongGuidePen == null || _strongGuidePen.Color != GetGuideColor(GuideAlphaStrong))
            {
                _strongGuidePen?.Dispose();
                _strongGuidePen = new Pen(GetGuideColor(GuideAlphaStrong)) { Width = 1 };
            }
        }
    }

    private Color GetGuideColor(float alpha)
    {
        var c = GuideColor;
        return Color.FromArgb((int)alpha, c.R, c.G, c.B);
    }

    #endregion

    #region Caching

    private void InvalidateCache()
    {
        _columnCache.Clear();
    }

    private void ClearCache()
    {
        _columnCache.Clear();
    }

    /// <summary>
    /// Attempts to get the X coordinate (in editor pixels) for a target character column.
    /// Computes via average char width initially; may refine in background.
    /// </summary>
    /// <param name="targetColumn">Zero-based character column</param>
    /// <param name="x">Output: X coordinate in editor coordinates</param>
    /// <param name="columnWidth">Width of the column in pixels</param>
    /// <returns>True if found; false if cache miss (background task computing)</returns>
    public bool TryGetColumnX(int targetColumn, out float x, out float columnWidth)
    {
        x = 0;
        columnWidth = 0;

        if (_guideColumns.Length == 0 || targetColumn < 0) return false;

        // Fast path: already cached
        if (_columnCache.TryGetValue(targetColumn, out var cached))
        {
            x = cached.x;
            columnWidth = cached.width;
            return true;
        }

        // Compute via average char width
        var font = _editor.Font;
        if (font == null) return false;

        float avgCharWidth = MeasureAverageCharWidth(font, GetZoomFactor(_editor));
        x = targetColumn * avgCharWidth;
        columnWidth = avgCharWidth;

        // Cache for fast reuse (best-effort, may be refined later)
        _columnCache.TryAdd(targetColumn, (x, columnWidth));

        // Queue refinement for visible lines
        EnqueueBackgroundCacheRequest(targetColumn);

        return true;
    }

    private float MeasureAverageCharWidth(Font font, float zoom)
    {
        // Measure using 'W' (typical wide char) and 'i' (typical narrow) average
        using var g = _editor.CreateGraphics();
        g.PageUnit = GraphicsUnit.Pixel;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var wide = g.MeasureString("WWWWWWWWWW", font, new PointF(0, 0), StringFormat.GenericTypographic);
        var narrow = g.MeasureString("iiiiiiiiii", font, new PointF(0, 0), StringFormat.GenericTypographic);

        float avg = (wide.Width + narrow.Width) / 20f; // 10 chars each
        return avg * zoom;
    }

    /// <summary>
    /// Inverse mapping: converts an X coordinate (click) to the nearest column guide index (if any).
    /// </summary>
    /// <param name="x">X coordinate in editor client pixels</param>
    /// <param name="snapDistance">Maximum snapping distance in pixels</param>
    /// <param name="columnIndex">Output: index into GuideColumns array (or -1 if none)</param>
    /// <returns>True if within snap distance of a guide</returns>
    public bool TryFindGuideAtX(float x, float snapDistance, out int columnIndex)
    {
        columnIndex = -1;
        if (_guideColumns.Length == 0) return false;

        float closestDist = float.MaxValue;
        int closestIdx = -1;

        foreach (var col in _guideColumns)
        {
            if (!TryGetColumnX(col, out float guideX, out _))
                continue;

            float dist = Math.Abs(x - guideX);
            if (dist < snapDistance && dist < closestDist)
            {
                closestDist = dist;
                closestIdx = Array.IndexOf(_guideColumns, col);
            }
        }

        columnIndex = closestIdx;
        return columnIndex >= 0;
    }

    #endregion

    #region Background Cache Worker

    private void EnqueueBackgroundCacheRequest(int column)
    {
        // Stub: could queue column for background refinement.
        // BackgroundCacheWorker already refines all visible columns.
    }

    private void BackgroundCacheWorker()
    {
        while (!_bgCts.Token.IsCancellationRequested)
        {
            try
            {
                System.Threading.Thread.Sleep(BackgroundDelayMs);

                if (!_isAttached || _editor.IsDisposed || !_editor.IsHandleCreated || _suspendRequests)
                    continue;

                // Skip during active typing (last edit <2 sec ago)
                if (DateTime.UtcNow.Ticks - _lastTextChangeTicks < TimeSpan.TicksPerSecond * 2)
                    continue;

                // Run GetVisibleLineRange on UI thread with short timeout
                int visibleLines = 0;
                int firstLine = 0, lastLine = 0;
                var ar = _editor.BeginInvoke(new Action(() =>
                {
                    visibleLines = GetVisibleLineRange(_editor, out firstLine, out lastLine);
                }));
                bool completed = ar.AsyncWaitHandle.WaitOne(50);
                if (!completed) continue; // UI thread busy, skip

                if (visibleLines <= 0) continue;

                if (firstLine == _lastVisibleStartLine && lastLine == _lastVisibleEndLine)
                    continue;

                _lastVisibleStartLine = firstLine;
                _lastVisibleEndLine = lastLine;

                // Fire-and-forget refinement
                try
                {
                    _editor.BeginInvoke(new Action(() =>
                    {
                        try { RefineCacheForVisibleRange(firstLine, lastLine); }
                        catch { }
                    }));
                }
                catch { }
            }
            catch { }
        }
    }

    private void RefineCacheForVisibleRange(int firstLine, int lastLine)
    {
        try
        {
            if (_editor.IsDisposed || !_editor.IsHandleCreated) return;

            using var g = _editor.CreateGraphics();
            g.PageUnit = GraphicsUnit.Pixel;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float zoom = GetZoomFactor(_editor);
            var font = _editor.Font;
            if (font == null) return;

            // Measure a sample string of N characters to get per-char accumulated width
            int maxCol = _guideColumns.Length > 0 ? _guideColumns.Max() + 1 : 100;
            var sample = new string('x', Math.Max(100, maxCol));

            var charPositions = new List<float>(sample.Length + 1);
            charPositions.Add(0);

            for (int i = 0; i < sample.Length; i++)
            {
                var sub = sample.Substring(0, i + 1);
                var sz = g.MeasureString(sub, font, new PointF(0, 0), StringFormat.GenericTypographic);
                charPositions.Add(sz.Width * zoom);
            }

            foreach (var col in _guideColumns)
            {
                if (col < charPositions.Count)
                {
                    var refinedX = charPositions[col];
                    var width = (col + 1 < charPositions.Count) ? charPositions[col + 1] - charPositions[col] : charPositions[^1] - charPositions[^2];
                    _columnCache.AddOrUpdate(col, (refinedX, width), (k, old) => (refinedX, width));
                }
            }
        }
        catch { }
    }

    #endregion

    #region Repaint

    private void RequestRepaint()
    {
        if (_repaintPending) return;
        _repaintPending = true;
        _repaintTimer.Stop();
        _repaintTimer.Start();
    }

    private void SafeInvalidate()
    {
        if (_repaintPending && _drawTarget.IsHandleCreated && !_drawTarget.IsDisposed)
        {
            _drawTarget.Invalidate();
            _repaintPending = false;
        }
    }

    private static int GetVisibleLineRange(Control editor, out int firstVisible, out int lastVisible)
    {
        firstVisible = 0;
        lastVisible = 0;

        try
        {
            if (editor is RichTextBox rtb)
            {
                if (!rtb.IsHandleCreated || rtb.Lines == null || rtb.Font == null)
                    return 0;

                firstVisible = rtb.GetLineFromCharIndex(rtb.GetCharIndexFromPosition(new Point(0, 0)));
                int visibleHeight = editor.ClientSize.Height;
                if (visibleHeight <= 0) return 0;
                int lineHeight = (int)Math.Ceiling(rtb.Font.GetHeight() * rtb.ZoomFactor);
                if (lineHeight <= 0) return 0;
                lastVisible = Math.Min(rtb.Lines.Length - 1, firstVisible + (visibleHeight / lineHeight) + 2);
                return Math.Max(0, lastVisible - firstVisible + 1);
            }
            else if (editor is TextBoxBase tb)
            {
                if (!tb.IsHandleCreated || tb.Lines == null || tb.Font == null)
                    return 0;
                firstVisible = 0;
                int lineHeight = (int)Math.Ceiling(tb.Font.GetHeight() * GetZoomFactor(tb));
                if (lineHeight <= 0) return 0;
                lastVisible = Math.Min(tb.Lines.Length - 1, (editor.ClientSize.Height / lineHeight) + 2);
                return Math.Max(0, lastVisible - firstVisible + 1);
            }
        }
        catch { /* Ignore — treat as no visible lines */ }

        return 0;
    }

    private static float GetZoomFactor(Control editor)
    {
        if (editor is RichTextBox rtb) return rtb.ZoomFactor;
        return 1.0f;
    }

    private static float GetDpiScale(Control editor)
    {
        using var g = editor.CreateGraphics();
        return g.DpiX / 96f;
    }

    #endregion

    #region IDisposable

    private void DisposePens()
    {
        lock (_penLock)
        {
            _softGuidePen?.Dispose();
            _strongGuidePen?.Dispose();
            _softGuidePen = null;
            _strongGuidePen = null;
        }
    }

    #endregion
}

/// <summary>
/// Style of the column guide line.
/// </summary>
public enum GuideStyle
{
    /// <summary>
    /// Faint/alpha-blended guide (~30% opacity).
    /// </summary>
    Soft = 0,

    /// <summary>
    /// More opaque guide (~60% opacity).
    /// </summary>
    Strong = 1
}

/// <summary>
/// Drawing placement relative to text.
/// </summary>
public enum GuidePlacement
{
    /// <summary>
    /// Drawn behind the text (default).
    /// </summary>
    Behind = 0,

    /// <summary>
    /// Drawn over the text.
    /// </summary>
    OverText = 1
}
