using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Pure-function helpers for computing tab-stop positions.
/// No WinForms handle or UI-thread dependency — fully unit-testable.
/// </summary>
internal static class ElasticTabService
{
    /// <summary>
    /// Computes elastic (content-aware) tab-stop positions for a slice of
    /// <paramref name="lines"/>.
    /// </summary>
    /// <param name="lines">All text lines in the editor.</param>
    /// <param name="firstLine">Index of the first visible line (inclusive).</param>
    /// <param name="lastLine">Index of the last visible line (inclusive).</param>
    /// <param name="measureWidth">
    ///   Callback that returns the pixel width of a cell string.
    ///   Pass <c>s =&gt; TabMeasurementCache.GetStringWidth(s, font)</c> from
    ///   Form1; inject a simple lambda (<c>s =&gt; s.Length * 8</c>) in tests.
    /// </param>
    /// <param name="token">Cancellation token; returns empty on cancellation.</param>
    /// <returns>
    ///   Array of cumulative tab-stop pixel positions, or an empty array when
    ///   the token is cancelled or there are no tab-delimited columns.
    /// </returns>
    public static int[] ComputeTabStops(
        string[] lines,
        int firstLine,
        int lastLine,
        Func<string, int> measureWidth,
        CancellationToken token,
        int minTabWidth = 32)
    {
        var maxWidths = new Dictionary<int, int>();

        for (int lineIdx = firstLine; lineIdx <= lastLine; lineIdx++)
        {
            if (token.IsCancellationRequested) return Array.Empty<int>();

            string line = lines[lineIdx];
            string[] cells = line.Split('\t');
            for (int col = 0; col < cells.Length - 1; col++)
            {
                int w = measureWidth(cells[col]);
                if (!maxWidths.TryGetValue(col, out int prev) || w > prev)
                    maxWidths[col] = w;
            }
        }

        if (token.IsCancellationRequested) return Array.Empty<int>();

        var stops = new List<int>();
        int cumulative = 0;
        for (int i = 0; ; i++)
        {
            if (!maxWidths.TryGetValue(i, out int w)) break;
            // Each column is at least minTabWidth wide so leading-indentation tabs
            // (where the cell content is empty, giving w=0) render at the full
            // configured tab width instead of collapsing to 2px.
            cumulative += Math.Max(minTabWidth, w + 2);
            stops.Add(cumulative);
        }

        return stops.ToArray();
    }

    /// <summary>
    /// Builds a uniform sequence of tab-stop positions for fixed-width tab
    /// rendering (used when elastic tabs are disabled).
    /// </summary>
    /// <param name="charWidth">Pixel width of one monospace character.</param>
    /// <param name="tabSize">Number of characters per tab stop.</param>
    /// <param name="maxStopCount">Maximum number of stop entries to generate.</param>
    public static int[] BuildFixedTabStops(int charWidth, int tabSize, int maxStopCount = 500)
    {
        if (charWidth <= 0) charWidth = 8;
        int tabPixelWidth = Math.Max(1, charWidth * tabSize);
        var stops = new int[maxStopCount];
        for (int i = 0; i < maxStopCount; i++)
            stops[i] = tabPixelWidth * (i + 1);
        return stops;
    }
}

/// <summary>
/// Thread-safe cache of GDI+ string widths keyed by (text, fontName, fontSize).
/// Promoted from a private nested class in Form1 so tests and services can reach it.
/// </summary>
internal static class TabMeasurementCache
{
    private static readonly ConcurrentDictionary<string, int> _cache
        = new(StringComparer.Ordinal);

    public static int GetStringWidth(string s, Font font)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        string key = $"{s}|{font.Name}|{font.Size}";
        return _cache.GetOrAdd(key, _ =>
        {
            using var bmp = new Bitmap(1, 1);
            using var g = Graphics.FromImage(bmp);
            var size = g.MeasureString(s, font, new PointF(0, 0), StringFormat.GenericTypographic);
            return (int)Math.Ceiling(size.Width);
        });
    }
}
