using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad
{
    public class MinimapControl : Control
    {
        public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_GETLINECOUNT = 0x00BA;
        private const int EM_LINESCROLL = 0x00B6;
        private const int EM_GETLINE = 0x00C4;

        private Control? _attachedEditor;
        private System.Windows.Forms.Timer? _pollTimer;
        private Rectangle _viewportRect = Rectangle.Empty;
        private bool _isDragging;
        private volatile int _totalLines;
        private float _lineScale;
        private Func<int, IReadOnlyList<TokenInfo>?>? _tokenProvider;

        private Bitmap? _fullMap;
        private bool _mapDirty = true;
        private int _mapGeneration;

        [Category("Layout")]
        public int MinimapWidth { get; set; } = 100;

        [Category("Appearance")]
        public Color ViewportColor { get; set; } = Color.FromArgb(60, 100, 180, 255);

        [Category("Appearance")]
        public Color ViewportBorderColor { get; set; } = Color.FromArgb(100, 180, 255);

        [Category("Appearance")]
        public Color BorderColor { get; set; } = Color.FromArgb(60, 60, 60);

        private bool _mouseHovering;
        private readonly Dictionary<int, Color> _tokenColorCache = new();

        public MinimapControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            TabStop = false;
            Width = MinimapWidth;
            MinimumSize = new Size(40, 0);

            _pollTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _pollTimer.Tick += (s, e) => PollEditor();
        }

        public void AttachEditor(Control editor)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            if (_attachedEditor == editor) return;
            DetachEditor();
            _attachedEditor = editor;
            editor.TextChanged += OnEditorTextChanged;
            if (editor is RichTextBox rtb)
                rtb.VScroll += (s, e) => Invalidate();
            editor.Resize += OnEditorResized;
            editor.HandleCreated += (s, e) => { _totalLines = CountLines(); MarkDirty(); Invalidate(); };
            _totalLines = CountLines();
            MarkDirty();
            _pollTimer?.Start();
            Invalidate();
        }

        private void OnEditorTextChanged(object? s, EventArgs e)
        {
            _totalLines = CountLines();
            MarkDirty();
            Invalidate();
        }

        private void OnEditorResized(object? s, EventArgs e)
        {
            MarkDirty();
            Invalidate();
        }

        public void DetachEditor()
        {
            if (_attachedEditor != null)
            {
                _attachedEditor.TextChanged -= OnEditorTextChanged;
                _attachedEditor.Resize -= OnEditorResized;
            }
            _pollTimer?.Stop();
            _attachedEditor = null;
            _totalLines = 0;
            _fullMap?.Dispose();
            _fullMap = null;
            Invalidate();
        }

        public void SetTokenProvider(Func<int, IReadOnlyList<TokenInfo>?> tokenProvider)
        {
            _tokenProvider = tokenProvider;
            MarkDirty();
            Invalidate();
        }

        public void RefreshNow() { MarkDirty(); Invalidate(); }

        public void MarkDirty()
        {
            _mapDirty = true;
            _mapGeneration++;
        }

        private int CountLines()
        {
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return 0;
            return SendMessage(_attachedEditor.Handle, EM_GETLINECOUNT, 0, 0);
        }

        private int GetFirstVisibleLine()
        {
            if (_attachedEditor?.IsHandleCreated == true)
                return SendMessage(_attachedEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            return 0;
        }

        private string GetLineText(int lineIndex)
        {
            if (_attachedEditor is RichTextBox rtb && rtb.IsHandleCreated)
            {
                IntPtr ptr = Marshal.AllocCoTaskMem(4096 * 2);
                try
                {
                    Marshal.WriteInt16(ptr, (short)4096);
                    int len = SendMessage(rtb.Handle, EM_GETLINE, lineIndex, ptr);
                    return len > 0 ? Marshal.PtrToStringUni(ptr, len)!.TrimEnd('\r', '\n') : string.Empty;
                }
                finally { Marshal.FreeCoTaskMem(ptr); }
            }
            return string.Empty;
        }

        private void ScrollToLine(int targetLine)
        {
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return;
            targetLine = Math.Max(0, Math.Min(_totalLines - 1, targetLine));
            int current = GetFirstVisibleLine();
            int delta = targetLine - current;
            if (delta != 0)
                SendMessage(_attachedEditor.Handle, EM_LINESCROLL, 0, delta);
        }

        private void PollEditor()
        {
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return;
            int total = CountLines();
            if (total != _totalLines) { _totalLines = total; MarkDirty(); Invalidate(); return; }
            int firstVis = GetFirstVisibleLine();
            int visHeight = _attachedEditor.ClientSize.Height;
            int lineH = Math.Max(1, _attachedEditor.Font.Height);
            int visibleLines = visHeight / lineH + 1;

            if (_totalLines > 0)
                _lineScale = Math.Max(0.5f, Height / (float)Math.Max(_totalLines, visibleLines));
            else
                _lineScale = 1.0f;

            int vpY = (int)(firstVis * _lineScale);
            int vpH = Math.Max(2, (int)(visibleLines * _lineScale));
            vpY = Math.Max(0, Math.Min(vpY, Height - vpH));
            vpH = Math.Min(vpH, Height - vpY);
            var newRect = new Rectangle(0, vpY, Width, vpH);
            if (newRect != _viewportRect)
            {
                if (Math.Abs(newRect.Y - _viewportRect.Y) < 3 && _viewportRect.Height == newRect.Height)
                    _viewportRect = newRect;
                else
                {
                    _viewportRect = newRect;
                    Invalidate();
                    RaiseViewportChanged();
                }
            }
        }

        private void RaiseViewportChanged()
        {
            var h = _attachedEditor;
            if (h == null) return;
            int vis = h.ClientSize.Height / Math.Max(1, h.Font.Height) + 1;
            ViewportChanged?.Invoke(this, new ViewportChangedEventArgs(
                Math.Max(0, (int)(_viewportRect.Y / _lineScale)),
                vis));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            if (_attachedEditor == null || _totalLines == 0) return;

            int mapW = Width - 4;
            int mapH = Height;
            if (mapW <= 0) return;

            int firstVis = GetFirstVisibleLine();
            int visHeight = _attachedEditor.ClientSize.Height;
            int lineH = Math.Max(1, _attachedEditor.Font.Height);
            int visibleLines = visHeight / lineH + 5;

            if (_totalLines > 0)
                _lineScale = Math.Max(0.5f, mapH / (float)Math.Max(_totalLines, visibleLines));
            else
                _lineScale = 1.0f;

            if (_mapDirty || _fullMap == null || _fullMap.Width != mapW || _fullMap.Height != mapH)
                RebuildFullMap(mapW, mapH);

            if (_fullMap != null)
                g.DrawImage(_fullMap, 2, 0);

            int vpY = (int)(firstVis * _lineScale);
            int vpH = Math.Max(4, (int)(visibleLines * _lineScale));
            vpY = Math.Max(0, Math.Min(vpY, Height - vpH));
            vpH = Math.Min(vpH, Height - vpY);
            _viewportRect = new Rectangle(2, vpY, mapW, vpH);

            if (_mouseHovering || _isDragging)
            {
                using var vpBrush = new SolidBrush(ViewportColor);
                g.FillRectangle(vpBrush, _viewportRect);
                using var vpPen = new Pen(ViewportBorderColor, 1);
                g.DrawRectangle(vpPen, _viewportRect.X, _viewportRect.Y, _viewportRect.Width - 1, _viewportRect.Height - 1);
            }
        }

        private void RebuildFullMap(int mapW, int mapH)
        {
            _fullMap?.Dispose();
            _fullMap = null;
            if (_totalLines <= 0 || mapW <= 0 || mapH <= 0) return;

            _fullMap = new Bitmap(mapW, mapH, PixelFormat.Format32bppArgb);
            using var mg = Graphics.FromImage(_fullMap);
            var theme = ThemeManager.Instance.CurrentTheme;
            mg.Clear(theme.EditorBackground);

            int contentHeight = (int)(_totalLines * _lineScale);
            int renderRows = Math.Min(mapH, contentHeight);
            float linesPerPixel = _totalLines / (float)Math.Max(1, renderRows);
            double accum = 0;

            for (int pixelRow = 0; pixelRow < renderRows; pixelRow++)
            {
                accum += linesPerPixel;
                int count = (int)Math.Floor(accum);
                accum -= count;
                if (count <= 0) continue;

                // Sample the middle line of this pixel-row's range for content/color
                int sampleLine = (int)(pixelRow * linesPerPixel + count * 0.5f);
                if (sampleLine >= _totalLines) break;

                string text = GetLineText(sampleLine);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var tokens = _tokenProvider?.Invoke(sampleLine);
                int x = 0;

                for (int pos = 0; pos < text.Length; pos++)
                {
                    char c = text[pos];
                    if (c == '\r' || c == '\n') break;
                    if (c == '\t')
                    {
                        x = ((x / 4) + 1) * 4;
                        continue;
                    }
                    if (x >= mapW) break;

                    if (c != ' ')
                    {
                        Color color = GetCharColor(tokens, pos, text.Length);
                        color = Color.FromArgb(200, color);
                        using var brush = new SolidBrush(color);
                        mg.FillRectangle(brush, x, pixelRow, 1, 1);
                    }
                    x++;
                }
            }

            _mapDirty = false;
        }

        private Color GetCharColor(IReadOnlyList<TokenInfo>? tokens, int charIndex, int textLength)
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            if (tokens == null) return theme.Text;

            foreach (var token in tokens)
            {
                if (charIndex >= token.StartIndex && charIndex < token.StartIndex + token.Length)
                {
                    return token.Type switch
                    {
                        SyntaxTokenType.Keyword => theme.KeywordColor,
                        SyntaxTokenType.String => theme.StringColor,
                        SyntaxTokenType.Comment => theme.CommentColor,
                        SyntaxTokenType.Number => theme.NumberColor,
                        SyntaxTokenType.Preprocessor => theme.PreprocessorColor,
                        SyntaxTokenType.Type => theme.TypeColor,
                        _ => theme.Text
                    };
                }
            }
            return theme.Text;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return;
            if (e.Button != MouseButtons.Left) return;

            var adjusted = new Point(e.X - 2, e.Y);
            if (_viewportRect.Contains(adjusted))
            {
                _isDragging = true;
                Capture = true;
            }
            else
            {
                int targetLine = (int)(e.Y / _lineScale);
                targetLine = Math.Max(0, Math.Min(_totalLines - 1, targetLine));
                int visHeight = _attachedEditor.ClientSize.Height;
                int lineH = Math.Max(1, _attachedEditor.Font.Height);
                int visibleLines = visHeight / lineH;
                int firstLine = Math.Max(0, targetLine - visibleLines / 2);
                ScrollToLine(firstLine);
                RaiseViewportChanged();
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isDragging || _attachedEditor == null) return;

            int targetLine = (int)(e.Y / _lineScale);
            targetLine = Math.Max(0, Math.Min(_totalLines - 1, targetLine));
            int visHeight = _attachedEditor.ClientSize.Height;
            int lineH = Math.Max(1, _attachedEditor.Font.Height);
            int visibleLines = visHeight / lineH + 1;
            int firstLine = Math.Max(0, Math.Min(targetLine, _totalLines - visibleLines));
            ScrollToLine(firstLine);
            RaiseViewportChanged();
            Invalidate();
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

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _mouseHovering = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _mouseHovering = false;
            if (!_isDragging)
            {
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            MarkDirty();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pollTimer?.Stop();
                _pollTimer?.Dispose();
                _fullMap?.Dispose();
                DetachEditor();
            }
            base.Dispose(disposing);
        }
    }

    public class TokenInfo
    {
        public SyntaxTokenType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int Length { get; set; }
    }

    public enum SyntaxTokenType
    {
        Identifier, Keyword, String, Comment, Number, Operator, Preprocessor, Type
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
}
