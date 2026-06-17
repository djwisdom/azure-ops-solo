using System;
using System.Linq;

namespace MyCrownJewelApp.Pfpad
{
    public static class IndentationHelper
    {
        public static string ComputeMixedIndent(int targetVisualColumns, int tabSize, bool insertSpaces)
        {
            if (targetVisualColumns <= 0) return string.Empty;
            if (insertSpaces)
                return new string(' ', targetVisualColumns);

            int tabs = targetVisualColumns / tabSize;
            int spaces = targetVisualColumns % tabSize;
            return new string('\t', tabs) + new string(' ', spaces);
        }

        public static string ComputeIndent(string prevLineText, int tabSize, bool insertSpaces)
        {
            int col = 0;
            foreach (char c in prevLineText)
            {
                if (!char.IsWhiteSpace(c)) break;
                col += (c == '\t') ? tabSize : 1;
            }

            string trimmed = prevLineText.Trim();
            if (trimmed.EndsWith("{") || trimmed.EndsWith("("))
                col += tabSize;

            return ComputeMixedIndent(col, tabSize, insertSpaces);
        }

        public static int GetIndentColumn(string lineText, int tabSize)
        {
            int col = 0;
            foreach (char c in lineText)
            {
                if (!char.IsWhiteSpace(c)) break;
                col += (c == '\t') ? tabSize : 1;
            }
            return col;
        }

        /// <summary>Returns the raw leading-whitespace prefix of a line (all chars before the first non-whitespace).</summary>
        public static string GetLeadingWhitespace(string line)
        {
            int i = 0;
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            return line[..i];
        }

        /// <summary>
        /// Scans up to <paramref name="maxLines"/> lines of <paramref name="content"/> to
        /// detect whether the file uses tabs or spaces and what the common indent width is.
        /// Returns (insertSpaces, tabSize, detected=true) on success, or (true, 4, false) when
        /// the content has too little indentation to decide.
        /// </summary>
        public static (bool insertSpaces, int tabSize, bool detected) DetectIndentation(
            string content, int maxLines = 100)
        {
            int tabLines = 0, spaceLines = 0;
            var spaceCounts = new Dictionary<int, int>();
            int lineCount = 0;
            int i = 0;

            while (i < content.Length && lineCount < maxLines)
            {
                int lineEnd = content.IndexOf('\n', i);
                if (lineEnd < 0) lineEnd = content.Length;
                string line = content[i..lineEnd];

                if (line.Length > 0)
                {
                    if (line[0] == '\t')
                    {
                        tabLines++;
                    }
                    else if (line[0] == ' ')
                    {
                        int spaces = 0;
                        while (spaces < line.Length && line[spaces] == ' ') spaces++;
                        // Only count lines where the indent precedes actual content
                        if (spaces > 0 && spaces < line.Length && !char.IsWhiteSpace(line[spaces]))
                        {
                            spaceLines++;
                            spaceCounts.TryGetValue(spaces, out int cnt);
                            spaceCounts[spaces] = cnt + 1;
                        }
                    }
                }

                i = lineEnd + 1;
                lineCount++;
            }

            if (tabLines == 0 && spaceLines == 0) return (true, 4, false);
            if (tabLines > spaceLines) return (false, 4, true);

            // Determine most common indent width from observed run lengths
            int tabSize = 4;
            if (spaceCounts.Count > 0)
            {
                int best = spaceCounts.OrderByDescending(x => x.Value).First().Key;
                tabSize = best <= 2 ? 2 : best <= 4 ? 4 : best <= 6 ? 6 : 8;
            }
            return (true, tabSize, true);
        }
    }
}
