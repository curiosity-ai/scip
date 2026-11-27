using System.Text;
using System.Text.RegularExpressions;
using ScipSharp.Indexer.Delphi.Semantics;
using ScipSharp.Indexer.Delphi.Syntax;
using Kind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Delphi.Emit;

/// <summary>
/// Turns collected occurrences into SCIP documents: SymbolInformation (kind, display name, signature, XMLDoc
/// documentation, relationships) for every definition and local, plus external symbols for everything referenced
/// but not defined in the project.
/// </summary>
internal sealed partial class DocumentAssembler(Model model, OccurrenceSink sink, IReadOnlyDictionary<int, string> texts)
{
    private const string SignatureLanguage = "delphi";

    public void Emit(Action<Document> onDocument, Action<SymbolInformation> onExternal)
    {
        // Relationships first: their targets must have SymbolInformation somewhere.
        var relationships = new Dictionary<Entity, List<Relationship>>(ReferenceEqualityComparer.Instance);
        foreach (var e in sink.DefinitionDoc.Keys.ToList())
        {
            var rels = Relationships(e);
            if (rels.Count > 0)
            {
                relationships[e] = rels;
            }
        }

        var docSymbols = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in sink.DefinitionDoc.Keys)
        {
            docSymbols.Add(SymbolNames.Global(e));
        }

        // The same unit may exist in several files (copies in tests/examples): relationships are emitted once per symbol.
        var withRelationships = new HashSet<string>(StringComparer.Ordinal);
        foreach (var doc in sink.Documents.Values.OrderBy(d => d.RelativePath, StringComparer.Ordinal))
        {
            var document = new Document
            {
                RelativePath = doc.RelativePath,
                Language = doc.Language,
                PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
            };
            document.Occurrences.AddRange(doc.Occurrences.OrderBy(o => o.GetSourceRange()?.Start.Line ?? 0)
                .ThenBy(o => o.GetSourceRange()?.Start.Character ?? 0));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in doc.Definitions)
            {
                var info = Info(e, SymbolNames.Global(e));
                if (relationships.TryGetValue(e, out var rels) && withRelationships.Add(info.Symbol))
                {
                    info.Relationships.AddRange(rels);
                }

                if (seen.Add(info.Symbol))
                {
                    document.Symbols.Add(info);
                }
            }

            foreach (var (local, symbol) in doc.Locals)
            {
                if (seen.Add(symbol))
                {
                    document.Symbols.Add(Info(local, symbol));
                }
            }

            onDocument(document);
        }

        var external = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in sink.Referenced.OrderBy(SymbolNames.Global, StringComparer.Ordinal))
        {
            if (e.IsLocal || sink.DefinitionDoc.ContainsKey(e))
            {
                continue;
            }

            var symbol = SymbolNames.Global(e);
            if (docSymbols.Contains(symbol) || !external.Add(symbol))
            {
                continue;
            }

            onExternal(Info(e, symbol));
        }
    }

    // -------------------------------------------------------------------------------------------------------------
    // Relationships

    private void AddRel(List<Relationship> list, Entity target, bool implementation, bool reference)
    {
        if (target.IsLocal)
        {
            return;
        }

        var symbol = SymbolNames.Global(target);
        if (list.Any(r => r.Symbol == symbol))
        {
            return;
        }

        list.Add(new Relationship { Symbol = symbol, IsImplementation = implementation, IsReference = reference });
        if (!sink.DefinitionDoc.ContainsKey(target))
        {
            sink.Referenced.Add(target);
        }
    }

    private List<Relationship> Relationships(Entity e)
    {
        var list = new List<Relationship>();
        if (e.IsLocal)
        {
            return list;
        }

        if (e.IsStructured)
        {
            foreach (var anc in model.Ancestors(e))
            {
                if (anc is NamedTy a && (e.AncestorSyntax.Count > 0 || !a.Decl.IsExternal))
                {
                    AddRel(list, a.Decl, implementation: true, reference: false);
                }
            }
        }

        if (e.IsRoutine && e.Parent is { IsStructured: true } owner)
        {
            if (e.Directives.Contains("override"))
            {
                if (model.InheritedMember(owner, e.Name, e.Params.Count) is { IsRoutine: true } overridden && !ReferenceEquals(overridden, e))
                {
                    AddRel(list, overridden, implementation: true, reference: true);
                }
            }

            // Methods implementing interface methods by name.
            if (owner.Kind is EntityKind.Class or EntityKind.Record or EntityKind.Object)
            {
                foreach (var intf in Interfaces(owner))
                {
                    foreach (var m in intf.Members.Get(e.Name))
                    {
                        if (m.IsRoutine && (m.Params.Count == e.Params.Count || intf.Members.Get(e.Name).Count == 1))
                        {
                            AddRel(list, m, implementation: true, reference: false);
                        }
                    }
                }
            }
        }

        if (sink.ExtraRelationships.TryGetValue(e, out var extra))
        {
            foreach (var (target, impl, reference) in extra)
            {
                AddRel(list, target, impl, reference);
            }
        }

        return list;
    }

    /// <summary>Interfaces a class declares (directly or through interface inheritance; not through ancestor classes).</summary>
    private List<Entity> Interfaces(Entity cls)
    {
        var result = new List<Entity>();
        var pending = new Stack<Entity>();
        foreach (var a in model.Ancestors(cls))
        {
            if (a is NamedTy { Decl.Kind: EntityKind.Interface } i)
            {
                pending.Push(i.Decl);
            }
        }

        var visited = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        while (pending.Count > 0)
        {
            var i = pending.Pop();
            if (!visited.Add(i) || i.Name is "IInterface" or "IUnknown" && ReferenceEquals(i.Unit, model.SystemUnit))
            {
                continue;
            }

            result.Add(i);
            foreach (var a in model.Ancestors(i))
            {
                if (a is NamedTy { Decl.Kind: EntityKind.Interface } p)
                {
                    pending.Push(p.Decl);
                }
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------------------------------------------
    // SymbolInformation

    private SymbolInformation Info(Entity e, string symbol)
    {
        var info = new SymbolInformation
        {
            Symbol = symbol,
            Kind = KindOf(e),
            DisplayName = e.IsUnitLike ? string.Join(".", e.UnitSegments) : e.Name,
        };
        if (Signature(e) is { Length: > 0 } sig)
        {
            info.SignatureDocumentation = new Signature { Language = SignatureLanguage, Text = sig };
        }

        if (FormatDoc(e.Doc) is { Length: > 0 } doc)
        {
            info.Documentation.Add(doc);
        }

        return info;
    }

    private Kind KindOf(Entity e) => e.Kind switch
    {
        EntityKind.Unit or EntityKind.Program or EntityKind.Library => Kind.Module,
        EntityKind.Package => Kind.Package,
        EntityKind.Class or EntityKind.Object => Kind.Class,
        EntityKind.Record => Kind.Struct,
        EntityKind.Interface => Kind.Interface,
        EntityKind.ClassHelper or EntityKind.RecordHelper => Kind.Extension,
        EntityKind.Enum => Kind.Enum,
        EntityKind.EnumMember => Kind.EnumMember,
        EntityKind.Alias or EntityKind.SetType or EntityKind.ArrayType or EntityKind.PointerType or EntityKind.ClassRef or EntityKind.Subrange => Kind.TypeAlias,
        EntityKind.OtherType => Kind.Type,
        EntityKind.ProcType => Kind.Delegate,
        EntityKind.TypeParam => Kind.TypeParameter,
        EntityKind.Field => e.IsClassMember ? Kind.StaticField : Kind.Field,
        EntityKind.Property => IsEvent(e) ? Kind.Event : e.IsClassMember ? Kind.StaticProperty : Kind.Property,
        EntityKind.Method => e.RoutineKind switch
        {
            RoutineKind.Constructor => Kind.Constructor,
            RoutineKind.Operator => Kind.Operator,
            _ when e.Directives.Contains("abstract") => Kind.AbstractMethod,
            _ when e.IsClassMember && e.Directives.Contains("static") => Kind.StaticMethod,
            _ => Kind.Method,
        },
        EntityKind.Routine => Kind.Function,
        EntityKind.Const => Kind.Constant,
        EntityKind.Var => Kind.Variable,
        EntityKind.Param => Kind.Parameter,
        _ => Kind.UnspecifiedKind,
    };

    private bool IsEvent(Entity property) =>
        model.TypeOf(property) is NamedTy { Decl.Kind: EntityKind.ProcType } t && t.Decl.TypeSyntax is ProcTypeNode { OfObject: true };

    private string? Signature(Entity e)
    {
        if (e.IsUnitLike)
        {
            var keyword = e.Kind switch
            {
                EntityKind.Program => "program",
                EntityKind.Library => "library",
                EntityKind.Package => "package",
                _ => "unit",
            };
            return $"{keyword} {string.Join(".", e.UnitSegments)}";
        }

        if (e.Source is not { } src)
        {
            return null;
        }

        var t = src.Tokens;
        switch (e.Decl)
        {
            case TypeDecl td when e.Kind != EntityKind.TypeParam:
            {
                var end = td.End;
                if (td.Type is StructTypeNode s)
                {
                    end = s.Ancestors.Count > 0 ? s.Ancestors[^1].End : s.HelperFor?.End ?? s.Start + 1;
                    while (end < t.Count && (t[end].IsSymbol(")") || t[end].IsWord("abstract") || t[end].IsWord("sealed")))
                    {
                        end++;
                        if (t[end - 1].IsSymbol(")"))
                        {
                            break;
                        }
                    }
                }
                else if (end > 0 && t[end - 1].IsSymbol(";"))
                {
                    end--;
                }

                return Slice(t, td.NameTok, end) is { } text ? "type " + text : null;
            }

            case RoutineDecl rd when e.IsRoutine:
            {
                var start = rd.Start;
                while (start < rd.HeadingEnd && !(t[start].IsKeyword("class") || t[start].IsKeyword("procedure") || t[start].IsKeyword("function") ||
                                                    t[start].IsKeyword("constructor") || t[start].IsKeyword("destructor") || t[start].IsWord("operator")))
                {
                    start++;
                }

                var sig = Slice(t, start, rd.HeadingEnd);
                var directives = rd.Directives.Where(d => d is "overload" or "override" or "virtual" or "dynamic" or "abstract" or "static" or "reintroduce" or "final" or "inline" or "deprecated")
                    .Order(StringComparer.Ordinal).ToList();
                if (sig is not null && directives.Count > 0)
                {
                    sig = sig.TrimEnd(';') + "; " + string.Join("; ", directives);
                }

                return sig;
            }

            case PropertyDecl pd when e.Kind == EntityKind.Property:
            {
                var start = pd.Start;
                while (start < pd.NameTok && !(t[start].IsKeyword("class") || t[start].IsKeyword("property")))
                {
                    start++;
                }

                var end = pd.End;
                for (var i = pd.NameTok; i < pd.End; i++)
                {
                    if (t[i].IsSymbol(";"))
                    {
                        end = i;
                        break;
                    }
                }

                return Slice(t, start, end);
            }

            case ConstDecl cd when e.Kind == EntityKind.Const:
            {
                var end = cd.End > 0 && t[cd.End - 1].IsSymbol(";") ? cd.End - 1 : cd.End;
                var text = Slice(t, cd.NameTok, end);
                return text is null ? null : (cd.IsResourceString ? "resourcestring " : "const ") + text;
            }
        }

        if (e.Kind is EntityKind.Var or EntityKind.Field or EntityKind.Param or EntityKind.Const)
        {
            var type = e.TypeSyntax is { } ts ? Slice(t, ts.Start, ts.End) : null;
            var prefix = e.Kind == EntityKind.Param && e.ParamModifier is { } mod ? mod + " " : e.Kind == EntityKind.Var && !e.IsLocal ? "var " : "";
            if (e.IsClassMember && e.Kind == EntityKind.Field)
            {
                prefix = "class var ";
            }

            var sig = type is null ? prefix + e.Name : $"{prefix}{e.Name}: {type}";
            if (e.DefaultValue is { } dv && Slice(t, dv.Start, dv.End) is { } dvText)
            {
                sig += " = " + dvText;
            }

            return sig;
        }

        if (e.Kind == EntityKind.EnumMember && e.Parent is { } en)
        {
            return en.Name.Length > 0 ? $"{en.Name}.{e.Name}" : e.Name;
        }

        if (e.Kind == EntityKind.TypeParam)
        {
            return e.Name;
        }

        return null;
    }

    private string? Slice(IReadOnlyList<Token> t, int start, int endExclusive)
    {
        if (start < 0 || endExclusive <= start || start >= t.Count)
        {
            return null;
        }

        var first = t[start];
        var last = t[Math.Min(endExclusive, t.Count) - 1];
        string text;
        if (first.File == last.File && texts.TryGetValue(first.File, out var src) && last.End <= src.Length && first.Start <= last.End)
        {
            text = src[first.Start..last.End];
        }
        else if (first.File < -1)
        {
            // stub tokens: rebuild from tokens
            var b = new StringBuilder();
            for (var i = start; i < endExclusive && i < t.Count; i++)
            {
                if (b.Length > 0 && NeedsSpace(t[i - 1], t[i]))
                {
                    b.Append(' ');
                }

                b.Append(t[i].Kind == TokenKind.String ? "'" + t[i].Text.Replace("'", "''", StringComparison.Ordinal) + "'" : t[i].Text);
            }

            text = b.ToString();
        }
        else
        {
            return null;
        }

        text = Whitespace().Replace(StripComments(text), " ").Trim();
        return text.Length > 500 ? text[..500] + " …" : text;
    }

    private static bool NeedsSpace(Token prev, Token cur)
    {
        if (cur.IsSymbol(",") || cur.IsSymbol(";") || cur.IsSymbol(")") || cur.IsSymbol("]") || cur.IsSymbol(".") || cur.IsSymbol(":") ||
            cur.IsSymbol("(") && prev.IsIdentifier || cur.IsSymbol("<") || cur.IsSymbol(">") || cur.IsSymbol("[") && prev.IsIdentifier)
        {
            return false;
        }

        return !(prev.IsSymbol("(") || prev.IsSymbol("[") || prev.IsSymbol(".") || prev.IsSymbol("^") || prev.IsSymbol("<"));
    }

    private static string StripComments(string text)
    {
        if (!text.Contains('{', StringComparison.Ordinal) && !text.Contains("//", StringComparison.Ordinal) && !text.Contains("(*", StringComparison.Ordinal))
        {
            return text;
        }

        var b = new StringBuilder(text.Length);
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'')
            {
                inString = !inString;
                b.Append(c);
                continue;
            }

            if (!inString)
            {
                if (c == '{')
                {
                    var end = text.IndexOf('}', i);
                    i = end < 0 ? text.Length : end;
                    b.Append(' ');
                    continue;
                }

                if (c == '(' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    var end = text.IndexOf("*)", i + 2, StringComparison.Ordinal);
                    i = end < 0 ? text.Length : end + 1;
                    b.Append(' ');
                    continue;
                }

                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    var end = text.IndexOf('\n', i);
                    i = end < 0 ? text.Length : end;
                    b.Append(' ');
                    continue;
                }
            }

            b.Append(c);
        }

        return b.ToString();
    }

    /// <summary>Renders XMLDoc (<c>/// &lt;summary&gt;…</c>) comment lines as Markdown-ish text.</summary>
    internal static string? FormatDoc(string? doc)
    {
        if (string.IsNullOrWhiteSpace(doc))
        {
            return null;
        }

        var lines = doc.Split('\n').Select(l => l.Trim());
        var text = string.Join("\n", lines).Trim();
        text = ParamTag().Replace(text, m => $"\n- `{m.Groups[1].Value}`: {m.Groups[2].Value.Trim()}");
        text = ReturnsTag().Replace(text, m => $"\nReturns: {m.Groups[1].Value.Trim()}");
        text = RemarksTag().Replace(text, m => $"\n\n{m.Groups[1].Value.Trim()}");
        text = SeeTag().Replace(text, m => $"`{m.Groups[1].Value}`");
        text = AnyTag().Replace(text, "");
        var result = string.Join("\n", text.Split('\n').Select(l => l.Trim())).Trim();
        while (result.Contains("\n\n\n", StringComparison.Ordinal))
        {
            result = result.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
        }

        return result.Length == 0 ? null : result;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"<param\s+name\s*=\s*""([^""]*)""\s*>(.*?)</param>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ParamTag();

    [GeneratedRegex(@"<returns>(.*?)</returns>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ReturnsTag();

    [GeneratedRegex(@"<remarks>(.*?)</remarks>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex RemarksTag();

    [GeneratedRegex(@"<see\s+cref\s*=\s*""([^""]*)""\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex SeeTag();

    [GeneratedRegex(@"</?[A-Za-z][^>]*>")]
    private static partial Regex AnyTag();
}
