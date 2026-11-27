using System.Text.RegularExpressions;
using ScipSharp.Indexer.Pascal.Semantics;
using ScipSharp.Indexer.Pascal.Syntax;

namespace ScipSharp.Indexer.Pascal.Project;

/// <summary>
/// Indexes a text Lazarus form (<c>.lfm</c>): <c>object Name: TClass</c> lines reference the form class / component
/// field and the component type, <c>OnXxx = Handler</c> lines reference the event handler methods of the form class.
/// </summary>
internal sealed partial class LfmIndexer(Workspace ws)
{
    [GeneratedRegex(@"^(?<indent>\s*)(?<kw>object|inherited|inline)\s+(?:(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*)?(?<cls>[A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectLine();

    [GeneratedRegex(@"^\s*(?<prop>On[A-Za-z0-9_]+)\s*=\s*(?<handler>[A-Za-z_][A-Za-z0-9_]*)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex EventLine();

    [GeneratedRegex(@"^\s*(end|item)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EndOrItem();

    public void Index(string path)
    {
        var unitPath = SourceExtensionsFor(path).FirstOrDefault(File.Exists);
        var unit = unitPath is null ? null : ws.UnitAt(unitPath);
        SourceText src;
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 4 && bytes[0] == (byte)'T' && bytes[1] == (byte)'P' && bytes[2] == (byte)'F')
            {
                return; // binary form
            }

            src = SourceText.Decode(path, bytes, System.Text.Encoding.Latin1);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }

        if (unit?.Sym is null || ws.Emit.DocFor(src, "lfm") is null)
        {
            return;
        }

        var used = ws.UsedUnitsOf(unit);
        PType? formType = null;
        var depth = 0;
        var inBinary = false;
        for (var line = 0; line < src.LineCount; line++)
        {
            var start = src.LineStart(line);
            var end = line + 1 < src.LineCount ? src.LineStart(line + 1) : src.Text.Length;
            var text = src.Text[start..end].TrimEnd('\r', '\n');
            if (inBinary)
            {
                if (text.Contains('}', StringComparison.Ordinal))
                {
                    inBinary = false;
                }

                continue;
            }

            if (text.TrimEnd().EndsWith('{') || (text.Contains('{', StringComparison.Ordinal) && !text.Contains('}', StringComparison.Ordinal) && text.Contains('=', StringComparison.Ordinal)))
            {
                inBinary = true;
                continue;
            }

            var m = ObjectLine().Match(text);
            if (m.Success)
            {
                var clsGroup = m.Groups["cls"];
                var nameGroup = m.Groups["name"];
                var typeSym = FindType(clsGroup.Value, unit, used);
                if (typeSym is not null)
                {
                    Ref(src, start + clsGroup.Index, clsGroup.Length, typeSym);
                }

                if (depth == 0)
                {
                    formType = typeSym?.Type;
                    if (nameGroup.Success && unit.Interface.Find(nameGroup.Value)?.FirstOrDefault(static s => s.Kind == SymKind.Var) is { } formVar)
                    {
                        Ref(src, start + nameGroup.Index, nameGroup.Length, formVar);
                    }
                }
                else if (nameGroup.Success && formType is not null && ws.Types.FindMember(formType, nameGroup.Value)?.FirstOrDefault(static s => s.Kind == SymKind.Field) is { } field)
                {
                    Ref(src, start + nameGroup.Index, nameGroup.Length, field);
                }

                depth++;
                continue;
            }

            var ev = EventLine().Match(text);
            if (ev.Success && formType is not null)
            {
                var handler = ev.Groups["handler"];
                if (ws.Types.FindMember(formType, handler.Value)?.FirstOrDefault(static s => s.Kind == SymKind.Routine) is { } method)
                {
                    Ref(src, start + handler.Index, handler.Length, method);
                }

                continue;
            }

            if (EndOrItem().Match(text) is { Success: true } e)
            {
                if (e.Groups[1].Value.Equals("item", StringComparison.OrdinalIgnoreCase))
                {
                    depth++;
                }
                else
                {
                    depth = Math.Max(0, depth - 1);
                }
            }
        }
    }

    private void Ref(SourceText src, int start, int length, Sym sym)
    {
        var tok = new Token(TokenKind.Identifier, src.Text.Substring(start, length), src, start, start + length);
        ws.Emit.Reference(tok, sym);
    }

    private Sym? FindType(string name, CompileUnit unit, IReadOnlyList<CompileUnit> used)
    {
        var dot = name.LastIndexOf('.');
        var simple = dot >= 0 ? name[(dot + 1)..] : name;
        foreach (var scope in new[] { unit.Interface, unit.Implementation }.Concat(used.Reverse().Select(static u => u.Interface)).Append(ws.System.Interface))
        {
            if (scope.Find(simple)?.FirstOrDefault(static s => s.Kind == SymKind.Type) is { } t)
            {
                return t;
            }
        }

        return null;
    }

    private static IEnumerable<string> SourceExtensionsFor(string lfm)
    {
        var dir = Path.GetDirectoryName(lfm)!;
        var stem = Path.GetFileNameWithoutExtension(lfm);
        foreach (var ext in Workspace.SourceExtensions)
        {
            yield return Path.Combine(dir, stem + ext);
        }
    }
}
