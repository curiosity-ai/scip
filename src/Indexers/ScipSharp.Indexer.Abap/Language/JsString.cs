namespace ScipSharp.Indexer.Abap;

/// <summary>Helpers reproducing JavaScript string semantics where they differ from .NET.</summary>
public static class JsString
{
    /// <summary>JS <c>String.prototype.toUpperCase</c> (locale independent, ß becomes SS).</summary>
    public static string ToUpper(string s)
    {
        var ret = s.ToUpperInvariant();
        return ret.Contains('ß') ? ret.Replace("ß", "SS") : ret;
    }

    /// <summary>JS <c>String.prototype.toLowerCase</c>.</summary>
    public static string ToLower(string s) => s.ToLowerInvariant();

    /// <summary>JS whitespace as used by <c>trim()</c>: Unicode White_Space plus BOM.</summary>
    public static bool IsJsWhiteSpace(char c) => char.IsWhiteSpace(c) || c == '﻿';

    /// <summary>JS <c>String.prototype.trim</c>.</summary>
    public static string Trim(string s)
    {
        int start = 0, end = s.Length;
        while (start < end && IsJsWhiteSpace(s[start])) start++;
        while (end > start && IsJsWhiteSpace(s[end - 1])) end--;
        return (start == 0 && end == s.Length) ? s : s.Substring(start, end - start);
    }
}
