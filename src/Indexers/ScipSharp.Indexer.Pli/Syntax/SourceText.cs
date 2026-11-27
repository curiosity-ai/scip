using System.Text;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Pli.Syntax;

/// <summary>
/// A PL/I source file prepared for lexing: the original text plus a "masked" copy of identical shape in which every
/// character outside the source margins (and every <c>*PROCESS</c>/<c>%PROCESS</c> line) is blanked, so that token
/// positions computed on the masked text are positions in the original file.
/// </summary>
internal sealed partial class SourceText
{
    private SourceText(string text, string masked, Margins margins, IReadOnlyList<string> processOptions)
    {
        Text = text;
        Masked = masked;
        Margins = margins;
        ProcessOptions = processOptions;
    }

    /// <summary>The decoded file contents.</summary>
    public string Text { get; }

    /// <summary>Same length as <see cref="Text"/>; non-source characters replaced by spaces (newlines kept).</summary>
    public string Masked { get; }

    /// <summary>The margins that were applied.</summary>
    public Margins Margins { get; }

    /// <summary>Text of the <c>*PROCESS</c>/<c>%PROCESS</c> statements found at the top of the file.</summary>
    public IReadOnlyList<string> ProcessOptions { get; }

    /// <summary>Decodes bytes as UTF-8, falling back to Latin-1 for files that are not valid UTF-8 (EBCDIC transfers).</summary>
    public static string Decode(byte[] bytes)
    {
        try
        {
            var s = new UTF8Encoding(false, true).GetString(bytes);
            return s.Length > 0 && s[0] == '﻿' ? s[1..] : s;
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    public static SourceText Create(string text, Margins margins, MarginMode mode = MarginMode.Fixed)
    {
        var lines = SplitLines(text);
        var process = new List<string>();
        var processLines = new HashSet<int>();

        // *PROCESS / %PROCESS statements may only precede the source proper.
        for (var i = 0; i < lines.Count; i++)
        {
            var (start, length) = lines[i];
            var line = text.AsSpan(start, length);
            var trimmed = line.TrimStart();
            if (trimmed.IsEmpty)
            {
                continue;
            }

            if (!IsProcessLine(line))
            {
                break;
            }

            processLines.Add(i);
            var options = trimmed[1..].ToString();
            process.Add(options);
            if (ParseProcessMargins(options) is { } m)
            {
                margins = m;
                mode = MarginMode.Fixed;
            }
        }

        if (mode == MarginMode.Auto)
        {
            margins = LooksColumnOriented(text, lines, processLines) ? Margins.Default : Margins.None;
        }

        var masked = new StringBuilder(text);
        for (var i = 0; i < lines.Count; i++)
        {
            var (start, length) = lines[i];
            if (processLines.Contains(i))
            {
                Blank(masked, start, length);
                continue;
            }

            if (margins.IsNone)
            {
                continue;
            }

            var left = Math.Min(margins.Left - 1, length);
            Blank(masked, start, left);
            if (margins.Right > 0 && margins.Right < length)
            {
                Blank(masked, start + margins.Right, length - margins.Right);
            }
        }

        return new SourceText(text, masked.ToString(), margins, process);
    }

    private static bool IsProcessLine(ReadOnlySpan<char> line)
    {
        var t = line.TrimStart();
        return (t.StartsWith("*PROCESS", StringComparison.OrdinalIgnoreCase) || t.StartsWith("%PROCESS", StringComparison.OrdinalIgnoreCase))
            && (t.Length == 8 || !char.IsLetterOrDigit(t[8]));
    }

    /// <summary>Extracts <c>MARGINS(l,r)</c>/<c>MAR(l,r)</c>/<c>NOMARGINS</c> from a process statement.</summary>
    internal static Margins? ParseProcessMargins(string options)
    {
        if (NoMarginsRegex().IsMatch(options))
        {
            return Margins.None;
        }

        var m = MarginsRegex().Match(options);
        if (!m.Success)
        {
            return null;
        }

        var l = int.Parse(m.Groups[1].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
        var r = int.Parse(m.Groups[2].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
        return l >= 1 && r >= l ? new Margins(l, r) : null;
    }

    /// <summary>
    /// Heuristic for <see cref="MarginMode.Auto"/>: the file is column oriented when no line has source text in column 1
    /// (column 1 is carriage control there) or when some line carries a sequence number in columns 73-80.
    /// </summary>
    private static bool LooksColumnOriented(string text, List<(int Start, int Length)> lines, HashSet<int> skip)
    {
        var col1 = false;
        var sequence = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (skip.Contains(i))
            {
                continue;
            }

            var line = text.AsSpan(lines[i].Start, lines[i].Length);
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                col1 = true;
            }

            if (line.Length >= 80 && line[72..80].Trim().Length > 0 && IsDigitsOrBlank(line[72..80]))
            {
                sequence = true;
            }
        }

        return sequence || !col1;
    }

    private static bool IsDigitsOrBlank(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
        {
            if (!char.IsAsciiDigit(c) && c != ' ')
            {
                return false;
            }
        }

        return true;
    }

    private static void Blank(StringBuilder b, int start, int length)
    {
        for (var i = start; i < start + length; i++)
        {
            if (b[i] is not ('\n' or '\r'))
            {
                b[i] = ' ';
            }
        }
    }

    /// <summary>Start offset and length (without the line terminator) of every line.</summary>
    internal static List<(int Start, int Length)> SplitLines(string text)
    {
        var result = new List<(int, int)>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                var end = i > start && text[i - 1] == '\r' ? i - 1 : i;
                result.Add((start, end - start));
                start = i + 1;
            }
        }

        result.Add((start, text.Length - start));
        return result;
    }

    [GeneratedRegex(@"\bNO(?:MARGINS|MAR)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoMarginsRegex();

    [GeneratedRegex(@"\bMAR(?:GINS)?\s*\(\s*(\d+)\s*,\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarginsRegex();
}
