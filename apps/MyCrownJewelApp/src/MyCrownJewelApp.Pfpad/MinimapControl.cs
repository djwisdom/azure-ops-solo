using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, System.Text.StringBuilder lParam);
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

        private static readonly Color _defaultLineColor = Color.FromArgb(180, 180, 180);

        [Category("Layout")]
        public int MinimapWidth { get; set; } = 100;

        [Category("Appearance")]
        public Color ViewportColor { get; set; } = Color.FromArgb(60, 100, 180, 255);

        [Category("Appearance")]
        public Color ViewportBorderColor { get; set; } = Color.FromArgb(100, 180, 255);

        [Category("Appearance")]
        public Color BorderColor { get; set; } = Color.FromArgb(60, 60, 60);

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

            _pollTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _pollTimer.Tick += (s, e) => PollEditor();
        }

        public void AttachEditor(Control editor)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            if (_attachedEditor == editor) return;
            DetachEditor();
            _attachedEditor = editor;
            editor.TextChanged += (s, e) => { _totalLines = CountLines(); Invalidate(); };
            if (editor is RichTextBox rtb)
                rtb.VScroll += (s, e) => Invalidate();
            editor.Resize += (s, e) => Invalidate();
            editor.HandleCreated += (s, e) => { _totalLines = CountLines(); Invalidate(); };
            _totalLines = CountLines();
            _pollTimer?.Start();
            Invalidate();
        }

        public void DetachEditor()
        {
            _pollTimer?.Stop();
            _attachedEditor = null;
            _totalLines = 0;
            Invalidate();
        }

        public void SetTokenProvider(Func<int, IReadOnlyList<TokenInfo>?> tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }

        public void RefreshNow() => Invalidate();

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
                var sb = new System.Text.StringBuilder(4096);
                int len = SendMessage(rtb.Handle, EM_GETLINE, lineIndex, sb);
                return len > 0 ? sb.ToString() : string.Empty;
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
            if (total != _totalLines) { _totalLines = total; Invalidate(); return; }
            int firstVis = GetFirstVisibleLine();
            int visHeight = _attachedEditor.ClientSize.Height;
            int lineH = Math.Max(1, _attachedEditor.Font.Height);
            int visibleLines = visHeight / lineH + 1;

            if (_totalLines > 0)
                _lineScale = Math.Max(0.5f, Height / (float)_totalLines);
            else
                _lineScale = 1.0f;

            int vpY = (int)(firstVis * _lineScale);
            int vpH = Math.Max(2, (int)(visibleLines * _lineScale));
            vpY = Math.Max(0, Math.Min(vpY, Height - vpH));
            vpH = Math.Min(vpH, Height - vpY);
            var newRect = new Rectangle(0, vpY, Width, vpH);
            if (newRect != _viewportRect) { _viewportRect = newRect; Invalidate(); RaiseViewportChanged(); }
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

            using (var borderPen = new Pen(BorderColor, 1))
                g.DrawLine(borderPen, 0, 0, 0, Height);

            if (_attachedEditor == null || _totalLines == 0) return;

            int firstVis = GetFirstVisibleLine();
            int visHeight = _attachedEditor.ClientSize.Height;
            int lineH = Math.Max(1, _attachedEditor.Font.Height);
            int visibleLines = visHeight / lineH + 5;

            int startLine = Math.Max(0, firstVis - 3);
            int endLine = Math.Min(_totalLines - 1, firstVis + visibleLines);

            if (_totalLines > 0)
                _lineScale = Math.Max(0.5f, Height / (float)_totalLines);
            else
                _lineScale = 1.0f;

            int lineHt = Math.Max(2, (int)Math.Ceiling(_lineScale));
            int contentWidth = Width - 4;

            for (int line = startLine; line <= endLine; line++)
            {
                int y = (int)(line * _lineScale);
                if (y >= Height) break;

                string text = GetLineText(line);
                if (string.IsNullOrEmpty(text)) continue;

                int maxLen = Math.Min(text.Length, contentWidth / Math.Max(1, lineHt) + 1);
                if (maxLen <= 0) continue;

                var tokens = _tokenProvider?.Invoke(line);
                var tokenColors = BuildColorMap(tokens, text.Length, _defaultLineColor);

                int x = 2;
                for (int i = 0; i < maxLen && x < Width - 2; i++)
                {
                    char c = text[i];
                    if (c == '\r' || c == '\n' || c == '\t')
                    {
                        x += lineHt;
                        continue;
                    }
                    int charW = Math.Max(1, lineHt);
                    if (x + charW > Width - 2) break;

                    var color = tokenColors.TryGetValue(i, out var tc) ? tc : _defaultLineColor;
                    using var brush = new SolidBrush(Color.FromArgb(200, color));
                    g.FillRectangle(brush, x, y, charW, lineHt);
                    x += charW;
                }
            }

            int vpY = (int)(firstVis * _lineScale);
            int vpH = Math.Max(4, (int)(visibleLines * _lineScale));
            vpY = Math.Max(0, Math.Min(vpY, Height - vpH));
            vpH = Math.Min(vpH, Height - vpY);
            _viewportRect = new Rectangle(0, vpY, Width, vpH);

            using var vpBrush = new SolidBrush(ViewportColor);
            g.FillRectangle(vpBrush, _viewportRect);
            using var vpPen = new Pen(ViewportBorderColor, 1);
            g.DrawRectangle(vpPen, _viewportRect.X, _viewportRect.Y, _viewportRect.Width - 1, _viewportRect.Height - 1);
        }

        private static Dictionary<int, Color> BuildColorMap(IReadOnlyList<TokenInfo>? tokens, int textLength, Color defaultColor)
        {
            var map = new Dictionary<int, Color>();
            if (tokens == null) return map;

            var tokenColors = new Dictionary<SyntaxTokenType, Color>
            {
                [SyntaxTokenType.Keyword] = Color.FromArgb(86, 156, 214),
                [SyntaxTokenType.String] = Color.FromArgb(214, 157, 133),
                [SyntaxTokenType.Comment] = Color.FromArgb(106, 153, 85),
                [SyntaxTokenType.Number] = Color.FromArgb(181, 206, 168),
                [SyntaxTokenType.Preprocessor] = Color.FromArgb(128, 128, 128),
                [SyntaxTokenType.Identifier] = defaultColor,
            };

            foreach (var token in tokens)
            {
                if (!tokenColors.TryGetValue(token.Type, out var tc))
                    tc = defaultColor;
                for (int i = token.StartIndex; i < token.StartIndex + token.Length && i < textLength; i++)
                    map[i] = tc;
            }
            return map;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_attachedEditor == null || !_attachedEditor.IsHandleCreated) return;
            if (e.Button != MouseButtons.Left) return;

            if (_viewportRect.Contains(e.Location))
            {
                _isDragging = true;
                Capture = true;
            }
            else
            {
                int targetLine = (int)(e.Y / _lineScale);
                targetLine = Math.Max(0, Math.Min(_totalLines - 1, targetLine));
                // Center the clicked line in the viewport
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pollTimer?.Stop();
                _pollTimer?.Dispose();
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
        Identifier, Keyword, String, Comment, Number, Operator, Preprocessor
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
