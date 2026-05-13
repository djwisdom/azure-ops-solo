using System;
using System.IO;
using System.Text;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Utility class for handling Unicode file operations with proper encoding detection.
/// </summary>
public static class UnicodeFileHelper
{
    /// <summary>
    /// Reads a text file with automatic encoding detection.
    /// Tries UTF-8 with BOM, then UTF-16, then system's default encoding.
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <returns>Tuple of (content, detected encoding)</returns>
    public static (string Content, Encoding Encoding) ReadAllTextWithEncoding(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        // Try UTF-8 with BOM first
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            string content = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            return (content, Encoding.UTF8);
        }

        // Try UTF-16 LE with BOM
        if (bytes.Length >= 2 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xFE)
        {
            string content = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            return (content, Encoding.Unicode);
        }

        // Try UTF-16 BE with BOM
        if (bytes.Length >= 2 &&
            bytes[0] == 0xFE &&
            bytes[1] == 0xFF)
        {
            string content = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            return (content, Encoding.BigEndianUnicode);
        }

        // Try UTF-8 without BOM
        try
        {
            string content = Encoding.UTF8.GetString(bytes);
            // Check if it's valid UTF-8 by trying to re-encode
            byte[] reencoded = Encoding.UTF8.GetBytes(content);
            if (reencoded.Length == bytes.Length)
            {
                return (content, Encoding.UTF8);
            }
        }
        catch
        {
            // Not valid UTF-8
        }

        // Fallback to system's default encoding
        string fallbackContent = Encoding.Default.GetString(bytes);
        return (fallbackContent, Encoding.Default);
    }

    /// <summary>
    /// Writes text to a file with UTF-8 encoding and BOM.
    /// </summary>
    public static void WriteAllText(string path, string content)
    {
        // Always write with UTF-8 BOM for maximum compatibility
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.Write(content);
    }

    /// <summary>
    /// Gets a user-friendly encoding name for display.
    /// </summary>
    public static string GetEncodingDisplayName(Encoding encoding)
    {
        if (encoding == Encoding.UTF8)
            return "UTF-8";
        if (encoding == Encoding.Unicode)
            return "UTF-16 LE";
        if (encoding == Encoding.BigEndianUnicode)
            return "UTF-16 BE";
        if (encoding == Encoding.Default)
            return "System Default";
        return encoding.EncodingName;
    }

    /// <summary>
    /// Detects if text contains right-to-left characters that may need special handling.
    /// </summary>
    public static bool ContainsRtlText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // Check for RTL Unicode ranges
        foreach (char c in text)
        {
            int codePoint = c;
            // Arabic, Hebrew, and other RTL scripts
            if ((codePoint >= 0x0590 && codePoint <= 0x05FF) || // Hebrew
                (codePoint >= 0x0600 && codePoint <= 0x06FF) || // Arabic
                (codePoint >= 0x0750 && codePoint <= 0x077F) || // Arabic Supplement
                (codePoint >= 0x08A0 && codePoint <= 0x08FF) || // Arabic Extended-A
                (codePoint >= 0xFB50 && codePoint <= 0xFDFF) || // Arabic Presentation Forms-A
                (codePoint >= 0xFE70 && codePoint <= 0xFEFF))   // Arabic Presentation Forms-B
            {
                return true;
            }
        }
        return false;
    }
}