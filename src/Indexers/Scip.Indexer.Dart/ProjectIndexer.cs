namespace Scip.Indexer.Dart;

/// <summary>Indexes one Dart package: parses every file, resolves references by name, emits documents.</summary>
internal sealed class ProjectIndexer(string root, Action<string>? log)
{
    private const string Scheme = "scip-dart";
    private const string Manager = "pub";

    private static readonly HashSet<string> Keywords =
    [
        "this", "super", "new", "const", "final", "var", "void", "null", "true", "false", "is", "as", "in", "if", "else",
        "for", "while", "do", "switch", "case", "default", "return", "get", "set", "on", "async", "await", "yield",
    ];

    private readonly Dictionary<string, FileModel> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Decl>> _topLevel = new(StringComparer.Ordinal);
    private readonly Dictionary<Decl, Decl> _syntheticCtors = [];
    private readonly Dictionary<string, SymbolInformation> _external = new(StringComparer.Ordinal);
    private Pubspec _pubspec = null!;
    private Dictionary<string, string> _lock = [];

    private string Version => _pubspec.Version ?? ".";

    public DartIndex Build(bool indexPubspec)
    {
        var pubspecPath = Path.Combine(root, "pubspec.yaml");
        if (!File.Exists(pubspecPath))
        {
            throw new InvalidOperationException("Unable to locate pubspec.yaml");
        }

        var pubspecText = File.ReadAllText(pubspecPath);
        _pubspec = Pubspec.Parse(pubspecText);
        var lockPath = Path.Combine(root, "pubspec.lock");
        if (File.Exists(lockPath))
        {
            _lock = Pubspec.ParseLock(File.ReadAllText(lockPath));
        }

        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in DiscoverFiles())
        {
            var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            var text = File.ReadAllText(path);
            sources[rel] = text;
            _files[rel] = DartParser.Parse(rel, text);
            log?.Invoke($"Parsed {rel}");
        }

        foreach (var f in _files.Values)
        {
            foreach (var d in f.TopLevel)
            {
                if (!_topLevel.TryGetValue(d.Name, out var list))
                {
                    _topLevel[d.Name] = list = [];
                }

                list.Add(d);
            }
        }

        var documents = new List<Document>();
        foreach (var rel in _files.Keys.Order(StringComparer.Ordinal))
        {
            documents.Add(BuildDocument(_files[rel], sources[rel]));
        }

        // Implicit (synthetic) default constructors referenced from any file need a SymbolInformation in the declaring file.
        var byPath = documents.ToDictionary(d => d.RelativePath, StringComparer.Ordinal);
        foreach (var ctor in _syntheticCtors.Values)
        {
            if (!ctor.IsLocal && byPath.TryGetValue(ctor.File, out var doc))
            {
                var symbol = SymbolOf(ctor, new LocalSymbols());
                if (!doc.Symbols.Any(x => x.Symbol == symbol))
                {
                    doc.Symbols.Add(new SymbolInformation
                    {
                        Symbol = symbol,
                        Kind = SymbolInformation.Types.Kind.Constructor,
                        SignatureDocumentation = new Signature { Language = "Dart", Text = ctor.Parent!.Name + "()" },
                    });
                }
            }
        }

        if (indexPubspec)
        {
            documents.Add(PubspecIndexer.Index(_pubspec, pubspecText, _lock));
        }

        var metadata = new Metadata
        {
            ProjectRoot = new Uri(root.TrimEnd('/', '\\')).AbsoluteUri,
            TextDocumentEncoding = TextEncoding.Utf16,
            ToolInfo = new ToolInfo { Name = "scip-dart", Version = DartIndexer.ToolVersion },
        };
        return new DartIndex(metadata, documents, [.. _external.Values]);
    }

    private IEnumerable<string> DiscoverFiles()
    {
        var nested = Directory.EnumerateFiles(root, "pubspec.yaml", SearchOption.AllDirectories)
            .Select(p => Path.GetDirectoryName(p)!)
            .Where(d => !string.Equals(Path.GetFullPath(d), root, StringComparison.Ordinal))
            .Select(d => Path.GetFullPath(d) + Path.DirectorySeparatorChar)
            .ToList();

        return Directory.EnumerateFiles(root, "*.dart", SearchOption.AllDirectories)
            .Where(p =>
            {
                var rel = Path.GetRelativePath(root, p);
                return !rel.Split(Path.DirectorySeparatorChar).Any(s => s.StartsWith('.'))
                    && !nested.Any(n => p.StartsWith(n, StringComparison.Ordinal));
            })
            .Order(StringComparer.Ordinal);
    }

    // ---- symbols ----------------------------------------------------------------------------------------

    private static string Format(string package, string version, IEnumerable<(string Name, SymbolDescriptor.Types.Suffix Suffix)> descriptors)
    {
        var symbol = new Symbol
        {
            Scheme = Scheme,
            Package = new Package { Manager = Manager, Name = package, Version = version },
        };
        foreach (var (name, suffix) in descriptors)
        {
            symbol.Descriptors.Add(new SymbolDescriptor { Name = name, Suffix = suffix });
        }

        return SymbolFormatter.Verbose.Format(symbol);
    }

    private static IEnumerable<(string, SymbolDescriptor.Types.Suffix)> Namespace(string relativePath) =>
        relativePath.Split('/').Select(s => (s, SymbolDescriptor.Types.Suffix.Namespace));

    private static string FileSymbol(string package, string version, string relativePath) =>
        Format(package, version, Namespace(relativePath));

    private string SymbolOf(Decl d, LocalSymbols locals) =>
        d.IsLocal
            ? locals.For(d)
            : Format(_pubspec.Name, Version, Namespace(d.File).Concat(d.Path));

    private sealed class LocalSymbols
    {
        private readonly Dictionary<Decl, string> _map = [];

        public string For(Decl d)
        {
            if (!_map.TryGetValue(d, out var s))
            {
                _map[d] = s = $"local {_map.Count}";
            }

            return s;
        }
    }

    // ---- lookups ----------------------------------------------------------------------------------------

    private Decl? LookupTopLevel(string name, FileModel from)
    {
        if (!_topLevel.TryGetValue(name, out var list))
        {
            return null;
        }

        var sameFile = list.FirstOrDefault(d => d.File == from.RelativePath);
        if (sameFile is not null)
        {
            return sameFile;
        }

        return name.StartsWith('_') ? null : list[0];
    }

    private Decl? LookupType(string name, FileModel from) =>
        LookupTopLevel(name, from) is { IsTypeLike: true } d ? d : null;

    private IEnumerable<Decl> SuperTypes(Decl type, FileModel? from = null)
    {
        var seen = new HashSet<Decl> { type };
        var queue = new Queue<Decl>([type]);
        var model = from ?? _files[type.File];
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var name in cur.Supers)
            {
                if (LookupType(name, _files[cur.File]) is { } s && seen.Add(s))
                {
                    queue.Enqueue(s);
                    yield return s;
                }
            }
        }

        _ = model;
    }

    private Decl? FindInstanceMember(Decl cls, string name)
    {
        foreach (var t in SuperTypes(cls).Prepend(cls))
        {
            var m = t.Members.FirstOrDefault(x => x.Name == name && x.Kind is DeclKind.Field or DeclKind.Property or DeclKind.Method);
            if (m is not null)
            {
                return m;
            }
        }

        return null;
    }

    private Decl ConstructorOf(Decl cls, string name)
    {
        var existing = cls.Members.FirstOrDefault(m => m.Kind == DeclKind.Constructor && m.Name == name);
        if (existing is not null)
        {
            return existing;
        }

        var key = cls;
        if (name.Length == 0 && _syntheticCtors.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var d = new Decl(DeclKind.Constructor, name, cls, cls.NameToken, cls.File)
        {
            Path = [.. cls.Path, (name.Length == 0 ? "<constructor>" : name, SymbolDescriptor.Types.Suffix.Method)],
            IsLocal = name.StartsWith('_'),
        };
        if (name.Length == 0)
        {
            _syntheticCtors[key] = d;
        }

        return d;
    }

    private static Decl? EnclosingType(FileModel f, int offset) =>
        f.TopLevel.FirstOrDefault(d => d.IsTypeLike && d.BodyStart >= 0 && offset >= d.BodyStart && offset < d.BodyEnd);

    // ---- document ---------------------------------------------------------------------------------------

    private sealed record Occ(int Start, int Length, Decl? Target, string? Symbol, bool Definition, Decl? Def = null);

    private Document BuildDocument(FileModel f, string source)
    {
        var lines = new LineIndex(source);
        var locals = new LocalSymbols();
        var occs = new List<Occ>();

        foreach (var d in f.AllDecls)
        {
            occs.Add(new Occ(d.NameToken.Offset, d.NameToken.Length, d, null, true, d));
        }

        ResolveReferences(f, occs);
        ResolveDirectives(f, occs);
        ResolveDocReferences(f, occs);

        var ordered = occs
            .OrderBy(o => o.Start)
            .ThenBy(o => o.Definition ? 0 : 1)
            .ToList();

        var fileSymbol = FileSymbol(_pubspec.Name, Version, f.RelativePath);
        var document = new Document
        {
            Language = "Dart",
            RelativePath = f.RelativePath,
            PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
        };

        var fileOcc = new Occurrence
        {
            Symbol = fileSymbol,
            SymbolRoles = (int)SymbolRole.Definition,
            SyntaxKind = SyntaxKind.IdentifierNamespace,
        };
        SetRange(fileOcc, new LineIndex.Range(0, 0, 0, 0));
        document.Occurrences.Add(fileOcc);
        document.Symbols.Add(new SymbolInformation { Symbol = fileSymbol });

        foreach (var o in ordered)
        {
            var symbol = o.Symbol ?? SymbolOf(o.Target!, locals);
            var occ = new Occurrence { Symbol = symbol, SymbolRoles = o.Definition ? (int)SymbolRole.Definition : 0 };
            SetRange(occ, lines.RangeOf(o.Start, o.Length));
            if (o.Def is { } def)
            {
                AddEnclosing(occ, lines.RangeOf(def.EnclosingStart, def.EnclosingEnd - def.EnclosingStart));
            }

            document.Occurrences.Add(occ);
        }

        foreach (var d in f.AllDecls.OrderBy(d => d.NameToken.Offset))
        {
            var info = new SymbolInformation
            {
                Symbol = SymbolOf(d, locals),
                Kind = KindOf(d),
                SignatureDocumentation = new Signature { Language = "Dart", Text = d.Signature },
            };
            if (d.Doc is not null)
            {
                info.Documentation.Add(d.Doc);
            }

            info.Relationships.AddRange(RelationshipsOf(d, f, locals));
            document.Symbols.Add(info);
        }

        return document;
    }

    private static void SetRange(Occurrence occ, LineIndex.Range r)
    {
        if (r.StartLine == r.EndLine)
        {
            occ.SingleLineRange = new SingleLineRange { Line = r.StartLine, StartCharacter = r.StartCharacter, EndCharacter = r.EndCharacter };
        }
        else
        {
            occ.MultiLineRange = new MultiLineRange
            {
                StartLine = r.StartLine,
                StartCharacter = r.StartCharacter,
                EndLine = r.EndLine,
                EndCharacter = r.EndCharacter,
            };
        }
    }

    // The SCIP proto only offers the (deprecated, but still the only) repeated form for enclosing ranges.
#pragma warning disable CS0612, CS0618
    private static void AddEnclosing(Occurrence occ, LineIndex.Range r)
    {
        if (r.StartLine == r.EndLine)
        {
            occ.EnclosingRange.AddRange([r.StartLine, r.StartCharacter, r.EndCharacter]);
        }
        else
        {
            occ.EnclosingRange.AddRange([r.StartLine, r.StartCharacter, r.EndLine, r.EndCharacter]);
        }
    }
#pragma warning restore CS0612, CS0618

    private static SymbolInformation.Types.Kind KindOf(Decl d) => d.Kind switch
    {
        DeclKind.Class => SymbolInformation.Types.Kind.Class,
        DeclKind.Mixin => SymbolInformation.Types.Kind.Mixin,
        DeclKind.Enum => SymbolInformation.Types.Kind.Enum,
        DeclKind.TypeAlias => SymbolInformation.Types.Kind.TypeAlias,
        DeclKind.Extension => SymbolInformation.Types.Kind.Extension,
        DeclKind.Constructor => SymbolInformation.Types.Kind.Constructor,
        DeclKind.Method => SymbolInformation.Types.Kind.Method,
        DeclKind.Function => SymbolInformation.Types.Kind.Function,
        DeclKind.Variable => SymbolInformation.Types.Kind.Variable,
        DeclKind.Property => SymbolInformation.Types.Kind.Property,
        DeclKind.Parameter => SymbolInformation.Types.Kind.Parameter,
        DeclKind.TypeParameter => SymbolInformation.Types.Kind.TypeParameter,
        DeclKind.ImportPrefix => SymbolInformation.Types.Kind.Namespace,
        DeclKind.Field => SymbolInformation.Types.Kind.Field,
        _ => SymbolInformation.Types.Kind.UnspecifiedKind,
    };

    // ---- relationships -----------------------------------------------------------------------------------

    private IEnumerable<Relationship> RelationshipsOf(Decl d, FileModel f, LocalSymbols locals)
    {
        if (d.Kind == DeclKind.Class)
        {
            foreach (var s in SuperTypes(d))
            {
                yield return new Relationship { Symbol = SymbolOf(s, locals), IsImplementation = true };
            }

            yield break;
        }

        if (d.Parent is not { Kind: DeclKind.Class } cls || d.Kind is not (DeclKind.Method or DeclKind.Field or DeclKind.Property))
        {
            yield break;
        }

        foreach (var s in SuperTypes(cls))
        {
            foreach (var m in s.Members)
            {
                var match = d.Kind switch
                {
                    DeclKind.Method => m.Kind == DeclKind.Method && m.Name == d.Name,
                    DeclKind.Field => m.Kind == DeclKind.Field && m.Name == d.Name,
                    _ => m.Kind == DeclKind.Property && m.Name == d.Name && m.IsGetter == d.IsGetter,
                };
                if (match)
                {
                    yield return new Relationship { Symbol = SymbolOf(m, locals), IsImplementation = true, IsReference = true };
                }
            }
        }

        _ = f;
    }

    // ---- references --------------------------------------------------------------------------------------

    private void ResolveReferences(FileModel f, List<Occ> occs)
    {
        var t = f.Tokens;

        foreach (var r in f.ThisFieldRefs)
        {
            var field = r.Class.Members.FirstOrDefault(m => m.Name == r.Name.Text && m.Kind is DeclKind.Field or DeclKind.Property);
            if (field is not null)
            {
                occs.Add(new Occ(r.Name.Offset, r.Name.Length, field, null, false));
            }
        }

        var shadowed = LocallyDeclaredNames(t, f);
        var callables = f.AllDecls.Where(d => d.Kind is DeclKind.Method or DeclKind.Function or DeclKind.Constructor or DeclKind.Property or DeclKind.Variable or DeclKind.Field).ToList();
        var thisRefOffsets = f.ThisFieldRefs.Select(r => r.Name.Offset).ToHashSet();
        for (var k = 0; k < t.Count; k++)
        {
            var tk = t[k];
            if (tk.Kind != TokenKind.Identifier || f.DeclTokenOffsets.Contains(tk.Offset) || thisRefOffsets.Contains(tk.Offset) || Keywords.Contains(tk.Text))
            {
                continue;
            }

            var prev = k > 0 ? t[k - 1] : default;
            var next = k + 1 < t.Count ? t[k + 1] : default;
            var afterDot = prev.Is(".") || prev.Is("?.");
            if (prev.Is("..") || (next.Is(":") && (prev.Is("(") || prev.Is(","))))
            {
                continue; // cascade member or named-argument label; both need type information
            }

            if (!afterDot && f.LookupLocal(tk.Text, tk.Offset) is { } local)
            {
                occs.Add(new Occ(tk.Offset, tk.Length, local, null, false));
                continue;
            }

            if (afterDot)
            {
                var q = k >= 2 ? t[k - 2] : default;
                var beforeQ = k >= 3 ? t[k - 3] : default;
                if (q.Is("this") && !beforeQ.Is(".") && EnclosingType(f, tk.Offset) is { } cls)
                {
                    if (FindInstanceMember(cls, tk.Text) is { } m)
                    {
                        occs.Add(new Occ(tk.Offset, tk.Length, m, null, false));
                    }

                    continue;
                }

                if (q.Kind == TokenKind.Identifier && f.ImportPrefixes.Contains(q.Text) && !beforeQ.Is("."))
                {
                    // `prefix.Name`: fall through to the top-level lookup
                }
                else if (q.Kind == TokenKind.Identifier && !beforeQ.Is(".") && LookupType(q.Text, f) is { } type)
                {
                    var member = type.Members.FirstOrDefault(m => m.Name == tk.Text && (m.Kind == DeclKind.Constructor || m.IsStatic));
                    if (member is not null)
                    {
                        occs.Add(new Occ(tk.Offset, tk.Length, member, null, false));
                    }

                    continue;
                }
                else
                {
                    continue;
                }
            }

            if (!afterDot && next.Is(".") && f.PrefixDecls.TryGetValue(tk.Text, out var prefixDecl))
            {
                occs.Add(new Occ(tk.Offset, tk.Length, prefixDecl, null, false));
                continue;
            }

            var typeParam = afterDot ? null : TypeParameterAt(f, tk.Text, tk.Offset);
            if (typeParam is not null)
            {
                occs.Add(new Occ(tk.Offset, tk.Length, typeParam, null, false));
                continue;
            }

            if (!afterDot && EnclosingType(f, tk.Offset) is { } enclosing && FindInstanceMember(enclosing, tk.Text) is { } own)
            {
                if (!IsShadowed(tk, callables, shadowed))
                {
                    occs.Add(new Occ(tk.Offset, tk.Length, own, null, false));
                }

                continue;
            }

            var target = LookupTopLevel(tk.Text, f);
            if (target is null)
            {
                continue;
            }

            if (!target.IsTypeLike && IsShadowed(tk, callables, shadowed))
            {
                continue; // likely shadowed by a parameter of the same name
            }

            var callIdx = next.Is("<") ? SkipAngleTokens(t, k + 1) : k + 1;
            var callTok = callIdx < t.Count ? t[callIdx] : default;
            if (target.Kind == DeclKind.Extension && callTok.Is("("))
            {
                continue; // extension override `Ext(x).member`; not indexed upstream
            }

            if (target.Kind == DeclKind.Class && callTok.Is("(") && !afterDot)
            {
                occs.Add(new Occ(tk.Offset, tk.Length, ConstructorOf(target, ""), null, false));
            }
            else
            {
                occs.Add(new Occ(tk.Offset, tk.Length, target, null, false));
            }
        }
    }

    /// <summary>Finds a type parameter named <paramref name="name"/> visible at <paramref name="offset"/> (innermost first).</summary>
    private static Decl? TypeParameterAt(FileModel f, string name, int offset)
    {
        foreach (var top in f.TopLevel)
        {
            if (offset < top.EnclosingStart || offset >= top.EnclosingEnd)
            {
                continue;
            }

            foreach (var member in top.Members.Where(m => offset >= m.EnclosingStart && offset < m.EnclosingEnd))
            {
                if (member.TypeParameters.FirstOrDefault(p => p.Name == name) is { } mp)
                {
                    return mp;
                }
            }

            return top.TypeParameters.FirstOrDefault(p => p.Name == name);
        }

        return null;
    }

    /// <summary>True when <paramref name="tk"/> is likely shadowed by a parameter or local variable of the enclosing member.</summary>
    private static bool IsShadowed(Token tk, List<Decl> callables, Dictionary<string, List<(int Start, int End)>> locals)
    {
        var owner = callables
            .Where(c => tk.Offset >= c.EnclosingStart && tk.Offset < c.EnclosingEnd)
            .OrderBy(c => c.EnclosingEnd - c.EnclosingStart)
            .FirstOrDefault();
        if (owner is null)
        {
            return false;
        }

        if (owner.Parameters.Any(p => p.Name == tk.Text))
        {
            return true;
        }

        return locals.TryGetValue(tk.Text, out var ranges) && ranges.Any(r => r.Start <= owner.EnclosingStart && owner.EnclosingEnd <= r.End);
    }

    private static readonly HashSet<string> NotATypeBefore =
        ["return", "case", "else", "await", "throw", "in", "is", "as", "new", "yield", "assert", "do", "default", "if", "while", "for"];

    /// <summary>
    /// Names that look like local variable declarations (<c>Type name =</c>, <c>var name;</c>) outside declaration
    /// headers. Used to avoid resolving such names to same-named top-level declarations.
    /// </summary>
    private static Dictionary<string, List<(int Start, int End)>> LocallyDeclaredNames(List<Token> t, FileModel f)
    {
        var result = new Dictionary<string, List<(int Start, int End)>>();
        for (var k = 1; k + 1 < t.Count; k++)
        {
            var tk = t[k];
            if (tk.Kind != TokenKind.Identifier || f.DeclTokenOffsets.Contains(tk.Offset))
            {
                continue;
            }

            var prev = t[k - 1];
            var next = t[k + 1];
            var prevIsType = (prev.Kind == TokenKind.Identifier && !NotATypeBefore.Contains(prev.Text)) || prev.Is(">") || prev.Is("?");
            if (prevIsType && (next.Is("=") || next.Is(";") || next.Is(",") || next.Is("in")))
            {
                var owner = f.AllDecls
                    .Where(c => c.Kind is DeclKind.Method or DeclKind.Function or DeclKind.Constructor or DeclKind.Property or DeclKind.Variable or DeclKind.Field
                        && tk.Offset >= c.EnclosingStart && tk.Offset < c.EnclosingEnd)
                    .OrderBy(c => c.EnclosingEnd - c.EnclosingStart)
                    .FirstOrDefault();
                if (owner is not null)
                {
                    if (!result.TryGetValue(tk.Text, out var ranges))
                    {
                        result[tk.Text] = ranges = [];
                    }

                    ranges.Add((owner.EnclosingStart, owner.EnclosingEnd));
                }
            }
        }

        return result;
    }

    private static int SkipAngleTokens(List<Token> t, int i)
    {
        var depth = 0;
        for (; i < t.Count; i++)
        {
            if (t[i].Is("<"))
            {
                depth++;
            }
            else if (t[i].Is(">") && --depth == 0)
            {
                return i + 1;
            }
            else if (t[i].Is(";") || t[i].Is("{"))
            {
                break;
            }
        }

        return t.Count;
    }

    /// <summary>Resolves <c>[Name]</c> / <c>[Type.member]</c> references in doc comments against the documented declaration's scope.</summary>
    private void ResolveDocReferences(FileModel f, List<Occ> occs)
    {
        if (f.DocRefs.Count == 0)
        {
            return;
        }

        var docable = f.AllDecls
            .Where(d => d.Kind is DeclKind.Class or DeclKind.Mixin or DeclKind.Enum or DeclKind.TypeAlias or DeclKind.Extension or DeclKind.Constructor
                or DeclKind.Method or DeclKind.Function or DeclKind.Variable or DeclKind.Field or DeclKind.Property
                && (d.Kind != DeclKind.Function || f.TopLevel.Contains(d)))
            .OrderBy(d => d.EnclosingStart)
            .ToList();
        foreach (var segments in f.DocRefs)
        {
            var first = segments[0];
            int lo = 0, hi = docable.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (docable[mid].EnclosingStart <= first.Offset)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            if (lo >= docable.Count)
            {
                continue;
            }

            var target = ResolveDocName(first.Text, docable[lo], f);
            if (target is null)
            {
                continue;
            }

            occs.Add(new Occ(first.Offset, first.Length, target, null, false));
            for (var i = 1; i < segments.Count && target is not null; i++)
            {
                var name = segments[i].Text;
                if (target.Kind == DeclKind.ImportPrefix)
                {
                    target = LookupTopLevel(name, f);
                }
                else if (target.IsTypeLike)
                {
                    target = SuperTypes(target).Prepend(target)
                        .Select(t => t.Members.FirstOrDefault(m => m.Name == name && m.Kind != DeclKind.Parameter))
                        .FirstOrDefault(m => m is not null);
                }
                else
                {
                    target = null;
                }

                if (target is not null)
                {
                    occs.Add(new Occ(segments[i].Offset, segments[i].Length, target, null, false));
                }
            }
        }
    }

    private Decl? ResolveDocName(string name, Decl doc, FileModel f)
    {
        if (doc.Parameters.FirstOrDefault(p => p.Name == name) is { } param)
        {
            return param;
        }

        if (doc.TypeParameters.FirstOrDefault(p => p.Name == name) is { } tp)
        {
            return tp;
        }

        var cls = doc.IsTypeLike ? doc : doc.Parent is { IsTypeLike: true } p ? p : null;
        if (cls is not null)
        {
            if (cls.TypeParameters.FirstOrDefault(x => x.Name == name) is { } ctp)
            {
                return ctp;
            }

            if (FindInstanceMember(cls, name) is { } m)
            {
                return m;
            }
        }

        if (f.PrefixDecls.TryGetValue(name, out var prefix))
        {
            return prefix;
        }

        return LookupTopLevel(name, f);
    }

    private void ResolveDirectives(FileModel f, List<Occ> occs)
    {
        foreach (var directive in f.Directives)
        {
            var uri = StringValue(directive.Uri.Text);
            string? symbol = null;
            if (uri.StartsWith("dart:", StringComparison.Ordinal))
            {
                continue;
            }

            if (uri.StartsWith("package:", StringComparison.Ordinal))
            {
                var rest = uri["package:".Length..];
                var slash = rest.IndexOf('/');
                if (slash < 0)
                {
                    continue;
                }

                var package = rest[..slash];
                var path = "lib/" + rest[(slash + 1)..];
                if (package == _pubspec.Name)
                {
                    symbol = _files.ContainsKey(path) ? FileSymbol(package, Version, path) : null;
                }
                else if (_lock.TryGetValue(package, out var version))
                {
                    symbol = FileSymbol(package, version, path);
                    _external.TryAdd(symbol, new SymbolInformation { Symbol = symbol, Kind = SymbolInformation.Types.Kind.File });
                }
            }
            else if (!uri.Contains(':'))
            {
                var rel = Resolve(f.RelativePath, uri);
                if (_files.ContainsKey(rel))
                {
                    symbol = FileSymbol(_pubspec.Name, Version, rel);
                }
            }

            if (symbol is not null)
            {
                occs.Add(new Occ(directive.Uri.Offset, directive.Uri.Length, null, symbol, false));
            }
        }
    }

    private static string Resolve(string fromFile, string uri)
    {
        var segments = new List<string>(fromFile.Split('/')[..^1]);
        foreach (var s in uri.Split('/'))
        {
            if (s == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
            }
            else if (s is not ("." or ""))
            {
                segments.Add(s);
            }
        }

        return string.Join('/', segments);
    }

    private static string StringValue(string literal)
    {
        var s = literal.TrimStart('r');
        var q = s.Length >= 3 && s[1] == s[0] && s[2] == s[0] ? 3 : 1;
        return s.Length >= 2 * q ? s[q..^q] : s;
    }
}
