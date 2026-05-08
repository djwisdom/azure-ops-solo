using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class MultiCaretManager
{
    private readonly HighlightRichTextBox _editor;
    private List<int> _carets = new();
    private bool _active;
    private int _lastTextLength;
    private int _lastSelectionStart;
    private string? _pendingInsertText;

    public bool Active => _active;
    public IReadOnlyList<int> Carets => _carets;
    public int TotalCaretCount => _active ? _carets.Count + 1 : 1;

    public bool IsMultiCaret => _active && _carets.Count > 0;

    public MultiCaretManager(HighlightRichTextBox editor)
    {
        _editor = editor;
    }

    public void AddCaret(Point location)
    {
        int pos = _editor.GetCharIndexFromPosition(location);
        if (pos < 0) return;

        if (!_active)
        {
            _carets.Clear();
            _active = true;
        }
        _carets.Add(pos);
        _editor.Invalidate();
    }

    public void AddCaretAbove()
    {
        int line = _editor.GetLineFromCharIndex(_editor.SelectionStart);
        if (line <= 0) return;
        int newLine = line - 1;
        int col = _editor.SelectionStart - _editor.GetFirstCharIndexFromLine(line);
        int newPos = Math.Min(
            _editor.GetFirstCharIndexFromLine(newLine) + col,
            _editor.TextLength - 1);
        AddCaretAtPosition(newPos);
    }

    public void AddCaretBelow()
    {
        int line = _editor.GetLineFromCharIndex(_editor.SelectionStart);
        int totalLines = _editor.Lines.Length;
        if (line >= totalLines - 1) return;
        int newLine = line + 1;
        int col = _editor.SelectionStart - _editor.GetFirstCharIndexFromLine(line);
        int newPos = Math.Min(
            _editor.GetFirstCharIndexFromLine(newLine) + col,
            _editor.TextLength - 1);
        AddCaretAtPosition(newPos);
    }

    public void AddCaretAtPosition(int pos)
    {
        if (!_active)
        {
            _carets.Clear();
            _active = true;
        }
        _carets.Add(pos);
        _editor.Invalidate();
    }

    public void Clear()
    {
        _carets.Clear();
        _active = false;
        _pendingInsertText = null;
        _editor.Invalidate();
    }

    public void OnBeforeKeyPress()
    {
        _lastTextLength = _editor.TextLength;
        _lastSelectionStart = _editor.SelectionStart;
    }

    public void OnTextChanged()
    {
        if (!_active || _carets.Count == 0) return;

        int delta = _editor.TextLength - _lastTextLength;
        if (delta <= 0) return;

        // Determine what text was inserted at the primary caret
        string inserted = _editor.Text.Substring(_lastSelectionStart, delta);
        if (string.IsNullOrEmpty(inserted)) return;

        // Adjust saved caret positions for the text insertion at primary position
        for (int i = 0; i < _carets.Count; i++)
        {
            if (_carets[i] >= _lastSelectionStart)
                _carets[i] += delta;
        }

        // Insert the same text at each additional caret position
        // Sort descending so insertions don't shift other positions
        var sorted = _carets.OrderByDescending(c => c).ToList();
        foreach (int pos in sorted)
        {
            if (pos < 0 || pos > _editor.TextLength) continue;
            _editor.Select(pos, 0);
            _editor.SelectedText = inserted;
        }

        // Restore primary caret position
        _editor.Select(_lastSelectionStart + delta, 0);
        _editor.Invalidate();
    }

    public void DrawAdditionalCarets(Graphics g)
    {
        if (!_active || _carets.Count == 0) return;

        int currentPrimary = _editor.SelectionStart;
        using var caretBrush = new SolidBrush(_editor.ForeColor);

        foreach (int pos in _carets)
        {
            if (pos == currentPrimary) continue;
            if (pos < 0 || pos >= _editor.TextLength) continue;

            Point pt = _editor.GetPositionFromCharIndex(pos);
            if (pt.IsEmpty) continue;

            int lineH = Math.Max(1, (int)Math.Ceiling(_editor.Font.GetHeight() * _editor.ZoomFactor));
            int charW = Math.Max(1, (int)(8 * _editor.ZoomFactor));

            g.FillRectangle(caretBrush, pt.X, pt.Y, charW, lineH);
        }
    }

    public bool HandleKeyDown(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        bool alt = (keyData & Keys.Alt) != 0;
        bool shift = (keyData & Keys.Shift) != 0;
        bool ctrl = (keyData & Keys.Control) != 0;

        // Escape: clear multi-caret
        if (key == Keys.Escape && _active)
        {
            Clear();
            return true;
        }

        // Alt+Shift+Up: add caret above
        if (alt && shift && key == Keys.Up)
        {
            AddCaretAbove();
            return true;
        }

        // Alt+Shift+Down: add caret below
        if (alt && shift && key == Keys.Down)
        {
            AddCaretBelow();
            return true;
        }

        // Alt+Click is handled in MouseDown

        // For arrow keys when multi-caret is active, move all carets
        if (_active && _carets.Count > 0 && (key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right))
        {
            // Move primary caret
            // Additional carets will be adjusted on next text operation
            for (int i = 0; i < _carets.Count; i++)
            {
                _carets[i] = SimulateMoveCaret(_carets[i], key);
            }
            return false; // Let the editor handle primary caret movement
        }

        return false;
    }

    private int SimulateMoveCaret(int pos, Keys key)
    {
        int line = _editor.GetLineFromCharIndex(pos);
        switch (key)
        {
            case Keys.Left: return Math.Max(0, pos - 1);
            case Keys.Right: return Math.Min(_editor.TextLength, pos + 1);
            case Keys.Up:
                if (line <= 0) return pos;
                {
                    int col = pos - _editor.GetFirstCharIndexFromLine(line);
                    int newLineStart = _editor.GetFirstCharIndexFromLine(line - 1);
                    int newLineEnd = line - 1 + 1 < _editor.Lines.Length
                        ? _editor.GetFirstCharIndexFromLine(line) - 1
                        : _editor.TextLength;
                    return Math.Min(newLineStart + col, newLineEnd);
                }
            case Keys.Down:
                if (line >= _editor.Lines.Length - 1) return pos;
                {
                    int col = pos - _editor.GetFirstCharIndexFromLine(line);
                    int newLineStart = _editor.GetFirstCharIndexFromLine(line + 1);
                    int newLineEnd = line + 2 < _editor.Lines.Length
                        ? _editor.GetFirstCharIndexFromLine(line + 2) - 1
                        : _editor.TextLength;
                    return Math.Min(newLineStart + col, newLineEnd);
                }
            default: return pos;
        }
    }
}
