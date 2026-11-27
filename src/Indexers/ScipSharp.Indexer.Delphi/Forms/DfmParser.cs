namespace ScipSharp.Indexer.Delphi.Forms;

/// <summary>A component in a text form file: <c>object Button1: TButton</c> (or <c>inherited</c>/<c>inline</c>).</summary>
internal sealed class DfmObject(string keyword, DfmObject? parent)
{
    public string Keyword { get; } = keyword;

    public DfmObject? Parent { get; } = parent;

    public string? Name { get; set; }

    public int Line { get; set; }

    public int NameColumn { get; set; }

    public string TypeName { get; set; } = "";

    public int TypeColumn { get; set; }

    public List<DfmObject> Children { get; } = [];

    public List<DfmProperty> Properties { get; } = [];
}

/// <summary>A property assignment <c>OnClick = Button1Click</c>. <see cref="IdentifierValue"/> is set for identifier values.</summary>
internal sealed record DfmProperty(string Name, int Line, int Column, string? IdentifierValue, int ValueColumn);

/// <summary>
/// Parses text DFM/FMX/LFM form files (binary forms, which start with <c>TPF0</c>, are not supported). Collections
/// (<c>&lt; item … end &gt;</c>), binary data (<c>{ … }</c>), lists (<c>( … )</c>) and continued strings are skipped.
/// </summary>
internal static class DfmParser
{
    public static bool IsBinary(ReadOnlySpan<byte> bytes) => bytes.Length >= 4 && bytes[0] == 'T' && bytes[1] == 'P' && bytes[2] == 'F' && bytes[3] == '0' ||
                                                            bytes.Length > 0 && bytes[0] == 0xFF;

    public static List<DfmObject> Parse(string text)
    {
        var roots = new List<DfmObject>();
        var stack = new Stack<DfmObject>();
        var lines = text.Split('\n');
        var mode = 0; // 0 normal, 1 inside <collection>, 2 inside {binary}, 3 inside (list)
        var collectionDepth = 0;
        for (var ln = 0; ln < lines.Length; ln++)
        {
            var raw = lines[ln].TrimEnd('\r');
            var indent = 0;
            while (indent < raw.Length && raw[indent] is ' ' or '\t')
            {
                indent++;
            }

            var line = raw[indent..].TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            switch (mode)
            {
                case 1:
                    // Collections may nest (`Items = <` inside an item); `end>` closes one level.
                    if (line.EndsWith('<'))
                    {
                        collectionDepth++;
                    }
                    else if (line.EndsWith('>') && !IsInsideString(line) && --collectionDepth <= 0)
                    {
                        mode = 0;
                    }

                    continue;
                case 2:
                    if (line.EndsWith('}'))
                    {
                        mode = 0;
                    }

                    continue;
                case 3:
                    if (line.EndsWith(')') && !IsInsideString(line))
                    {
                        mode = 0;
                    }

                    continue;
            }

            var word = FirstWord(line);
            if (word.Equals("object", StringComparison.OrdinalIgnoreCase) || word.Equals("inherited", StringComparison.OrdinalIgnoreCase) || word.Equals("inline", StringComparison.OrdinalIgnoreCase))
            {
                var obj = new DfmObject(word.ToLowerInvariant(), stack.Count > 0 ? stack.Peek() : null) { Line = ln };
                var rest = indent + word.Length;
                var colon = raw.IndexOf(':', rest);
                var p = rest;
                while (p < raw.Length && raw[p] is ' ' or '\t')
                {
                    p++;
                }

                if (colon > 0)
                {
                    var name = raw[p..colon].Trim();
                    obj.Name = name;
                    obj.NameColumn = p;
                    p = colon + 1;
                    while (p < raw.Length && raw[p] is ' ' or '\t')
                    {
                        p++;
                    }
                }

                var tstart = p;
                while (p < raw.Length && (char.IsLetterOrDigit(raw[p]) || raw[p] is '_' or '.'))
                {
                    p++;
                }

                obj.TypeName = raw[tstart..p];
                obj.TypeColumn = tstart;
                if (stack.Count > 0)
                {
                    stack.Peek().Children.Add(obj);
                }
                else
                {
                    roots.Add(obj);
                }

                stack.Push(obj);
                continue;
            }

            if (word.Equals("end", StringComparison.OrdinalIgnoreCase) && line.Length == 3)
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }

                continue;
            }

            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0 && stack.Count > 0 && IsPropertyName(line.AsSpan(0, eq).Trim()))
            {
                var propName = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();
                var valueCol = indent + eq + 1;
                while (valueCol < raw.Length && raw[valueCol] is ' ' or '\t')
                {
                    valueCol++;
                }

                string? ident = null;
                if (value.Length > 0 && IsIdentifier(value) && !value.Equals("True", StringComparison.OrdinalIgnoreCase) && !value.Equals("False", StringComparison.OrdinalIgnoreCase))
                {
                    ident = value;
                }

                stack.Peek().Properties.Add(new DfmProperty(propName, ln, indent, ident, valueCol));
                if (value.StartsWith('<') && !value.EndsWith('>'))
                {
                    mode = 1;
                    collectionDepth = 1;
                }
                else if (value.StartsWith('{') && !value.EndsWith('}'))
                {
                    mode = 2;
                }
                else if (value.StartsWith('(') && !value.EndsWith(')'))
                {
                    mode = 3;
                }
            }
        }

        return roots;
    }

    private static bool IsInsideString(string line) => line.Count(c => c == '\'') % 2 == 1;

    private static string FirstWord(string line)
    {
        var i = 0;
        while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
        {
            i++;
        }

        return line[..i];
    }

    private static bool IsPropertyName(ReadOnlySpan<char> s)
    {
        if (s.Length == 0 || !(char.IsLetter(s[0]) || s[0] == '_'))
        {
            return false;
        }

        foreach (var c in s)
        {
            if (!(char.IsLetterOrDigit(c) || c is '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifier(string s)
    {
        if (!(char.IsLetter(s[0]) || s[0] == '_'))
        {
            return false;
        }

        foreach (var c in s)
        {
            if (!(char.IsLetterOrDigit(c) || c is '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}
