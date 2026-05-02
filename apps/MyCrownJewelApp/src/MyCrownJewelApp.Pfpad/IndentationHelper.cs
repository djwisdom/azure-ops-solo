using System;

namespace MyCrownJewelApp.Pfpad
{
    /// <summary>
    /// Provides static methods for computing indentation strings based on tab size and insert-spaces setting.
    /// </summary>
    public static class IndentationHelper
    {
        /// <summary>
        /// Computes a whitespace string that, when placed at the caret, advances the visual cursor by the specified number of character columns.
        /// Tabs count as TabSize columns, spaces as 1.
        /// </summary>
        /// <param name="targetVisualColumns">Desired number of character columns to advance.</param>
        /// <param name="tabSize">Number of spaces per tab stop.</param>
        /// <param name="insertSpaces">If true, uses spaces only; otherwise uses tabs as much as possible and pads with spaces.</param>
        /// <returns>A string of whitespace characters.</returns>
        public static string ComputeMixedIndent(int targetVisualColumns, int tabSize, bool insertSpaces)
        {
            if (targetVisualColumns <= 0) return string.Empty;
            if (insertSpaces)
            {
                return new string(' ', targetVisualColumns);
            }
            else
            {
                int tabs = targetVisualColumns / tabSize;
                int spaces = targetVisualColumns % tabSize;
                return new string('\t', tabs) + new string(' ', spaces);
            }
        }

        /// <summary>
        /// Computes the indentation string for a new line based on the leading whitespace of the previous line.
        /// The result respects the current tabSize and insertSpaces setting.
        /// </summary>
        /// <param name="prevLineText">Text of the line above the new line.</param>
        /// <param name="tabSize">Number of spaces per tab stop.</param>
        /// <param name="insertSpaces">If true, converts any tabs to spaces in the result.</param>
        /// <returns>A whitespace string to insert after the newline.</returns>
        public static string ComputeIndent(string prevLineText, int tabSize, bool insertSpaces)
        {
            // Count visual columns of leading whitespace
            int col = 0;
            foreach (char c in prevLineText)
            {
                if (!char.IsWhiteSpace(c)) break;
                col += (c == '\t') ? tabSize : 1;
            }
            return ComputeMixedIndent(col, tabSize, insertSpaces);
        }
    }
}
