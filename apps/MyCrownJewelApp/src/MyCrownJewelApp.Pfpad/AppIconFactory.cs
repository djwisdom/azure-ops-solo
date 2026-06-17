using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>Generates the Pfpad "fp" monogram tile icon at runtime.</summary>
internal static class AppIconFactory
{
    private static Icon? _cachedIcon;
    private static Bitmap? _cachedBitmap32;

    /// <summary>Returns a cached multi-size (16/32/48 px) Icon for use as the window icon.</summary>
    public static Icon GetIcon() => _cachedIcon ??= BuildMultiSizeIcon();

    /// <summary>Returns a fresh Bitmap of the icon at the requested pixel size.</summary>
    public static Bitmap CreateBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        // Rounded tile background — deep blue
        var tileColor = Color.FromArgb(0x26, 0x8B, 0xD2);
        int radius = Math.Max(2, size / 6);
        using var tileBrush = new SolidBrush(tileColor);
        FillRoundedRect(g, tileBrush, new Rectangle(0, 0, size, size), radius);

        // "fp" monogram centred — slightly offset upward for optical balance
        float fontSize = size * 0.40f;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None
        };
        var textRect = new RectangleF(0, size * 0.03f, size, size);
        g.DrawString("fp", font, Brushes.White, textRect, sf);

        return bmp;
    }

    /// <summary>Returns a cached 32×32 Bitmap (no dispose — owned by factory).</summary>
    public static Bitmap GetBitmap32() => _cachedBitmap32 ??= CreateBitmap(32);

    private static void FillRoundedRect(Graphics g, Brush brush, Rectangle rect, int radius)
    {
        using var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    private static Icon BuildMultiSizeIcon()
    {
        int[] sizes = [16, 32, 48];
        var pngs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            using var bmp = CreateBitmap(sizes[i]);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            pngs[i] = ms.ToArray();
        }
        return BuildIconFromPngs(sizes, pngs);
    }

    private static Icon BuildIconFromPngs(int[] sizes, byte[][] pngs)
    {
        // Hand-write a valid .ico container: ICONDIR + ICONDIRENTRYs + PNG blobs.
        // Windows supports PNG-compressed ICO since Vista.
        int count = sizes.Length;
        int headerSize = 6 + count * 16;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // ICONDIR
        bw.Write((short)0);      // reserved
        bw.Write((short)1);      // type = ICO
        bw.Write((short)count);  // image count

        // ICONDIRENTRYs — offsets are relative to start of file
        int dataOffset = headerSize;
        for (int i = 0; i < count; i++)
        {
            int sz = sizes[i];
            bw.Write((byte)(sz >= 256 ? 0 : sz)); // width  (0 = 256)
            bw.Write((byte)(sz >= 256 ? 0 : sz)); // height (0 = 256)
            bw.Write((byte)0);   // color count (0 = true-color)
            bw.Write((byte)0);   // reserved
            bw.Write((short)1);  // planes
            bw.Write((short)32); // bits per pixel
            bw.Write(pngs[i].Length);
            bw.Write(dataOffset);
            dataOffset += pngs[i].Length;
        }

        foreach (var png in pngs)
            bw.Write(png);

        ms.Position = 0;
        return new Icon(ms);
    }
}
