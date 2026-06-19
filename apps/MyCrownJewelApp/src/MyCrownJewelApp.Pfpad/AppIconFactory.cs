using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>Generates the Pfpad "pfp" monogram tile icon at runtime.</summary>
internal static class AppIconFactory
{
    // Gradient stops: lighter azure (top-left) → deep navy (bottom-right)
    private static readonly Color _tileTop    = Color.FromArgb(0x3D, 0xA9, 0xE8);
    private static readonly Color _tileBottom = Color.FromArgb(0x17, 0x60, 0xA0);

    private static Icon? _cachedIcon;
    private static Bitmap? _cachedBitmap32;

    /// <summary>Returns a cached multi-size (16/32/48/256 px) Icon for use as the window icon.</summary>
    public static Icon GetIcon() => _cachedIcon ??= BuildMultiSizeIcon();

    /// <summary>Returns a fresh Bitmap of the icon at the requested pixel size.</summary>
    public static Bitmap CreateBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
        g.TextRenderingHint  = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        // Rounded tile — corner radius ≈ 22% (matches modern app-icon style)
        int radius = Math.Max(2, size / 5);
        var tileRect = new Rectangle(0, 0, size, size);

        using (var tilePath = BuildRoundedPath(tileRect, radius))
        {
            // Gradient background
            using var grad = new LinearGradientBrush(tileRect, _tileTop, _tileBottom,
                                                     LinearGradientMode.ForwardDiagonal);
            g.FillPath(grad, tilePath);

            // Frosted-glass highlight band across the upper third (size ≥ 32 only)
            if (size >= 32)
            {
                g.SetClip(tilePath);
                int bandH = size / 3;
                using var shimmer = new LinearGradientBrush(
                    new Rectangle(0, 0, size, bandH),
                    Color.FromArgb(55, 255, 255, 255),
                    Color.FromArgb(0,  255, 255, 255),
                    LinearGradientMode.Vertical);
                g.FillRectangle(shimmer, 0, 0, size, bandH);
                g.ResetClip();
            }
        }

        // "pfp" monogram — Bold, centred, white
        // Font scale: 0.33 fits three Segoe UI Bold chars comfortably at any size
        float fontSize = size * 0.33f;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming      = StringTrimming.None
        };

        // Subtle drop-shadow at larger sizes for legibility on the gradient
        if (size >= 32)
        {
            using var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0));
            float shift = Math.Max(1f, size * 0.025f);
            g.DrawString("pfp", font, shadow,
                new RectangleF(shift, size * 0.03f + shift, size, size), sf);
        }

        // Foreground text — slight downward nudge so descenders of "p" appear visually centred
        g.DrawString("pfp", font, Brushes.White,
            new RectangleF(0, size * 0.03f, size, size), sf);

        return bmp;
    }

    /// <summary>Returns a cached 32×32 Bitmap (no dispose — owned by factory).</summary>
    public static Bitmap GetBitmap32() => _cachedBitmap32 ??= CreateBitmap(32);

    private static GraphicsPath BuildRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X,          rect.Y,          d, d, 180, 90);
        path.AddArc(rect.Right - d,  rect.Y,          d, d, 270, 90);
        path.AddArc(rect.Right - d,  rect.Bottom - d, d, d,   0, 90);
        path.AddArc(rect.X,          rect.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    private static Icon BuildMultiSizeIcon()
    {
        int[] sizes = [16, 32, 48, 256];
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
        int count      = sizes.Length;
        int headerSize = 6 + count * 16;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // ICONDIR
        bw.Write((short)0);     // reserved
        bw.Write((short)1);     // type = ICO
        bw.Write((short)count); // image count

        // ICONDIRENTRYs — offsets are relative to start of file
        int dataOffset = headerSize;
        for (int i = 0; i < count; i++)
        {
            int sz = sizes[i];
            bw.Write((byte)(sz >= 256 ? 0 : sz)); // width  (0 means 256)
            bw.Write((byte)(sz >= 256 ? 0 : sz)); // height (0 means 256)
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
