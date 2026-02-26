using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class FileNameHelper
{
    /// <summary>
    /// Converts an arbitrary string into a Windows-safe filename.
    /// If input looks like it has an extension, it will be preserved.
    /// </summary>
    public static string ToSafeWindowsFileName(string input, int maxBaseLength = 150, string replacement = "_")
    {
        if (string.IsNullOrWhiteSpace(input))
            return "file";

        input = input.Trim();

        // Split extension (optional)
        var ext = Path.GetExtension(input);
        var baseName = string.IsNullOrEmpty(ext) ? input : input[..^ext.Length];

        baseName = SanitizeWindowsFileNamePart(baseName, replacement);

        // Windows forbids trailing dots/spaces
        baseName = baseName.TrimEnd(' ', '.');

        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "file";

        // Reserved device names (case-insensitive) are not allowed even with extension in many contexts
        if (IsWindowsReservedDeviceName(baseName))
            baseName = "_" + baseName;

        // Enforce length budget (keep extension)
        // Windows max segment is 255 chars, but keep conservative.
        var maxTotal = 200;
        var maxForBase = Math.Min(maxBaseLength, maxTotal - ext.Length);
        if (maxForBase < 1) maxForBase = 1;

        if (baseName.Length > maxForBase)
            baseName = baseName.Substring(0, maxForBase).TrimEnd(' ', '.');

        // Sanitize extension too (rare, but just in case)
        ext = SanitizeWindowsExtension(ext);

        return baseName + ext;
    }

    private static string SanitizeWindowsFileNamePart(string s, string replacement)
    {
        // Replace invalid filename chars
        var invalid = Path.GetInvalidFileNameChars();
        s = new string(s.Select(ch => invalid.Contains(ch) ? replacement[0] : ch).ToArray());

        // Remove control chars explicitly (Path.GetInvalidFileNameChars already covers most, but be safe)
        s = Regex.Replace(s, @"\p{C}+", "");

        // Normalize whitespace -> replacement
        s = Regex.Replace(s, @"\s+", replacement);

        // Collapse repeated replacements
        var repEsc = Regex.Escape(replacement);
        s = Regex.Replace(s, $"{repEsc}+", replacement);

        return s.Trim();
    }

    private static bool IsWindowsReservedDeviceName(string baseName)
    {
        // Compare only the base name (no extension), ignore trailing dots/spaces already removed.
        var name = baseName.Trim().TrimEnd(' ', '.');

        // Windows reserved device names
        // (CON, PRN, AUX, NUL, COM1-9, LPT1-9)
        var upper = name.ToUpperInvariant();

        if (upper is "CON" or "PRN" or "AUX" or "NUL")
            return true;

        if (upper.Length == 4 && (upper.StartsWith("COM") || upper.StartsWith("LPT")))
        {
            var last = upper[3];
            return last >= '1' && last <= '9';
        }

        return false;
    }

    private static string SanitizeWindowsExtension(string ext)
    {
        if (string.IsNullOrEmpty(ext))
            return "";

        // Keep dot + letters/numbers only
        // e.g. ".ya:ml" -> ".yaml"
        var cleaned = Regex.Replace(ext, @"[^A-Za-z0-9\.]+", "");
        if (cleaned == ".") return "";
        return cleaned;
    }
}
