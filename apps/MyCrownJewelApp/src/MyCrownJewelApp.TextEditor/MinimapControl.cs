using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.TextEditor
{
    /// <summary>
    /// MinimapControl provides a scaled vertical overview of an editor's content.
    /// Pure WinForms implementation using GDI+ with double-buffering and polling-based
    /// viewport synchronization (works with TextBox and RichTextBox).
    /// </summary>
    public class MinimapControl : Control
    {
        #region Win32 Interop

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int EM_GETFIRSTVISIBLELINE = 0xCE;
        private const int EM_GETLINECOUNT = 0xBA;
        private const int EM_LINESCROLL = 0xB6;

        #endregion

        #region Fields

        private Control? _attachedEditor;
        private System.Windows.Forms.Timer? _redrawTimer;
        private System.Windows.Forms.Timer? _pollTimer;
        private bool _redrawPending;
        private Bitmap? _bufferBitmap;
        private Rectangle _viewportRect = Rectangle.Empty;
        private Rectangle _lastPolledViewport = Rectangle.Empty; // used for change detection
        private bool _isDragging;
        private Point _dragStartPoint;
        private Rectangle _viewportDragStart;
        private int _totalLines;
        private int _visibleLines;

        // Syntax coloring support
        private Func<int, IReadOnlyList<TokenInfo>?>? _tokenProvider;
        private Dictionary<SyntaxTokenType, Color> _tokenColors = new()
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

        [Category("Appearance"), Description("Vertical scale factor for the minimap content.")]
        public new float Scale { get; set; } = 0.5f;

        [Category("Appearance"), Description("Enables syntax-aware coloring when tokens are provided.")]
        public bool ShowColors { get; set; } = false;

        [Category("Appearance"), Description("Background color of the minimap.")]
        public override Color BackColor { get; set; } = Color.WhiteSmoke;

        [Category("Appearance"), Description("Color of the viewport indicator rectangle.")]
        public Color ViewportColor { get; set; } = Color.FromArgb(128, Color.LightBlue);

        [Category("Appearance"), Description("Border color of the viewport indicator.")]
        public Color ViewportBorderColor { get; set; } = Color.DodgerBlue;

        public int VirtualHeight => _totalLines > 0 ? (int)Math.Ceiling(_totalLines * Scale) : Height;
        public int ViewportStartY => _viewportRect.Y;
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

            // Redraw throttling timer (~20 FPS)
            _redrawTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _redrawTimer.Tick += (s, e) => { _redrawTimer!.Stop(); RedrawImmediate(); };

            // Polling timer for editor viewport changes (~33 FPS)
            _pollTimer = new System.Windows.Forms.Timer { Interval = 30 };
            _pollTimer.Tick += (s, e) => CheckEditorState();
        }

        #endregion

        #region Public API

        public void AttachEditor(Control editor)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            if (_attachedEditor == editor) return;

            DetachEditor();

            _attachedEditor = editor;

            // Subscribe to events that affect layout or content
            if (editor is TextBoxBase textBoxBase)
            {
                textBoxBase.TextChanged += Editor_TextChanged;
                textBoxBase.HandleCreated += Editor_HandleCreated;
                textBoxBase.HandleDestroyed += Editor_HandleDestroyed;
                textBoxBase.Resize += Editor_Resize;
            }
            else
            {
                editor.HandleCreated += Editor_HandleCreated;
                editor.HandleDestroyed += Editor_HandleDestroyed;
                editor.Resize += Editor_Resize;
            }

            _pollTimer?.Start();

            UpdateTotalLines();
            UpdateViewportFromEditor();
            _lastPolledViewport = _viewportRect;
            ScheduleRedraw();
        }

        public void DetachEditor()
        {
            _pollTimer?.Stop();

            if (_attachedEditor != null)
            {
                if (_attachedEditor is TextBoxBase textBoxBase)
                {
                    textBoxBase.TextChanged -= Editor_TextChanged;
                    textBoxBase.HandleCreated -= Editor_HandleCreated;
                    textBoxBase.HandleDestroyed -= Editor_HandleDestroyed;
                    textBoxBase.Resize -= Editor_Resize;
                }
                else
                {
                    _attachedEditor.HandleCreated -= Editor_HandleCreated;
                    _attachedEditor.HandleDestroyed -= Editor_HandleDestroyed;
                    _attachedEditor.Resize -= Editor_Resize;
                }

                _attachedEditor = null;
            }

            _totalLines = 0;
            _viewportRect = Rectangle.Empty;
            _lastPolledViewport = Rectangle.Empty;
            ScheduleRedraw();
        }

        public void SetTokenProvider(Func<int, IReadOnlyList<TokenInfo>?> tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }

        #endregion

        #region Event Handlers

        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            UpdateTotalLines();
            UpdateViewportFromEditor();
            _lastPolledViewport = _viewportRect;
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

        private void Editor_HandleDestroyed(object? sender, EventArgs e)
        {
            DetachEditor();
        }

        #endregion

        #region Coordinate Conversion

        public int MinimapYToLine(int minimapY)
        {
            if (Scale <= 0) return 0;
            return (int)Math.Floor(minimapY / Scale);
        }

        public int LineToMinimapY(int lineIndex)
        {
            if (lineIndex < 0) lineIndex = 0;
            if (lineIndex >= _totalLines) lineIndex = _totalLines - 1;
            int y = (int)(lineIndex * Scale);
            if (y < 0) y = 0;
            return y;
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
                    return richTextBox.ClientSize.Height / richTextBox.Font.Height;
            }
            else if (_attachedEditor is TextBoxBase textBoxBase)
            {
                if (textBoxBase.Font.Height > 0)
                    return textBoxBase.ClientSize.Height / textBoxBase.Font.Height;
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

            if (firstVisible > 0) firstVisible--; // include partial top

            int vpY = LineToMinimapY(firstVisible);
            int vpH = Math.Max(1, (int)Math.Ceiling(visibleCount * Scale));

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

        #region Drawing

        private void ScheduleRedraw()
        {
            if (_redrawTimer != null && !_redrawPending)
            {
                _redrawPending = true;
                _redrawTimer.Start();
            }
        }

        private void RedrawImmediate()
        {
            _redrawPending = false;

            if (_attachedEditor == null || _totalLines == 0)
            {
                using var g = CreateGraphics();
                g.Clear(BackColor);
                DrawEmptyMessage(g);
                return;
            }

            // Guard against invalid dimensions
            if (Width <= 0 || Height <= 0) return;

            if (_bufferBitmap == null || _bufferBitmap.Width != Width || _bufferBitmap.Height != Height)
            {
                _bufferBitmap?.Dispose();
                try
                {
                    _bufferBitmap = new Bitmap(Width, Height);
                }
                catch (ArgumentException)
                {
                    // Invalid dimensions — skip this redraw
                    return;
                }
            }

            using (var g = Graphics.FromImage(_bufferBitmap))
            {
                g.Clear(BackColor);
                DrawMinimap(g);
            }

            using (var g = Graphics.FromHwnd(Handle))
            {
                g.DrawImageUnscaled(_bufferBitmap, 0, 0);
            }
        }

        private void DrawMinimap(Graphics g)
        {
            if (_totalLines == 0) return;

            int startLine = Math.Max(0, MinimapYToLine(_viewportRect.Top) - 1);
            int endLine = Math.Min(_totalLines - 1, MinimapYToLine(_viewportRect.Bottom) + 1);

            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            for (int line = startLine; line <= endLine; line++)
            {
                int y = LineToMinimapY(line);
                if (y >= Height) break;

                Color lineColor = GetLineColor(line);
                int lineH = Math.Max(1, (int)Scale);
                if (lineH <= 0) lineH = 1;
                Rectangle lineRect = new Rectangle(1, y, MinimapWidth - 2, lineH);
                // Clamp rectangle to control bounds
                if (lineRect.Right > Width) lineRect.Width = Math.Max(1, Width - lineRect.Left);
                if (lineRect.Bottom > Height) lineRect.Height = Math.Max(1, Height - lineRect.Top);
                if (lineRect.Width <= 0 || lineRect.Height <= 0) continue;

                using var brush = new SolidBrush(lineColor);
                g.FillRectangle(brush, lineRect);
            }

            DrawViewportOverlay(g);
        }

        private void DrawViewportOverlay(Graphics g)
        {
            // Guard against invalid viewport dimensions
            if (_viewportRect.Width <= 0 || _viewportRect.Height <= 0) return;

            using var fillBrush = new SolidBrush(ViewportColor);
            g.FillRectangle(fillBrush, _viewportRect);
            using var pen = new Pen(ViewportBorderColor, 2);
            // Draw with safe coordinates
            Rectangle safeRect = Rectangle.Intersect(_viewportRect, new Rectangle(0, 0, Width, Height));
            if (safeRect.Width > 0 && safeRect.Height > 0)
            {
                g.DrawRectangle(pen, safeRect.X, safeRect.Y, safeRect.Width - 1, safeRect.Height - 1);
            }
        }

        private void DrawEmptyMessage(Graphics g)
        {
            try
            {
                using var font = new Font("Segoe UI", 9);
                var size = g.MeasureString("Attach an editor to enable minimap.", font);
                var x = (Width - (int)size.Width) / 2;
                var y = (Height - (int)size.Height) / 2;
                g.DrawString("Attach an editor to enable minimap.", font, Brushes.Gray, x, y);
            }
            catch
            {
                // Fallback: draw simple text without custom font
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
                    var token = tokens.FirstOrDefault(t => t.Type != SyntaxTokenType.Identifier) ?? tokens[0];
                    if (_tokenColors.TryGetValue(token.Type, out Color color))
                    {
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
                    Capture = true;
                }
                else
                {
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
            int pagesToScroll = _visibleLines;

            switch (e.KeyCode)
            {
                case Keys.Up: ScrollEditorByLines(-linesToScroll); break;
                case Keys.Down: ScrollEditorByLines(linesToScroll); break;
                case Keys.PageUp: ScrollEditorByLines(-pagesToScroll); break;
                case Keys.PageDown: ScrollEditorByLines(pagesToScroll); break;
                case Keys.Home: ScrollEditorToLine(0); break;
                case Keys.End: ScrollEditorToLine(_totalLines - _visibleLines); break;
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
                SendMessage(_attachedEditor.Handle, EM_LINESCROLL, 0, lineIndex);
            }
            else if (_attachedEditor is TextBoxBase textBoxBase)
            {
                int charIndex = textBoxBase.GetFirstCharIndexFromLine(lineIndex);
                textBoxBase.SelectionStart = charIndex;
                textBoxBase.ScrollToCaret();
                textBoxBase.SelectionLength = 0;
            }

            // Update viewport immediately to reflect change; polling will also confirm
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
                Math.Max(1, (int)Math.Ceiling(_viewportRect.Height / Scale))));
        }

        #endregion

        #region Lifetime

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _redrawTimer?.Stop();
                _redrawTimer?.Dispose();
                _pollTimer?.Stop();
                _pollTimer?.Dispose();
                _bufferBitmap?.Dispose();
                DetachEditor();
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Overrides

        protected override void OnPaint(PaintEventArgs e)
        {
            // Painting done via double-buffered bitmap in RedrawImmediate
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

    public class TokenInfo
    {
        public SyntaxTokenType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int Length { get; set; }
    }

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
