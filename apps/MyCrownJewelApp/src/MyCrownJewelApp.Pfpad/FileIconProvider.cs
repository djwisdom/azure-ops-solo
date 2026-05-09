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
        // Language file types with distinctive colors
        _extensionMap[".cs"] = (Color.FromArgb(0x9B, 0x4F, 0x96), "C#");      // Purple
        _extensionMap[".csx"] = (Color.FromArgb(0x9B, 0x4F, 0x96), "C#");
        _extensionMap[".vb"] = (Color.FromArgb(0x00, 0x64, 0xBA), "VB");      // Blue
        _extensionMap[".fs"] = (Color.FromArgb(0x37, 0x8B, 0xBA), "F#");      // Teal
        _extensionMap[".py"] = (Color.FromArgb(0x35, 0x7A, 0xB7), "PY");      // Python blue
        _extensionMap[".js"] = (Color.FromArgb(0xF7, 0xDF, 0x1E), "JS");      // JS yellow
        _extensionMap[".jsx"] = (Color.FromArgb(0x61, 0xDA, 0xFB), "JSX");    // React cyan
        _extensionMap[".ts"] = (Color.FromArgb(0x31, 0x78, 0xC6), "TS");      // TypeScript blue
        _extensionMap[".tsx"] = (Color.FromArgb(0x31, 0x78, 0xC6), "TSX");
        _extensionMap[".html"] = (Color.FromArgb(0xE4, 0x4D, 0x26), "HT");    // HTML orange
        _extensionMap[".css"] = (Color.FromArgb(0x26, 0x3C, 0xED), "CS");     // CSS blue
        _extensionMap[".scss"] = (Color.FromArgb(0xCD, 0x67, 0x99), "SC");    // Sass pink
        _extensionMap[".less"] = (Color.FromArgb(0x1D, 0x36, 0x5F), "LE");    // Less dark
        _extensionMap[".json"] = (Color.FromArgb(0x5B, 0x5B, 0x5B), "{}");    // JSON gray
        _extensionMap[".xml"] = (Color.FromArgb(0x00, 0x6E, 0x8A), "XM");     // XML teal
        _extensionMap[".yaml"] = (Color.FromArgb(0x6B, 0x5B, 0x3E), "YM");    // YAML brown
        _extensionMap[".yml"] = (Color.FromArgb(0x6B, 0x5B, 0x3E), "YM");
        _extensionMap[".md"] = (Color.FromArgb(0x08, 0x3E, 0x76), "MD");      // Markdown dark blue
        _extensionMap[".sql"] = (Color.FromArgb(0xE3, 0x8D, 0x00), "SQ");     // SQL amber
        _extensionMap[".sh"] = (Color.FromArgb(0x4E, 0xAA, 0x25), "SH");      // Shell green
        _extensionMap[".bash"] = (Color.FromArgb(0x4E, 0xAA, 0x25), "SH");
        _extensionMap[".ps1"] = (Color.FromArgb(0x01, 0x27, 0xAC), "PS");      // PowerShell blue
        _extensionMap[".bat"] = (Color.FromArgb(0x4D, 0x4D, 0x4D), "BT");     // Batch gray
        _extensionMap[".cmd"] = (Color.FromArgb(0x4D, 0x4D, 0x4D), "CM");
        _extensionMap[".java"] = (Color.FromArgb(0xB0, 0x72, 0x19), "JV");    // Java orange
        _extensionMap[".kt"] = (Color.FromArgb(0x7F, 0x52, 0xFF), "KT");      // Kotlin purple
        _extensionMap[".go"] = (Color.FromArgb(0x00, 0xAC, 0xD8), "GO");      // Go light blue
        _extensionMap[".rs"] = (Color.FromArgb(0xDE, 0xA5, 0x84), "RS");      // Rust tan
        _extensionMap[".c"] = (Color.FromArgb(0x28, 0x3B, 0xD7), "C");       // C blue
        _extensionMap[".h"] = (Color.FromArgb(0x28, 0x3B, 0xD7), "H");
        _extensionMap[".cpp"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");     // C++ dark blue
        _extensionMap[".cxx"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".cc"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".hpp"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".hxx"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".hh"] = (Color.FromArgb(0x00, 0x52, 0x8F), "C+");
        _extensionMap[".swift"] = (Color.FromArgb(0xF0, 0x51, 0x38), "SW");   // Swift orange
        _extensionMap[".rb"] = (Color.FromArgb(0xCC, 0x34, 0x2D), "RB");      // Ruby red
        _extensionMap[".php"] = (Color.FromArgb(0x77, 0x7B, 0xB4), "PH");     // PHP purple
        _extensionMap[".pl"] = (Color.FromArgb(0x02, 0x9E, 0xD1), "PL");      // Perl blue
        _extensionMap[".lua"] = (Color.FromArgb(0x00, 0x00, 0x7C), "LU");     // Lua dark blue
        _extensionMap[".r"] = (Color.FromArgb(0x19, 0x8C, 0xE7), "R");       // R blue
        _extensionMap[".dart"] = (Color.FromArgb(0x01, 0x75, 0xC0), "DA");    // Dart blue
        _extensionMap[".tf"] = (Color.FromArgb(0x84, 0x4F, 0xBA), "TF");      // Terraform purple
        _extensionMap[".bicep"] = (Color.FromArgb(0x00, 0x74, 0xCB), "BP");   // Bicep blue
        _extensionMap[".dockerfile"] = (Color.FromArgb(0x0D, 0xB7, 0xED), "DK"); // Docker cyan
        _extensionMap[".editorconfig"] = (Color.FromArgb(0x8C, 0x8C, 0x8C), "EC"); // Gray
        _extensionMap[".gitignore"] = (Color.FromArgb(0xE4, 0x4D, 0x26), "GI"); // Git orange
        _extensionMap[".sln"] = (Color.FromArgb(0x68, 0x4D, 0x95), "SL");     // Solution purple
        _extensionMap[".csproj"] = (Color.FromArgb(0x9B, 0x4F, 0x96), "CS");  // C# project purple
        _extensionMap[".txt"] = (Color.Gray, "TXT");                         // Text
        _extensionMap[".gitignore"] = (Color.FromArgb(0xE4, 0x4D, 0x26), "DOT"); // Dotfile
        _extensionMap[".editorconfig"] = (Color.FromArgb(0x8C, 0x8C, 0x8C), "DOT");
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
        string ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext))
        {
            // Check for exact filename match (e.g. Dockerfile, .gitignore)
            string name = Path.GetFileName(filePath);
            if (_extensionIndex.TryGetValue(name, out int idx)) return idx;
            ext = name; // fallback: try full name
        }
        if (_extensionIndex.TryGetValue(ext, out int ix)) return ix;
        return _defaultIndex;
    }

    public static (Color Color, string Label) GetFileInfo(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return (Color.Gray, "");
        string ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext))
        {
            string name = Path.GetFileName(filePath);
            if (_extensionMap.TryGetValue(name, out var info)) return info;
            return (Color.Gray, "FL");
        }
        if (_extensionMap.TryGetValue(ext, out var val)) return val;
        return (Color.Gray, "FL");
    }

    private static Bitmap CreateIcon(string label)
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color c = Color.FromArgb(130, 130, 130);

        switch (label)
        {
            case "C":
                DrawBigChar(g, c, 'C');
                break;
            case "C+":
            case "CP":
            case "HP":
                DrawCpp(g, c);
                break;
            case "DOT":
            case "TXT":
                DrawLines(g, c);
                break;
            default:
                DrawLabel(g, c, label);
                break;
        }
        return bmp;
    }

    private static void DrawBigChar(Graphics g, Color color, char ch)
    {
        using var font = new Font("Segoe UI", 10, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var path = new GraphicsPath();
        path.AddString(ch.ToString(), font.FontFamily, (int)FontStyle.Bold, 12, new Point(8, 8), sf);
        using var pen = new Pen(color, 1.2f);
        g.DrawPath(pen, path);
    }

    private static void DrawCpp(Graphics g, Color color)
    {
        using var font = new Font("Segoe UI", 5.5f, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var path = new GraphicsPath();
        path.AddString("c++", font.FontFamily, (int)FontStyle.Bold, 7, new Point(8, 8), sf);
        using var pen = new Pen(color, 1);
        g.DrawPath(pen, path);
    }

    private static void DrawLines(Graphics g, Color color)
    {
        // Four horizontal jagged/zigzag lines representing text content
        var pts = new Point[5];
        int cx = 8, cy = 8;
        using var pen = new Pen(color, 1);

        for (int i = 0; i < 4; i++)
        {
            int y = cy - 5 + i * 3;
            int x0 = cx - 5;
            int x1 = cx + 5;
            // Simple zigzag: short line segment
            g.DrawLine(pen, x0, y, x0 + 4, y - 1);
            g.DrawLine(pen, x0 + 4, y - 1, x0 + 8, y);
            g.DrawLine(pen, x0 + 8, y, x1, y - 1);
        }
    }

    private static void DrawLabel(Graphics g, Color color, string text)
    {
        using var font = new Font("Segoe UI", 6, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var path = new GraphicsPath();
        path.AddString(text, font.FontFamily, (int)FontStyle.Bold, 7, new Point(8, 8), sf);
        using var pen = new Pen(color, 1);
        g.DrawPath(pen, path);
    }

    private static Bitmap CreateDefaultIcon()
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color c = Color.FromArgb(130, 130, 130);
        DrawLines(g, c);
        return bmp;
    }

    private static Bitmap CreateFolderIcon()
    {
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color c = Color.FromArgb(130, 130, 130);
        using var pen = new Pen(c, 1.2f);

        // Folder outline without fill: open folder shape (tab on left, angled right side)
        g.DrawLine(pen, 1, 4, 5, 4);       // top tab
        g.DrawLine(pen, 5, 4, 7, 7);       // tab angle
        g.DrawLine(pen, 7, 7, 15, 7);      // top edge
        g.DrawLine(pen, 15, 7, 15, 13);    // right edge
        g.DrawLine(pen, 15, 13, 1, 13);    // bottom edge
        g.DrawLine(pen, 1, 13, 1, 4);      // left edge

        return bmp;
    }
}