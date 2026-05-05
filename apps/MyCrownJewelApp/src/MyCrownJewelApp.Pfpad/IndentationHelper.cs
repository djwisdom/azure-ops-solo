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
    }
}
