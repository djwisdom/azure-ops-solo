using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace MyCrownJewelApp.Pfpad;

public static class FileIconProvider
{
    private static readonly Dictionary<string, (Color Color, string Label)> _extensionMap = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> _extensionIndex = new(StringComparer.OrdinalIgnoreCase);
    private static ImageList? _imageList;
    private static int _defaultIndex = -1;

    static FileIconProvider()
    {
        _extensionMap[".cs"] = (Color.FromArgb(0x9B, 0x4F, 0x96), "CS#");
        _extensionMap[".csx"] = (Color.FromArgb(0x9B, 0x4F, 0x96), "CS#");
        _extensionMap[".vb"] = (Color.FromArgb(0x00, 0x64, 0xBA), "VB");
        _extensionMap[".fs"] = (Color.FromArgb(0x37, 0x8B, 0xBA), "F#");
        _extensionMap[".py"] = (Color.FromArgb(0x35, 0x7A, 0xB7), "PY");
        _extensionMap[".js"] = (Color.FromArgb(0xF7, 0xDF, 0x1E), "JS");
        _extensionMap[".jsx"] = (Color.FromArgb(0x61, 0xDA, 0xFB), "JSX");
        _extensionMap[".ts"] = (Color.FromArgb(0x31, 0x78, 0xC6), "TS");
        _extensionMap[".tsx"] = (Color.FromArgb(0x31, 0x78, 0xC6), "TSX");
        _extensionMap[".html"] = (Color.FromArgb(0xE4, 0x4D, 0x26), "HT");
        _extensionMap[".css"] = (Color.FromArgb(0x26, 0x3C, 0xED), "CS");
        _extensionMap[".scss"] = (Color.FromArgb(0xCD, 0x67, 0x99), "SC");
        _extensionMap[".less"] = (Color.FromArgb(0x1D, 0x36, 0x5F), "LE");
        _extensionMap[".json"] = (Color.FromArgb(0x5B, 0x5B, 0x5B), "{}");
        _extensionMap[".xml"] = (Color.FromArgb(0x00, 0x6E, 0x8A), "XM");
        _extensionMap[".yaml"] = (Color.FromArgb(0x6B, 0x5B, 0x3E), "YM");
        _extensionMap[".yml"] = (Color.FromArgb(0x6B, 0x5B, 0x3E), "YM");
        _extensionMap[".md"] = (Color.FromArgb(0x08, 0x3E, 0x76), "MD");
        _extensionMap[".sql"] = (Color.FromArgb(0xE3, 0x8D, 0x00), "SQ");
        _extensionMap[".sh"] = (Color.FromArgb(0x4E, 0xAA, 0x25), "$");
        _extensionMap[".bash"] = (Color.FromArgb(0x4E, 0xAA, 0x25), "$");
        _extensionMap[".zsh"] = (Color.FromArgb(0x4E, 0xAA, 0x25), "$");
        _extensionMap[".ps1"] = (Color.FromArgb(0x01, 0x27, 0xAC), "PS");
        _extensionMap[".bat"] = (Color.FromArgb(0x4D, 0x4D, 0x4D), "BT");
        _extensionMap[".cmd"] = (Color.FromArgb(0x4D, 0x4D, 0x4D), "CM");
        _extensionMap[".java"] = (Color.FromArgb(0xB0, 0x72, 0x19), "JV");
        _extensionMap[".kt"] = (Color.FromArgb(0x7F, 0x52, 0xFF), "KT");
        _extensionMap[".go"] = (Color.FromArgb(0x00, 0xAC, 0xD8), "GO");
        _extensionMap[".rs"] = (Color.FromArgb(0xDE, 0xA5, 0x84), "RS");
        _extensionMap[".c"] = (Color.FromArgb(0x28, 0x3B, 0xD7), "C");
        _extensionMap[".h"] = (Color.FromArgb(0x28, 0x3B, 0xD7), "H");
        _extensionMap[".cpp"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".cxx"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".cc"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".hpp"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".hxx"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".hh"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".swift"] = (Color.FromArgb(0xF0, 0x51, 0x38), "SW");
        _extensionMap[".rb"] = (Color.FromArgb(0xCC, 0x34, 0x2D), "RB");
        _extensionMap[".php"] = (Color.FromArgb(0x77, 0x7B, 0xB4), "PH");
        _extensionMap[".pl"] = (Color.FromArgb(0x02, 0x9E, 0xD1), "PL");
        _extensionMap[".lua"] = (Color.FromArgb(0x00, 0x00, 0x7C), "LU");
        _extensionMap[".r"] = (Color.FromArgb(0x19, 0x8C, 0xE7), "R");
        _extensionMap[".dart"] = (Color.FromArgb(0x01, 0x75, 0xC0), "DA");
        _extensionMap[".tf"] = (Color.FromArgb(0x84, 0x4F, 0xBA), "TF");
        _extensionMap[".bicep"] = (Color.FromArgb(0x00, 0x74, 0xCB), "BP");
        _extensionMap[".dockerfile"] = (Color.FromArgb(0x0D, 0xB7, 0xED), "DK");
        _extensionMap[".tmp"] = (Color.FromArgb(0x8C, 0x8C, 0x8C), "TMP");
        _extensionMap[".sln"] = (Color.FromArgb(0x68, 0x4D, 0x95), "SL");
        _extensionMap[".csproj"] = (Color.FromArgb(0x9B, 0x4F, 0x96), "CS");
        _extensionMap[".txt"] = (Color.Gray, "TXT");

        // Filename-based entries (must be exact match)
        _extensionMap["Makefile"] = (Color.FromArgb(0xE4, 0x4D, 0x26), "MK");
        _extensionMap["makefile"] = (Color.FromArgb(0xE4, 0x4D, 0x26), "MK");
        _extensionMap["README"] = (Color.FromArgb(0x08, 0x3E, 0x76), "READ");
        _extensionMap["README.md"] = (Color.FromArgb(0x08, 0x3E, 0x76), "READ");
        _extensionMap["README.txt"] = (Color.FromArgb(0x08, 0x3E, 0x76), "READ");
        _extensionMap["LICENSE"] = (Color.FromArgb(0x6B, 0x5B, 0x3E), "KEY");
        _extensionMap["LICENSE.txt"] = (Color.FromArgb(0x6B, 0x5B, 0x3E), "KEY");
        _extensionMap[".editorconfig"] = (Color.FromArgb(0x8C, 0x8C, 0x8C), "DOT");
        _extensionMap[".gitignore"] = (Color.FromArgb(0xE4, 0x4D, 0x26), "DOT");
        _extensionMap[".gitattributes"] = (Color.Gray, "DOT");

        BuildImageList();
    }

    private static void BuildImageList()
    {
        _imageList = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        int index = 0;
        foreach (var kvp in _extensionMap)
        {
            _extensionIndex[kvp.Key] = index;
            _imageList.Images.Add(CreateIcon(kvp.Value.Label));
            index++;
        }
        _defaultIndex = index;
        _imageList.Images.Add(CreateDefaultIcon());
        index++;
        FolderIconIndex = index;
        _imageList.Images.Add(CreateFolderIcon());
    }

    public static ImageList ImageList => _imageList!;
    public static int FolderIconIndex { get; private set; } = -1;
    public static int DefaultFileIconIndex => _defaultIndex;

    public static int GetIconIndex(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return _defaultIndex;
        string name = Path.GetFileName(filePath);
        if (_extensionIndex.TryGetValue(name, out int idx)) return idx;
        string ext = Path.GetExtension(filePath);
        if (_extensionIndex.TryGetValue(ext, out int ix)) return ix;
        return _defaultIndex;
    }

    private static Bitmap CreateIcon(string label)
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color c = Color.FromArgb(130, 130, 130);

        switch (label)
        {
            case "C":     DrawBigChar(g, c, 'C'); break;
            case "C+":    DrawCpp(g, c); break;
            case "CS#":   DrawCSharp(g, c); break;
            case "DOT":
            case "TXT":   DrawLines(g, c); break;
            case "MD":    DrawDownArrow(g, c); break;
            case "$":     DrawDollar(g, c); break;
            case "{}":    DrawBraces(g, c); break;
            case "MK":    DrawBigChar(g, c, 'M'); break;
            case "READ":  DrawInfo(g, c); break;
            case "KEY":   DrawKey(g, c); break;
            case "TMP":   DrawClock(g, c); break;
            default:      DrawLabel(g, c, label); break;
        }
        return bmp;
    }

    // ─── Drawing helpers ───

    private static void DrawBigChar(Graphics g, Color color, char ch)
    {
        using var f = new Font("Segoe UI", 10, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var p = new GraphicsPath();
        p.AddString(ch.ToString(), f.FontFamily, (int)FontStyle.Bold, 12, new Point(8, 8), sf);
        using var pen = new Pen(color, 1.2f);
        g.DrawPath(pen, p);
    }

    private static void DrawCpp(Graphics g, Color color)
    {
        using var f = new Font("Segoe UI", 5.5f, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var p = new GraphicsPath();
        p.AddString("c++", f.FontFamily, (int)FontStyle.Bold, 7, new Point(8, 8), sf);
        using var pen = new Pen(color, 1);
        g.DrawPath(pen, p);
    }

    private static void DrawCSharp(Graphics g, Color color)
    {
        using var f = new Font("Segoe UI", 5.5f, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var p = new GraphicsPath();
        p.AddString("C#", f.FontFamily, (int)FontStyle.Bold, 7, new Point(8, 8), sf);
        using var pen = new Pen(color, 1);
        g.DrawPath(pen, p);
    }

    private static void DrawLines(Graphics g, Color color)
    {
        using var pen = new Pen(color, 1);
        g.DrawLine(pen, 2, 1, 14, 1);
        g.DrawLine(pen, 14, 1, 14, 15);
        g.DrawLine(pen, 14, 15, 2, 15);
        g.DrawLine(pen, 2, 15, 2, 1);
        for (int i = 0; i < 3; i++)
        {
            int y = 4 + i * 4;
            g.DrawLine(pen, 5, y, 12, y);
        }
    }

    private static void DrawDownArrow(Graphics g, Color color)
    {
        // Thick downward arrow
        using var pen = new Pen(color, 2.5f);
        int cx = 8, cy = 5;
        g.DrawLine(pen, cx, cy, cx, cy + 5);
        g.DrawLine(pen, cx - 4, cy + 2, cx, cy + 6);
        g.DrawLine(pen, cx + 4, cy + 2, cx, cy + 6);
    }

    private static void DrawDollar(Graphics g, Color color)
    {
        using var f = new Font("Segoe UI", 9, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var p = new GraphicsPath();
        p.AddString("$", f.FontFamily, (int)FontStyle.Bold, 10, new Point(8, 8), sf);
        using var pen = new Pen(color, 1.2f);
        g.DrawPath(pen, p);
    }

    private static void DrawBraces(Graphics g, Color color)
    {
        using var f = new Font("Segoe UI", 7, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var p = new GraphicsPath();
        p.AddString("{}", f.FontFamily, (int)FontStyle.Bold, 8, new Point(8, 8), sf);
        using var pen = new Pen(color, 1.2f);
        g.DrawPath(pen, p);
    }

    private static void DrawInfo(Graphics g, Color color)
    {
        // Circle with "i" inside
        using var pen = new Pen(color, 1.2f);
        g.DrawEllipse(pen, 2, 1, 12, 12);
        // dot
        g.DrawEllipse(pen, 7, 3, 2, 2);
        // stem
        g.DrawLine(pen, 8, 7, 8, 12);
    }

    private static void DrawKey(Graphics g, Color color)
    {
        // Simple key outline: circle head + line with teeth
        using var pen = new Pen(color, 1.2f);
        g.DrawEllipse(pen, 1, 2, 6, 6);
        g.DrawLine(pen, 7, 5, 14, 5);
        g.DrawLine(pen, 14, 5, 14, 8);
        g.DrawLine(pen, 11, 5, 11, 8);
        g.DrawLine(pen, 8, 5, 8, 8);
    }

    private static void DrawClock(Graphics g, Color color)
    {
        // Clock face with hands
        using var pen = new Pen(color, 1.2f);
        g.DrawEllipse(pen, 1, 1, 14, 14);
        using var thin = new Pen(color, 1);
        // center dot
        g.DrawEllipse(thin, 7, 7, 2, 2);
        // hour hand (pointing to 10 o'clock)
        g.DrawLine(thin, 8, 8, 5, 4);
        // minute hand (pointing to 2 o'clock)
        g.DrawLine(thin, 8, 8, 11, 3);
    }

    private static void DrawLabel(Graphics g, Color color, string text)
    {
        using var f = new Font("Segoe UI", 6, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var p = new GraphicsPath();
        p.AddString(text, f.FontFamily, (int)FontStyle.Bold, 7, new Point(8, 8), sf);
        using var pen = new Pen(color, 1);
        g.DrawPath(pen, p);
    }

    private static Bitmap CreateDefaultIcon()
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        DrawLines(g, Color.FromArgb(130, 130, 130));
        return bmp;
    }

    private static Bitmap CreateFolderIcon()
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color c = Color.FromArgb(130, 130, 130);
        using var pen = new Pen(c, 1.2f);
        g.DrawLine(pen, 1, 4, 5, 4);
        g.DrawLine(pen, 5, 4, 7, 7);
        g.DrawLine(pen, 7, 7, 15, 7);
        g.DrawLine(pen, 15, 7, 15, 13);
        g.DrawLine(pen, 15, 13, 1, 13);
        g.DrawLine(pen, 1, 13, 1, 4);
        return bmp;
    }
}