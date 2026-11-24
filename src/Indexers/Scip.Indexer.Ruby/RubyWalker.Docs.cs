using System.Text;

namespace Scip.Indexer.Ruby;

internal enum ParamKind { Required, Optional, Rest, KeyRequired, KeyOptional, KeyRest, Block }

/// <summary>One parameter of a <c>def</c>, as needed to render upstream's hover signature.</summary>
internal sealed record ParamSpec(string Name, ParamKind Kind);

/// <summary>
/// Hover documentation in upstream scip-ruby's format: a <c>```ruby</c> block (Sorbet's pretty-printed signature for
/// methods, <c>class A &lt; B</c>, <c>name (Type)</c> for variables and parameters) followed by the doc comment.
/// Types come from the <c>sig</c> when there is one and are <c>T.untyped</c> otherwise.
/// </summary>
internal sealed partial class RubyWalker
{
    private const int MaxPrettySigArgs = 4;
    private const int MaxPrettyWidth = 80;
    private const string Untyped = "T.untyped";

    /// <summary>Attaches the hover docs of a definition. <paramref name="markdown"/> is resolved after all files are walked.</summary>
    private Occ WithDocs(Occ occ, Func<RubyProject, string?>? markdown, bool comment = true)
    {
        occ.Markdown = markdown;
        if (comment)
        {
            occ.Comment = FindComment(occ.Line);
        }

        return occ;
    }

    private Occ WithVarDocs(Occ occ, string name, string type, bool comment = false)
    {
        var md = $"{name} ({type})";
        return WithDocs(occ, _ => md, comment);
    }

    private static bool Starts(string line, string prefix) => line.StartsWith(prefix, StringComparison.Ordinal);

    /// <summary>Port of the LSP <c>findDocumentation</c> used by upstream: comment lines directly above (skipping a <c>sig</c>).</summary>
    private string? FindComment(int defLine)
    {
        var lines = _lines ??= _doc.Text.Split('\n');
        var docs = new List<string>();
        for (var k = Math.Min(defLine, lines.Length) - 1; k >= 0; k--)
        {
            var line = lines[k].Trim();
            if (line.Length == 0)
            {
                break;
            }

            if (Starts(line, "sig"))
            {
                continue;
            }

            if (line[0] == '}')
            {
                k--;
                while (k >= 0 && !(Starts(lines[k].Trim(), "sig {") || Starts(lines[k].Trim(), "sig(:final) {")) && !Starts(lines[k].Trim(), "}"))
                {
                    k--;
                }

                if (k < 0 || Starts(lines[k].Trim(), "}"))
                {
                    break;
                }

                continue;
            }

            if (Starts(line, "end"))
            {
                k--;
                while (k >= 0 && !(Starts(lines[k].Trim(), "sig do") || Starts(lines[k].Trim(), "sig(:final) do")) && lines[k].Trim() != "end")
                {
                    k--;
                }

                if (k < 0 || lines[k].Trim() == "end")
                {
                    break;
                }

                var opener = lines[k].Trim();
                if ((Starts(opener, "sig do;") || Starts(opener, "sig(:final) do;")) && opener.EndsWith("end", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            if (line[0] == '#' && !Starts(line, "# typed:"))
            {
                var comment = line[(Starts(line, "# ") ? 2 : 1)..];
                docs.Add(comment);
                if (comment.StartsWith('@'))
                {
                    docs.Add("");
                }

                continue;
            }

            break;
        }

        docs.Reverse();
        var text = string.Join('\n', docs).TrimEnd();
        return text.Length == 0 ? null : text;
    }

    // ---- sig text -----------------------------------------------------------------------------------------

    /// <summary>Source text of the tokens in [from, to), with single spaces where the source had whitespace.</summary>
    private string TokenText(int from, int to)
    {
        var sb = new StringBuilder();
        for (var k = from; k < to && k < _t.Count; k++)
        {
            if (_t[k].Kind is Tk.Newline or Tk.Eof)
            {
                continue;
            }

            if (sb.Length > 0 && _t[k].SpaceBefore)
            {
                sb.Append(' ');
            }

            sb.Append(_t[k].Text);
        }

        return sb.ToString();
    }

    // ---- parameters ---------------------------------------------------------------------------------------

    /// <summary>Splits the parameter list tokens in [start, end) into <see cref="ParamSpec"/>s.</summary>
    private List<ParamSpec> ExtractParams(int start, int end)
    {
        var result = new List<ParamSpec>();
        var seg = new List<int>();
        var depth = 0;
        void Flush()
        {
            if (seg.Count > 0)
            {
                AddParam(result, seg);
            }

            seg.Clear();
        }

        for (var k = start; k < end && k < _t.Count; k++)
        {
            var t = _t[k];
            if (t.Kind is Tk.Newline or Tk.Eof)
            {
                continue;
            }

            if (t is { Kind: Tk.Punct, Text: "(" or "[" or "{" })
            {
                depth++;
            }
            else if (t is { Kind: Tk.Punct, Text: ")" or "]" or "}" })
            {
                depth--;
            }

            if (depth == 0 && t is { Kind: Tk.Punct, Text: "," or ";" })
            {
                Flush();
                continue;
            }

            seg.Add(k);
        }

        Flush();
        return result;
    }

    private void AddParam(List<ParamSpec> into, List<int> seg)
    {
        var first = _t[seg[0]];
        var second = seg.Count > 1 ? _t[seg[1]] : new Token(Tk.Eof, "", 0, 0, 0, false);
        switch (first)
        {
            case { Kind: Tk.Punct, Text: "*" }:
                into.Add(new ParamSpec(second.Kind == Tk.Ident ? second.Text : "*", ParamKind.Rest));
                break;
            case { Kind: Tk.Punct, Text: "**" }:
                if (second.Kind == Tk.Ident || seg.Count == 1)
                {
                    into.Add(new ParamSpec(second.Kind == Tk.Ident ? second.Text : "**", ParamKind.KeyRest));
                }

                break;
            case { Kind: Tk.Punct, Text: "&" }:
                into.Add(new ParamSpec(second.Kind == Tk.Ident ? second.Text : "&", ParamKind.Block));
                break;
            case { Kind: Tk.Punct, Text: "..." }:
                into.Add(new ParamSpec("*", ParamKind.Rest));
                into.Add(new ParamSpec("**", ParamKind.KeyRest));
                into.Add(new ParamSpec("&", ParamKind.Block));
                break;
            case { Kind: Tk.Label }:
                into.Add(new ParamSpec(first.Text, seg.Count > 1 ? ParamKind.KeyOptional : ParamKind.KeyRequired));
                break;
            case { Kind: Tk.Ident }:
                into.Add(new ParamSpec(first.Text, second is { Kind: Tk.Punct, Text: "=" } ? ParamKind.Optional : ParamKind.Required));
                break;
            case { Kind: Tk.Punct, Text: "(" }:
                into.Add(new ParamSpec($"arg{into.Count}", ParamKind.Required));
                break;
        }
    }

    // ---- method signature rendering -----------------------------------------------------------------------

    private static string PrettyParamName(string name) => name is "*" or "**" or "&" ? $"\"{name}\"" : name;

    /// <summary>Port of Sorbet's <c>prettyTypeForMethod</c> (as post-processed by scip-ruby).</summary>
    internal static string PrettyMethod(string name, bool singleton, IReadOnlyList<ParamSpec> parameters, SigInfo? sig)
    {
        var typed = new List<string>();
        foreach (var p in parameters)
        {
            var type = sig is not null && sig.ParamText.TryGetValue(p.Name, out var declared) ? declared : Untyped;
            typed.Add($"{PrettyParamName(p.Name)}: {type}");
        }

        var ret = sig?.RetText is { } r ? (r == "void" ? "void" : $"returns({r})") : $"returns({Untyped})";
        var flags = new List<string>();
        if (sig is not null)
        {
            if (sig.Abstract)
            {
                flags.Add("abstract");
            }

            if (sig.Overridable)
            {
                flags.Add("overridable");
            }

            if (sig.Override)
            {
                flags.Add("override");
            }
        }

        var sigCall = sig is { Final: true } ? "sig(:final)" : "sig";
        var flagString = flags.Count > 0 ? string.Join('.', flags) + "." : "";
        var typeParams = sig is { TypeParameters.Length: > 0 } ? $"type_parameters({string.Join(", ", sig.TypeParameters)})." : "";
        var paramsString = typed.Count > 0 ? $"params({string.Join(", ", typed)})." : "";
        string sigText;
        var oneline = sigCall.Length + flagString.Length + paramsString.Length + ret.Length + 5;
        if (oneline <= MaxPrettyWidth && typed.Count <= MaxPrettySigArgs)
        {
            sigText = $"{sigCall} {{ {flagString}{typeParams}{paramsString}{ret} }}";
        }
        else
        {
            if (sig is { TypeParameters.Length: > 0 })
            {
                typeParams = $"type_parameters({string.Join(", ", sig.TypeParameters)})\n  .";
            }

            if (flags.Count > 0)
            {
                flagString = string.Join("\n  .", flags) + "\n  .";
            }

            if (typed.Count > 0)
            {
                paramsString = $"params(\n    {string.Join(",\n    ", typed)}\n  )\n  .";
            }

            sigText = $"{sigCall} do\n  {flagString}{typeParams}{paramsString}{ret}\nend";
        }

        var pretty = new List<string>();
        foreach (var p in parameters)
        {
            var (prefix, suffix) = p.Kind switch
            {
                ParamKind.Rest => ("*", ""),
                ParamKind.KeyRest => ("**", ""),
                ParamKind.KeyRequired => ("", ":"),
                ParamKind.KeyOptional => ("", ": …"),
                ParamKind.Block => ("&", ""),
                ParamKind.Optional => ("", "=…"),
                _ => ("", ""),
            };
            var pname = prefix.Length > 0 && p.Name == prefix ? "" : p.Name;
            pretty.Add($"{prefix}{pname}{suffix}");
        }

        var head = $"def {(singleton ? "self." : "")}{name}";
        string def;
        if (pretty.Count == 0)
        {
            def = head;
        }
        else
        {
            def = $"{head}({string.Join(", ", pretty)})";
            if (def.Length + 5 >= MaxPrettyWidth)
            {
                def = $"{head}(\n  {string.Join(",\n  ", pretty)}\n)\nend";
            }
        }

        return $"{sigText}\n{def}";
    }

    // ---- variable types -----------------------------------------------------------------------------------

    /// <summary>The Sorbet literal type of the value assigned at token <paramref name="rhs"/> when it is a lone literal.</summary>
    private string LiteralType(int rhs)
    {
        if (!TypedFile)
        {
            return Untyped;
        }

        var t = Tok(rhs);
        var after = Tok(rhs + 1);
        if (after.Kind is not (Tk.Newline or Tk.Eof) && after is not { Kind: Tk.Punct, Text: ";" })
        {
            return Untyped;
        }

        return t.Kind switch
        {
            Tk.Str when RawText(t) is { Length: >= 2 } raw && raw[0] is '"' or '\'' && !raw.Contains("#{", StringComparison.Ordinal) => $"String({raw.Replace('\'', '"')})",
            Tk.Num when long.TryParse(t.Text.Replace("_", ""), out var n) => $"Integer({n})",
            Tk.Num when t.Text.Contains('.') && double.TryParse(t.Text.Replace("_", ""), System.Globalization.CultureInfo.InvariantCulture, out _) => $"Float({t.Text})",
            Tk.Symbol => $"Symbol({(t.Text.StartsWith(':') ? t.Text : ":" + t.Text)})",
            Tk.Keyword when t.Text == "nil" => "NilClass",
            Tk.Keyword when t.Text == "true" => "TrueClass",
            Tk.Keyword when t.Text == "false" => "FalseClass",
            _ => Untyped,
        };
    }

    /// <summary>The type shown for a variable whose name token is at <paramref name="nameIndex"/> (`x = rhs`).</summary>
    private string AssignedType(int nameIndex) =>
        Tok(nameIndex + 1) is { Kind: Tk.Punct, Text: "=" } ? LiteralType(nameIndex + 2) : Untyped;
}

internal sealed partial class RubyWalker
{
    private bool? _typedFile;

    /// <summary>Sorbet infers literal types of locals and fields only in files with <c># typed: true</c> or stricter.</summary>
    private bool TypedFile => _typedFile ??= ComputeTypedFile();

    private bool ComputeTypedFile()
    {
        foreach (var line in _doc.Text.Split('\n').Take(30))
        {
            var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*#\s*typed:\s*(\w+)");
            if (m.Success)
            {
                return m.Groups[1].Value is "true" or "strict" or "strong";
            }
        }

        return false;
    }

    /// <summary>Type of a constant's value as the resolver sees it: the literal's class, or a tuple of classes.</summary>
    private string ConstType(int rhs)
    {
        var end = rhs;
        var depth = 0;
        for (; end < _t.Count && _t[end].Kind is not (Tk.Newline or Tk.Eof); end++)
        {
            if (_t[end] is { Kind: Tk.Punct, Text: "(" or "[" or "{" })
            {
                depth++;
            }
            else if (_t[end] is { Kind: Tk.Punct, Text: ")" or "]" or "}" })
            {
                depth--;
            }
            else if (depth == 0 && _t[end] is { Kind: Tk.Punct, Text: ";" })
            {
                break;
            }
        }

        // Ignore a trailing `.freeze`.
        if (end - 2 > rhs && _t[end - 1] is { Kind: Tk.Ident, Text: "freeze" } && _t[end - 2] is { Kind: Tk.Punct, Text: "." })
        {
            end -= 2;
        }

        if (end == rhs + 1)
        {
            return ScalarClass(_t[rhs]);
        }

        if (_t[rhs] is { Kind: Tk.Punct, Text: "[" } && _t[end - 1] is { Kind: Tk.Punct, Text: "]" })
        {
            var elems = new List<string>();
            var k = rhs + 1;
            while (k < end - 1)
            {
                var c = ScalarClass(_t[k]);
                if (c == Untyped)
                {
                    return Untyped;
                }

                elems.Add(c);
                k++;
                if (k < end - 1)
                {
                    if (_t[k] is not { Kind: Tk.Punct, Text: "," })
                    {
                        return Untyped;
                    }

                    k++;
                }
            }

            return $"[{string.Join(", ", elems)}]";
        }

        return Untyped;
    }

    private string ScalarClass(Token t)
    {
        var raw = t.Kind == Tk.Str ? RawText(t) : t.Text;
        switch (t.Kind)
        {
            case Tk.Str when raw.Length >= 2 && raw[0] is '"' or '\'' && !raw.Contains("#{", StringComparison.Ordinal):
                return "String";
            case Tk.Str when raw.StartsWith("%i[", StringComparison.Ordinal) || raw.StartsWith("%w[", StringComparison.Ordinal):
                {
                    var words = raw[3..^1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    return words.Length == 0 ? Untyped : $"[{string.Join(", ", words.Select(_ => raw[1] == 'i' ? "Symbol" : "String"))}]";
                }
            case Tk.Num:
                return t.Text.Contains('.') && !t.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? "Float" : "Integer";
            case Tk.Symbol:
                return "Symbol";
            case Tk.Keyword when t.Text == "nil":
                return "NilClass";
            case Tk.Keyword when t.Text == "true":
                return "TrueClass";
            case Tk.Keyword when t.Text == "false":
                return "FalseClass";
            default:
                return Untyped;
        }
    }
}
