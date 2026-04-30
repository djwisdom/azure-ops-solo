using System.Text;

namespace TextEditor;

internal static class Program
{
    private static string? _currentFilePath;
    private static List<string> _lines = new() { "" };
    private static int _cursorRow = 0;
    private static int _cursorCol = 0;
    private static int _scrollRow = 0;
    private static bool _modified = false;
    private static string _statusMessage = "";
    private static DateTime _statusTime = DateTime.MinValue;

    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "Text Editor";

        if (args.Length > 0)
        {
            OpenFile(args[0]);
        }

        Run();
    }

    private static void Run()
    {
        Console.CursorVisible = false;
        Render();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (HandleKey(key)) break;
        }
    }

    private static bool HandleKey(ConsoleKeyInfo key)
    {
        // Quit
        if (key.Key == ConsoleKey.Q && key.Modifiers == ConsoleModifiers.Control)
        {
            if (_modified)
            {
                SetStatus("Unsaved changes. Ctrl+Q again to quit, or Ctrl+S to save.");
                return false;
            }
            return true;
        }

        // Save
        if (key.Key == ConsoleKey.S && key.Modifiers == ConsoleModifiers.Control)
        {
            SaveFile();
            return false;
        }

        // Open
        if (key.Key == ConsoleKey.O && key.Modifiers == ConsoleModifiers.Control)
        {
            SetStatus("Open not available in this demo.");
            return false;
        }

        // New file
        if (key.Key == ConsoleKey.N && key.Modifiers == ConsoleModifiers.Control)
        {
            if (_modified)
            {
                SetStatus("Save current file first (Ctrl+S).");
                return false;
            }
            _currentFilePath = null;
            _lines.Clear();
            _lines.Add("");
            _cursorRow = 0;
            _cursorCol = 0;
            _modified = false;
            _scrollRow = 0;
            return false;
        }

        // Navigation
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                MoveCursorLeft();
                break;
            case ConsoleKey.RightArrow:
                MoveCursorRight();
                break;
            case ConsoleKey.UpArrow:
                MoveCursorUp();
                break;
            case ConsoleKey.DownArrow:
                MoveCursorDown();
                break;
            case ConsoleKey.Home:
                _cursorCol = 0;
                break;
            case ConsoleKey.End:
                _cursorCol = _lines[_cursorRow].Length;
                break;
            case ConsoleKey.PageUp:
                _scrollRow = Math.Max(0, _scrollRow - Console.WindowHeight - 2);
                break;
            case ConsoleKey.PageDown:
                _scrollRow = Math.Min(Math.Max(0, _lines.Count - (Console.WindowHeight - 2)), _scrollRow + Console.WindowHeight - 2);
                break;
            case ConsoleKey.Enter:
                InsertNewLine();
                break;
            case ConsoleKey.Backspace:
                DeleteChar();
                break;
            case ConsoleKey.Delete:
                DeleteForward();
                break;
            default:
                if (!char.IsControl(key.KeyChar))
                {
                    InsertChar(key.KeyChar);
                }
                break;
        }

        EnsureCursorVisible();
        Render();
        return false;
    }

    private static void MoveCursorLeft()
    {
        if (_cursorCol > 0)
        {
            _cursorCol--;
        }
        else if (_cursorRow > 0)
        {
            _cursorRow--;
            _cursorCol = _lines[_cursorRow].Length;
        }
    }

    private static void MoveCursorRight()
    {
        if (_cursorCol < _lines[_cursorRow].Length)
        {
            _cursorCol++;
        }
        else if (_cursorRow < _lines.Count - 1)
        {
            _cursorRow++;
            _cursorCol = 0;
        }
    }

    private static void MoveCursorUp()
    {
        if (_cursorRow > 0)
        {
            _cursorRow--;
            _cursorCol = Math.Min(_cursorCol, _lines[_cursorRow].Length);
        }
    }

    private static void MoveCursorDown()
    {
        if (_cursorRow < _lines.Count - 1)
        {
            _cursorRow++;
            _cursorCol = Math.Min(_cursorCol, _lines[_cursorRow].Length);
        }
    }

    private static void InsertChar(char c)
    {
        var line = _lines[_cursorRow];
        _lines[_cursorRow] = line.Insert(_cursorCol, c.ToString());
        _cursorCol++;
        _modified = true;
    }

    private static void InsertNewLine()
    {
        var line = _lines[_cursorRow];
        var before = line[.._cursorCol];
        var after = line[_cursorCol..];
        _lines[_cursorRow] = before;
        _lines.Insert(_cursorRow + 1, after);
        _cursorRow++;
        _cursorCol = 0;
        _modified = true;
    }

    private static void DeleteChar()
    {
        if (_cursorCol > 0)
        {
            var line = _lines[_cursorRow];
            _lines[_cursorRow] = line.Remove(_cursorCol - 1, 1);
            _cursorCol--;
            _modified = true;
        }
        else if (_cursorRow > 0)
        {
            var prevLine = _lines[_cursorRow - 1];
            var currentLine = _lines[_cursorRow];
            _lines[_cursorRow - 1] = prevLine + currentLine;
            _lines.RemoveAt(_cursorRow);
            _cursorRow--;
            _cursorCol = prevLine.Length;
            _modified = true;
        }
    }

    private static void DeleteForward()
    {
        var line = _lines[_cursorRow];
        if (_cursorCol < line.Length)
        {
            _lines[_cursorRow] = line.Remove(_cursorCol, 1);
            _modified = true;
        }
        else if (_cursorRow < _lines.Count - 1)
        {
            var nextLine = _lines[_cursorRow + 1];
            _lines[_cursorRow] += nextLine;
            _lines.RemoveAt(_cursorRow + 1);
            _modified = true;
        }
    }

    private static void OpenFile(string path)
    {
        try
        {
            _lines = File.ReadAllLines(path).ToList();
            if (_lines.Count == 0) _lines.Add("");
            _currentFilePath = path;
            _cursorRow = 0;
            _cursorCol = 0;
            _modified = false;
            SetStatus($"Opened: {path}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private static void SaveFile()
    {
        if (_currentFilePath == null)
        {
            SetStatus("Use 'Save As' - not implemented in demo.");
            return;
        }

        try
        {
            File.WriteAllLines(_currentFilePath, _lines);
            _modified = false;
            SetStatus($"Saved: {_currentFilePath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private static void EnsureCursorVisible()
    {
        var windowHeight = Console.WindowHeight - 2;
        if (_cursorRow < _scrollRow)
        {
            _scrollRow = _cursorRow;
        }
        else if (_cursorRow >= _scrollRow + windowHeight)
        {
            _scrollRow = _cursorRow - windowHeight + 1;
        }
    }

    private static void Render()
    {
        Console.Clear();

        var windowWidth = Console.WindowWidth;
        var windowHeight = Console.WindowHeight - 1;

        // Calculate visible range
        var visibleStart = _scrollRow;
        var visibleEnd = Math.Min(_scrollRow + windowHeight - 1, _lines.Count - 1);

        // Render lines
        for (int i = visibleStart; i <= visibleEnd; i++)
        {
            var line = _lines[i];
            var displayRow = i - visibleStart + 1;

            if (i == _cursorRow)
            {
                // Render cursor line
                var beforeCursor = line[.._cursorCol];
                var cursorChar = _cursorCol < line.Length ? line[_cursorCol].ToString() : " ";
                var afterCursor = _cursorCol < line.Length ? line[(_cursorCol + 1)..] : "";

                var beforeDisplay = beforeCursor.Length >= windowWidth - 2
                    ? beforeCursor[^(windowWidth - 2)..]
                    : beforeCursor;
                var full = beforeDisplay + cursorChar + afterCursor;

                if (full.Length > windowWidth - 2)
                {
                    full = full[..(windowWidth - 2)];
                }

                Console.SetCursorPosition(0, displayRow);
                Console.Write(full.PadRight(windowWidth - 2));

                // Position cursor
                var cursorX = Math.Min(_cursorCol - (beforeDisplay.Length - beforeDisplay.Length), windowWidth - 2);
                Console.SetCursorPosition(cursorX, displayRow);
            }
            else
            {
                var display = line.Length > windowWidth - 2 ? line[..(windowWidth - 2)] : line;
                Console.SetCursorPosition(0, displayRow);
                Console.Write(display.PadRight(windowWidth - 2));
            }
        }

        // Clear any remaining lines
        for (int r = visibleEnd - visibleStart + 2; r < windowHeight; r++)
        {
            Console.SetCursorPosition(0, r);
            Console.Write(new string(' ', windowWidth - 2));
        }

        // Status bar
        var statusLine = windowHeight;
        Console.SetCursorPosition(0, statusLine);

        var filename = _currentFilePath ?? "Untitled";
        if (_modified) filename += " *";

        var lineInfo = $"Line {_cursorRow + 1}, Col {_cursorCol + 1}";
        var left = filename.Length > windowWidth / 2 ? filename[..(windowWidth / 2)] : filename;
        var right = lineInfo;

        Console.Write(left.PadRight(windowWidth / 2));
        Console.ForegroundColor = ConsoleColor.Black;
        Console.BackgroundColor = ConsoleColor.Gray;
        Console.Write(right.PadLeft(windowWidth - left.Length));
        Console.ResetColor();

        // Message area (top)
        if (DateTime.Now - _statusTime < TimeSpan.FromSeconds(2))
        {
            Console.SetCursorPosition(0, 0);
            Console.Write(_statusMessage.PadRight(windowWidth - 2));
        }

        // Restore cursor
        Console.SetCursorPosition(
            Math.Min(Math.Max(0, _cursorCol - (_lines[_cursorRow].Length > windowWidth - 2 ? _lines[_cursorRow].Length - (windowWidth - 2) : 0)), windowWidth - 2),
            Math.Min(_cursorRow - _scrollRow + 1, windowHeight));
    }

    private static void SetStatus(string message)
    {
        _statusMessage = message;
        _statusTime = DateTime.Now;
        Console.SetCursorPosition(0, 0);
        Console.Write(message.PadRight(Console.WindowWidth - 2));
    }
}
