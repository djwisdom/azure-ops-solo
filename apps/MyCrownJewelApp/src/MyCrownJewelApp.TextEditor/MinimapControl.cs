using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.TextEditor
{
    /// <summary>
    /// MinimapControl provides a scaled vertical overview of an editor's content.
    /// Pure WinForms implementation using GDI+ with double-buffering, downsampled bitmap
    /// rendering for performance, and full mouse/keyboard interaction.
    /// </summary>
    public class MinimapControl : Control
    {
        #region Win32 Interop

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_GETLINECOUNT = 0x00BA;
        private const int EM_LINESCROLL = 0x00B6;
        private const int EM_GETFIRSTCHARINDEX = 0x00CE; // same as EM_GETFIRSTVISIBLELINE for line index

        #endregion

        #region Fields

        private Control? _attachedEditor;
        private System.Windows.Forms.Timer? _pollTimer;
        private System.Windows.Forms.Timer? _throttleTimer;
        private Bitmap? _minimapBitmap;           // Downsampled full-document bitmap
        private Bitmap? _bufferBitmap;            // Double-buffer for display
        private Rectangle _viewportRect = Rectangle.Empty;
        private Rectangle _lastPolledViewport = Rectangle.Empty;
        private bool _isDragging;
        private Point _dragStartPoint;
        private Rectangle _viewportDragStart;
        private int _totalLines;
        private int _visibleLines;
        private float _scale = 1.0f;               // pixels per logical line in minimap
        private bool _showColors = false;
        private bool _throttlePending;

        // Token provider for syntax coloring (optional)
        private Func<int, IReadOnlyList<TokenInfo>?>? _tokenProvider;

        // Color mapping for syntax token types (used when ShowColors = true)
        private readonly Dictionary<SyntaxTokenType, Color> _tokenColors = new()
        {
            [SyntaxTokenType.Keyword] = Color.Blue,
            [SyntaxTokenType.String] = Color.Brown,
            [SyntaxTokenType.Comment] = Color.Green,
            [SyntaxTokenType.Number] = Color.Purple,
            [SyntaxTokenType.Identifier] = Color.Black,
            [SyntaxTokenType.Operator] = Color.DarkRed,
            [SyntaxTokenType.Preprocessor] = Color.Gray
        };

        #endregion

        #region Properties

        [Category("Layout"), Description("Width of the minimap control in pixels.")]
        public int MinimapWidth { get; set; } = 100;

        [Category("Appearance"), Description("Vertical scale factor (pixels per line). 1.0 = 1px/line, 0.5 = half density.")]
        public new float Scale
        {
            get => _scale;
            set
            {
                if (value <= 0) value = 0.1f;
                if (Math.Abs(_scale - value) > 0.01f)
                {
                    _scale = value;
                    InvalidateCaches();
                    ScheduleRedraw();
                }
            }
        }

        [Category("Appearance"), Description("Enables syntax-aware coloring when tokens are provided.")]
        public bool ShowColors
        {
            get => _showColors;
            set
            {
                if (_showColors != value)
                {
                    _showColors = value;
                    ScheduleRedraw();
                }
            }
        }

        [Category("Appearance"), Description("Background color of the minimap.")]
        public override Color BackColor { get; set; } = Color.WhiteSmoke;

        [Category("Appearance"), Description("Color of the viewport indicator rectangle (supports alpha).")]
        public Color ViewportColor { get; set; } = Color.FromArgb(128, Color.DodgerBlue);

        [Category("Appearance"), Description("Border color of the viewport indicator.")]
        public Color ViewportBorderColor { get; set; } = Color.DodgerBlue;

        /// <summary>Total vertical height of the minimap content in pixels (virtual space).</summary>
        public int VirtualHeight => _totalLines > 0 ? (int)Math.Ceiling(_totalLines * _scale) : Height;

        /// <summary>Current viewport start Y in minimap coordinates.</summary>
        public int ViewportStartY => _viewportRect.Y;

        /// <summary>Current viewport height in minimap coordinates.</summary>
        public int ViewportHeight => _viewportRect.Height;

        #endregion

        #region Events

        [Category("Behavior"), Description("Fired when the viewport position changes.")]
        public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;

        #endregion

        #region Constructor

        public MinimapControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            UpdateStyles();

            Width = MinimapWidth;
            MinimumSize = new Size(40, 0);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;

            // Throttled redraw: coalesce rapid updates into a single paint
            _throttleTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps max
            _throttleTimer.Tick += (s, e) =>
            {
                _throttleTimer.Stop();
                _throttlePending = false;
                RedrawImmediate();
            };

            // Polling timer: check editor state at ~30-60Hz
            _pollTimer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30Hz
            _pollTimer.Tick += (s, e) => CheckEditorState();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Attaches the minimap to an editor control (TextBoxBase or RichTextBox).
        /// </summary>
        public void AttachEditor(Control editor)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            if (_attachedEditor == editor) return;

            DetachEditor();

            _attachedEditor = editor;

            // Subscribe to editor events for immediate viewport updates
            if (editor is RichTextBox richTextBox)
            {
                richTextBox.VScroll += Editor_VScroll;
                richTextBox.KeyUp += Editor_KeyUp;
                richTextBox.MouseUp += Editor_MouseUp;
            }
            else if (editor is TextBoxBase textBoxBase)
            {
                // TextBoxBase does not have VScroll; rely on polling for scroll
                textBoxBase.KeyUp += Editor_KeyUp;
                textBoxBase.MouseUp += Editor_MouseUp;
            }
            else
            {
                // Generic Control: only polling supported
            }

            // Common events for all editor types that have TextChanged
            editor.TextChanged += Editor_TextChanged;
            editor.HandleCreated += Editor_HandleCreated;
            editor.Resize += Editor_Resize;

            _pollTimer?.Start();

            UpdateTotalLines();
            UpdateVisibleLines();
            UpdateViewportFromEditor();
            _lastPolledViewport = _viewportRect;
            ScheduleRedraw();
        }

        /// <summary>
        /// Detaches the minimap from the current editor.
        /// </summary>
        /// Detaches the minimap from the current editor.
        /// </summary>
        public void DetachEditor()
        {
            _pollTimer?.Stop();
            _throttleTimer?.Stop();

            if (_attachedEditor != null)
            {
                var editor = _attachedEditor;

                // Unsubscribe generic Control events
                editor.HandleCreated -= Editor_HandleCreated;
                editor.Resize -= Editor_Resize;
                editor.TextChanged -= Editor_TextChanged;
                editor.KeyUp -= Editor_KeyUp;
                editor.MouseUp -= Editor_MouseUp;

                // Unsubscribe RichTextBox-specific event
                if (editor is RichTextBox richTextBox)
                {
                    richTextBox.VScroll -= Editor_VScroll;
                }

                _attachedEditor = null;
            }

            _totalLines = 0;
            _viewportRect = Rectangle.Empty;
            _lastPolledViewport = Rectangle.Empty;
            InvalidateCaches();
            ScheduleRedraw();
        }

        /// <summary>
        /// Sets a token provider for optional syntax-aware coloring.
        /// The provider receives a line index and returns tokens for that line.
        /// </summary>
        public void SetTokenProvider(Func<int, IReadOnlyList<TokenInfo>?> tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }

        /// <summary>
        /// Forces an immediate redraw of the minimap.
        /// </summary>
        public void RefreshNow()
        {
            ScheduleRedraw();
        }

        #endregion

        #region Event Handlers

        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            UpdateTotalLines();
            UpdateViewportFromEditor();
            _lastPolledViewport = _viewportRect;
            InvalidateCaches();
            ScheduleRedraw();
        }

        private void Editor_Resize(object? sender, EventArgs e)
        {
            UpdateVisibleLines();
            UpdateViewportFromEditor();
            _lastPolledViewport = _viewportRect;
            ScheduleRedraw();
        }

        private void Editor_HandleCreated(object? sender, EventArgs e)
        {
            UpdateTotalLines();
            UpdateVisibleLines();
            UpdateViewportFromEditor();
            _lastPolledViewport = _viewportRect;
            ScheduleRedraw();
        }

        private void Editor_VScroll(object? sender, EventArgs e)
        {
            UpdateViewportFromEditor();
            if (!_viewportRect.Equals(_lastPolledViewport))
            {
                _lastPolledViewport = _viewportRect;
                ScheduleRedraw();
                RaiseViewportChanged();
            }
        }

        private void Editor_KeyUp(object? sender, KeyEventArgs e)
        {
            UpdateViewportFromEditor();
            if (!_viewportRect.Equals(_lastPolledViewport))
            {
                _lastPolledViewport = _viewportRect;
                ScheduleRedraw();
                RaiseViewportChanged();
            }
        }

        private void Editor_MouseUp(object? sender, MouseEventArgs e)
        {
            UpdateViewportFromEditor();
            if (!_viewportRect.Equals(_lastPolledViewport))
            {
                _lastPolledViewport = _viewportRect;
                ScheduleRedraw();
                RaiseViewportChanged();
            }
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>Converts a minimap Y coordinate to a logical line index.</summary>
        public int MinimapYToLine(int minimapY)
        {
            if (_scale <= 0) return 0;
            return (int)Math.Floor(minimapY / _scale);
        }

        /// <summary>Converts a logical line index to a minimap Y coordinate.</summary>
        public int LineToMinimapY(int lineIndex)
        {
            if (lineIndex < 0) lineIndex = 0;
            if (lineIndex >= _totalLines) lineIndex = _totalLines - 1;
            return (int)(lineIndex * _scale);
        }

        private int GetEditorFirstVisibleLine()
        {
            if (_attachedEditor?.IsHandleCreated == true)
            {
                return SendMessage(_attachedEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            }
            return 0;
        }

        private int GetEditorVisibleLines()
        {
            if (_attachedEditor is RichTextBox richTextBox)
            {
                if (richTextBox.Font.Height > 0)
                    return (int)Math.Ceiling(richTextBox.ClientSize.Height / (float)richTextBox.Font.Height);
            }
            else if (_attachedEditor is TextBoxBase textBoxBase)
            {
                if (textBoxBase.Font.Height > 0)
                    return (int)Math.Ceiling(textBoxBase.ClientSize.Height / (float)textBoxBase.Font.Height);
            }
            return 20;
        }

        private void UpdateViewportFromEditor()
        {
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated)
            {
                _viewportRect = Rectangle.Empty;
                return;
            }

            int firstVisible = GetEditorFirstVisibleLine();
            int visibleCount = GetEditorVisibleLines();

            // Add slight padding above so first line isn't flush with top
            if (firstVisible > 0) firstVisible--;

            int vpY = LineToMinimapY(firstVisible);
            int vpH = Math.Max(1, (int)Math.Ceiling(visibleCount * _scale));

            if (vpY + vpH > VirtualHeight) vpH = Math.Max(1, VirtualHeight - vpY);
            if (vpH < 1) vpH = 1;

            _viewportRect = new Rectangle(0, vpY, MinimapWidth, vpH);
        }

        private void UpdateTotalLines()
        {
            if (_attachedEditor?.IsHandleCreated == true)
            {
                _totalLines = SendMessage(_attachedEditor.Handle, EM_GETLINECOUNT, 0, 0);
                if (_totalLines < 0) _totalLines = 0;
            }
            else
            {
                _totalLines = 0;
            }
        }

        private void UpdateVisibleLines()
        {
            _visibleLines = GetEditorVisibleLines();
        }

        #endregion

        #region Editor State Polling

        private void CheckEditorState()
        {
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return;

            UpdateTotalLines();
            UpdateViewportFromEditor();

            if (!_viewportRect.Equals(_lastPolledViewport))
            {
                _lastPolledViewport = _viewportRect;
                ScheduleRedraw();
                RaiseViewportChanged();
            }
        }

        #endregion

        #region Drawing & Caching

        /// <summary>
        /// Invalidates cached bitmap data (call when document content changes).
        /// </summary>
        private void InvalidateCaches()
        {
            _minimapBitmap?.Dispose();
            _minimapBitmap = null;
        }

        private void ScheduleRedraw()
        {
            if (_throttleTimer != null && !_throttlePending)
            {
                _throttlePending = true;
                _throttleTimer.Start();
            }
        }

        private void RedrawImmediate()
        {
            if (Width <= 0 || Height <= 0) return;

            // Allocate buffer bitmap if needed
            if (_bufferBitmap == null || _bufferBitmap.Width != Width || _bufferBitmap.Height != Height)
            {
                _bufferBitmap?.Dispose();
                try
                {
                    _bufferBitmap = new Bitmap(Width, Height);
                }
                catch (ArgumentException)
                {
                    return;
                }
            }

            using (var g = Graphics.FromImage(_bufferBitmap))
            {
                g.Clear(BackColor);

                if (_attachedEditor == null || _totalLines == 0)
                {
                    DrawEmptyMessage(g);
                }
                else
                {
                    RenderMinimap(g);
                }
            }

            Invalidate(); // trigger OnPaint
        }

        /// <summary>
        /// Renders the minimap by drawing a downsampled representation of the document.
        /// For very large documents, we could render to a smaller offscreen bitmap and stretch.
        /// Current approach: draw visible line strips directly (O(visibleLines)).
        /// </summary>
        private void RenderMinimap(Graphics g)
        {
            if (_totalLines == 0) return;

            // Compute visible line range in minimap coordinates
            int viewportTop = _viewportRect.Top;
            int viewportBottom = _viewportRect.Bottom;

            int startLine = Math.Max(0, MinimapYToLine(viewportTop) - 1);
            int endLine = Math.Min(_totalLines - 1, MinimapYToLine(viewportBottom) + 1);

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Option A: Draw per-line (efficient for typical minimap heights ~600px)
            for (int line = startLine; line <= endLine; line++)
            {
                int y = LineToMinimapY(line);
                if (y >= Height) break;

                Color lineColor = GetLineColor(line);
                int lineH = Math.Max(1, (int)Math.Ceiling(_scale));
                int lineY = y;

                // Clip to viewport
                if (lineY + lineH <= 0) continue;
                if (lineY >= Height) break;

                Rectangle lineRect = new Rectangle(1, lineY, MinimapWidth - 2, lineH);
                if (lineRect.Right > Width) lineRect.Width = Math.Max(1, Width - lineRect.Left);
                if (lineRect.Bottom > Height) lineRect.Height = Math.Max(1, Height - lineRect.Top);
                if (lineRect.Width <= 0 || lineRect.Height <= 0) continue;

                using var brush = new SolidBrush(lineColor);
                g.FillRectangle(brush, lineRect);
            }

            // Alternative: Downsampled bitmap rendering (uncomment to enable)
            // RenderWithDownsampledBitmap(g);

            DrawViewportOverlay(g);
        }

        /// <summary>
        /// Alternative rendering: draw entire document to a downsampled bitmap once, then stretch-blit.
        /// This is more efficient for extremely large files (100K+ lines) but uses more memory.
        /// Current implementation uses per-visible-line drawing for simplicity and good performance.
        /// </summary>
        private void RenderWithDownsampledBitmap(Graphics g)
        {
            if (_minimapBitmap == null || _minimapBitmap.Width != MinimapWidth || _minimapBitmap.Height != VirtualHeight)
            {
                _minimapBitmap?.Dispose();
                _minimapBitmap = new Bitmap(MinimapWidth, Math.Min(VirtualHeight, 10000)); // cap at 10Kpx height
                using var bg = Graphics.FromImage(_minimapBitmap);
                bg.Clear(BackColor);
                for (int line = 0; line < _totalLines; line++)
                {
                    int y = LineToMinimapY(line);
                    if (y >= _minimapBitmap.Height) break;
                    Color c = GetLineColor(line);
                    using var brush = new SolidBrush(c);
                    int h = Math.Max(1, (int)_scale);
                    bg.FillRectangle(brush, 0, y, MinimapWidth, h);
                }
            }

            // Stretch draw to fit control height
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(_minimapBitmap, 0, 0, Width, Height);
        }

        private void DrawViewportOverlay(Graphics g)
        {
            if (_viewportRect.Width <= 0 || _viewportRect.Height <= 0) return;

            Rectangle safeRect = Rectangle.Intersect(_viewportRect, new Rectangle(0, 0, Width, Height));
            if (safeRect.Width <= 0 || safeRect.Height <= 0) return;

            using var fillBrush = new SolidBrush(ViewportColor);
            g.FillRectangle(fillBrush, safeRect);
            using var pen = new Pen(ViewportBorderColor, 2);
            g.DrawRectangle(pen, safeRect.X, safeRect.Y, safeRect.Width - 1, safeRect.Height - 1);
        }

        private void DrawEmptyMessage(Graphics g)
        {
            try
            {
                using var font = new Font("Segoe UI", 9);
                g.DrawString("Attach an editor to enable minimap.", font, Brushes.Gray, 10, 10);
            }
            catch
            {
                g.DrawString("Attach an editor to enable minimap.", Font, Brushes.Gray, 10, 10);
            }
        }

        private Color GetLineColor(int lineIndex)
        {
            if (ShowColors && _tokenProvider != null)
            {
                var tokens = _tokenProvider(lineIndex);
                if (tokens != null && tokens.Count > 0)
                {
                    // Prefer non-identifier tokens for semantic coloring
                    var token = tokens.FirstOrDefault(t => t.Type != SyntaxTokenType.Identifier) ?? tokens[0];
                    if (_tokenColors.TryGetValue(token.Type, out Color color))
                    {
                        // Lighten for minimap background
                        return ControlPaint.Light(color);
                    }
                }
            }
            return Color.Gray;
        }

        #endregion

        #region Interaction

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return;

            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _dragStartPoint = e.Location;
                _viewportDragStart = _viewportRect;

                if (_viewportRect.Contains(e.Location))
                {
                    Capture = true; // capture mouse for smooth drag
                }
                else
                {
                    // Click outside viewport: jump to that position
                    JumpToPosition(e.Y);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging && _attachedEditor != null && _attachedEditor.IsHandleCreated)
            {
                int deltaY = e.Y - _dragStartPoint.Y;
                int newY = _viewportDragStart.Y + deltaY;

                int maxY = VirtualHeight - _viewportRect.Height;
                if (newY < 0) newY = 0;
                if (newY > maxY) newY = maxY;

                _viewportRect.Y = newY;
                ScheduleRedraw();
                ScrollEditorToViewport();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = false;
                Capture = false;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return;

            int lines = e.Delta / SystemInformation.MouseWheelScrollDelta * SystemInformation.MouseWheelScrollLines;
            int newFirstVisible = GetEditorFirstVisibleLine() + lines;
            if (newFirstVisible < 0) newFirstVisible = 0;
            if (newFirstVisible >= _totalLines) newFirstVisible = _totalLines - 1;

            ScrollEditorToLine(newFirstVisible);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return;

            int linesToScroll = 1;
            int pagesToScroll = Math.Max(1, _visibleLines - 1);

            switch (e.KeyCode)
            {
                case Keys.Up: ScrollEditorByLines(-linesToScroll); break;
                case Keys.Down: ScrollEditorByLines(linesToScroll); break;
                case Keys.PageUp: ScrollEditorByLines(-pagesToScroll); break;
                case Keys.PageDown: ScrollEditorByLines(pagesToScroll); break;
                case Keys.Home: ScrollEditorToLine(0); break;
                case Keys.End: ScrollEditorToLine(Math.Max(0, _totalLines - _visibleLines)); break;
                case Keys.Left:
                case Keys.Right:
                    // Optional: horizontal scroll support if editor has horizontal scrolling
                    break;
            }

            e.Handled = true;
        }

        #endregion

        #region Editor Scrolling

        private void ScrollEditorToLine(int lineIndex)
        {
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return;

            if (_attachedEditor is RichTextBox)
            {
                // EM_LINESCROLL scrolls vertically by line count
                SendMessage(_attachedEditor.Handle, EM_LINESCROLL, 0, lineIndex);
            }
            else if (_attachedEditor is TextBoxBase textBoxBase)
            {
                // For TextBox, set selection to line start and scroll to caret
                if (lineIndex < 0 || lineIndex >= textBoxBase.Lines.Length) return;
                int charIndex = textBoxBase.GetFirstCharIndexFromLine(lineIndex);
                if (charIndex < 0) return;
                textBoxBase.SelectionStart = charIndex;
                textBoxBase.ScrollToCaret();
                textBoxBase.SelectionLength = 0;
            }

            UpdateViewportFromEditor();
            _lastPolledViewport = _viewportRect;
            ScheduleRedraw();
        }

        private void ScrollEditorByLines(int lineDelta)
        {
            int current = GetEditorFirstVisibleLine();
            ScrollEditorToLine(current + lineDelta);
        }

        private void ScrollEditorToViewport()
        {
            int targetLine = MinimapYToLine(_viewportRect.Y);
            ScrollEditorToLine(targetLine);
        }

        private void JumpToPosition(int minimapY)
        {
            int targetLine = MinimapYToLine(minimapY);
            ScrollEditorToLine(targetLine);
        }

        private void RaiseViewportChanged()
        {
            ViewportChanged?.Invoke(this, new ViewportChangedEventArgs(
                MinimapYToLine(_viewportRect.Y),
                Math.Max(1, (int)Math.Ceiling(_viewportRect.Height / _scale))));
        }

        #endregion

        #region Lifetime

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pollTimer?.Stop();
                _pollTimer?.Dispose();
                _throttleTimer?.Stop();
                _throttleTimer?.Dispose();
                _bufferBitmap?.Dispose();
                _minimapBitmap?.Dispose();
                DetachEditor();
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Overrides

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_bufferBitmap != null)
            {
                e.Graphics.DrawImageUnscaled(_bufferBitmap, 0, 0);
            }
            else
            {
                e.Graphics.Clear(BackColor);
                DrawEmptyMessage(e.Graphics);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ScheduleRedraw();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Token information for syntax-aware minimap coloring.
    /// </summary>
    public class TokenInfo
    {
        public SyntaxTokenType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int Length { get; set; }
    }

    /// <summary>
    /// Syntax token types recognised by the minimap coloring system.
    /// </summary>
    public enum SyntaxTokenType
    {
        Identifier,
        Keyword,
        String,
        Comment,
        Number,
        Operator,
        Preprocessor
    }

    /// <summary>
    /// Event arguments for ViewportChanged event.
    /// </summary>
    public class ViewportChangedEventArgs : EventArgs
    {
        public int FirstLine { get; }
        public int VisibleLines { get; }

        public ViewportChangedEventArgs(int firstLine, int visibleLines)
        {
            FirstLine = firstLine;
            VisibleLines = visibleLines;
        }
    }

    #endregion
}
