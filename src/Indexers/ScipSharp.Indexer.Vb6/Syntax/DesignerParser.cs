namespace ScipSharp.Indexer.Vb6.Syntax;

/// <summary>A span on one line of a source file.</summary>
internal readonly record struct Span(int Line, int Col, int EndCol);

/// <summary>An object declared in a designer block: <c>Begin VB.CommandButton Command1</c>.</summary>
/// <param name="Library">Type library prefix (<c>VB</c>, <c>MSComctlLib</c>), or null for a <c>{GUID}</c> designer.</param>
/// <param name="TypeName">Class name (<c>CommandButton</c>), or the GUID text.</param>
/// <param name="TypeSpan">Position of <paramref name="TypeName"/> when it is a class name.</param>
/// <param name="Depth">0 for the form/user control itself, 1+ for controls.</param>
internal sealed record DesignerObject(string? Library, string TypeName, Span? TypeSpan, string Name, Span NameSpan, int Depth)
{
    /// <summary>An <c>Index = n</c> property marks a member of a control array.</summary>
    public int? Index { get; set; }

    /// <summary>The value of the <c>Caption</c> property, if any (used in documentation).</summary>
    public string? Caption { get; set; }

    public string QualifiedType => Library is null ? TypeName : $"{Library}.{TypeName}";
}

/// <summary>Header of a .frm/.ctl/.cls/.dsr file: everything before the code (and its Attribute lines).</summary>
internal sealed record DesignerHeader(int CodeStartLine, string? Version, List<DesignerObject> Objects, List<string> ObjectReferences)
{
    public DesignerObject? Root => Objects.Count > 0 && Objects[0].Depth == 0 ? Objects[0] : null;

    public IEnumerable<DesignerObject> Controls => Objects.Where(o => o.Depth > 0);
}

/// <summary>Parses the designer block that VB6 writes at the top of form-like and class modules.</summary>
internal static class DesignerParser
{
    public static DesignerHeader Parse(IReadOnlyList<string> lines)
    {
        var objects = new List<DesignerObject>();
        var objectRefs = new List<string>();
        var i = 0;
        while (i < lines.Count && lines[i].Trim().Length == 0)
        {
            i++;
        }

        if (i >= lines.Count || !StartsWithWord(lines[i].TrimStart(), "VERSION"))
        {
            return new DesignerHeader(0, null, objects, objectRefs);
        }

        var version = lines[i].Trim()["VERSION".Length..].Trim();
        i++;
        var depth = 0;
        var propertyDepth = 0;
        var started = false;
        var stack = new Stack<DesignerObject>();
        for (; i < lines.Count; i++)
        {
            var raw = lines[i];
            var line = raw.TrimStart();
            var indent = raw.Length - line.Length;
            if (line.Length == 0)
            {
                continue;
            }

            if (!started && StartsWithWord(line, "Object"))
            {
                objectRefs.Add(line);
                continue;
            }

            if (StartsWithWord(line, "BeginProperty"))
            {
                propertyDepth++;
                continue;
            }

            if (StartsWithWord(line, "EndProperty"))
            {
                propertyDepth = Math.Max(0, propertyDepth - 1);
                continue;
            }

            if (propertyDepth > 0)
            {
                continue;
            }

            if (StartsWithWord(line, "Begin"))
            {
                started = true;
                var obj = ParseBegin(line, i, indent, depth);
                if (obj is not null)
                {
                    objects.Add(obj);
                    stack.Push(obj);
                }
                else
                {
                    stack.Push(new DesignerObject(null, "", null, "", default, depth));
                }

                depth++;
                continue;
            }

            if (line.TrimEnd().Equals("End", StringComparison.OrdinalIgnoreCase))
            {
                depth--;
                if (stack.Count > 0)
                {
                    stack.Pop();
                }

                if (depth <= 0)
                {
                    return new DesignerHeader(i + 1, version, objects, objectRefs);
                }

                continue;
            }

            if (!started)
            {
                // Not a designer block after all (e.g. "VERSION" followed directly by code); stop here.
                return new DesignerHeader(i, version, objects, objectRefs);
            }

            if (stack.Count > 0 && stack.Peek() is { Name.Length: > 0 } current)
            {
                var eq = line.IndexOf('=', StringComparison.Ordinal);
                if (eq > 0)
                {
                    var key = line[..eq].Trim();
                    var value = line[(eq + 1)..].Trim();
                    if (key.Equals("Index", StringComparison.OrdinalIgnoreCase) && int.TryParse(StripComment(value), out var index))
                    {
                        current.Index = index;
                    }
                    else if (key.Equals("Caption", StringComparison.OrdinalIgnoreCase) && value.StartsWith('"'))
                    {
                        current.Caption = value.Length >= 2 && value.EndsWith('"') ? value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal) : value[1..];
                    }
                }
            }
        }

        return new DesignerHeader(lines.Count, version, objects, objectRefs);
    }

    private static string StripComment(string value)
    {
        var q = value.IndexOf('\'', StringComparison.Ordinal);
        return (q >= 0 ? value[..q] : value).Trim();
    }

    private static bool StartsWithWord(string line, string word) =>
        line.StartsWith(word, StringComparison.OrdinalIgnoreCase)
        && (line.Length == word.Length || !Lexer.IsIdentifierPart(line[word.Length]));

    // "Begin VB.CommandButton Command1" / "Begin {GUID} DataEnvironment1" / "BEGIN" (class header)
    private static DesignerObject? ParseBegin(string line, int lineNo, int indent, int depth)
    {
        var pos = "Begin".Length;
        SkipSpaces(line, ref pos);
        if (pos >= line.Length)
        {
            return null;
        }

        var typeStart = pos;
        while (pos < line.Length && !char.IsWhiteSpace(line[pos]))
        {
            pos++;
        }

        var type = line[typeStart..pos];
        SkipSpaces(line, ref pos);
        var nameStart = pos;
        while (pos < line.Length && Lexer.IsIdentifierPart(line[pos]))
        {
            pos++;
        }

        if (pos == nameStart)
        {
            return null;
        }

        var name = line[nameStart..pos];
        string? library = null;
        var typeName = type;
        Span? typeSpan = null;
        if (!type.StartsWith('{'))
        {
            var dot = type.LastIndexOf('.');
            if (dot > 0)
            {
                library = type[..dot];
                typeName = type[(dot + 1)..];
                typeSpan = new Span(lineNo, indent + typeStart + dot + 1, indent + typeStart + type.Length);
            }
            else
            {
                typeSpan = new Span(lineNo, indent + typeStart, indent + typeStart + type.Length);
            }
        }

        return new DesignerObject(library, typeName, typeSpan, name, new Span(lineNo, indent + nameStart, indent + pos), depth);
    }

    private static void SkipSpaces(string line, ref int pos)
    {
        while (pos < line.Length && char.IsWhiteSpace(line[pos]))
        {
            pos++;
        }
    }
}
