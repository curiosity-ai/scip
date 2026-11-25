using System.Text;

namespace ScipSharp.Indexer.Rust;

using Kind = SymbolInformation.Types.Kind;
using Suffix = SymbolDescriptor.Types.Suffix;

/// <summary>One emitted occurrence (byte offsets into the file).</summary>
public sealed record OccurrenceRecord(int Start, int End, string Symbol, bool IsDefinition, (int Start, int End)? Enclosing);

/// <summary>Everything the native scanner learned about one source file.</summary>
public sealed class SourceFileResult(string path, byte[] bytes, List<RustToken> tokens)
{
    public string Path { get; } = path;

    public byte[] Bytes { get; } = bytes;

    public List<RustToken> Tokens { get; } = tokens;

    public List<OccurrenceRecord> Occurrences { get; } = [];

    public List<SymbolInformation> Symbols { get; } = [];

    /// <summary>Indices of tokens already turned into occurrences.</summary>
    public HashSet<int> Consumed { get; } = [];

    public int LocalCount { get; set; }

    // ---- structural facts recorded for the resolver (token indices) ----
    public List<(int Start, int End, string Symbol)> ModuleRanges { get; } = [];

    /// <summary>Symbols of items declared inside function bodies.</summary>
    public HashSet<string> BodyItems { get; } = [];

    public List<ImplRange> ImplRanges { get; } = [];

    public List<(int Start, int End, string Symbol)> TraitRanges { get; } = [];

    /// <summary>Token range of the type text associated with a definition (field type, return type, alias target).</summary>
    public Dictionary<string, (int From, int To)> TypeRanges { get; } = [];

    public List<GenericScope> Generics { get; } = [];

    public List<(int Start, int End)> Uses { get; } = [];

    public Dictionary<string, GenericScope> ItemGenerics { get; } = [];

    /// <summary>Resolved crate this file belongs to (set by the scanner owner).</summary>
    public object? Crate { get; set; }
}

/// <summary>An <c>impl</c> block: token range of the whole item and of the self type / trait path.</summary>
public sealed record ImplRange(int Start, int End, string Symbol, int SelfFrom, int SelfTo, int TraitFrom, int TraitTo);

/// <summary>Generic parameters declared by an item, visible in <c>[Start, End)</c>.</summary>
public sealed class GenericScope(int start, Dictionary<string, string> names)
{
    public int Start { get; } = start;

    public int End { get; set; }

    public Dictionary<string, string> Names { get; } = names;
}

internal enum AssocKind { None, Trait, Impl }

internal sealed record ScanContext(
    MonikerDescriptor[] Prefix,
    MonikerDescriptor[] ModulePrefix,
    string Dir,
    AssocKind Assoc);

/// <summary>
/// Lightweight native Rust "analyzer": a token-level item scanner that walks the module tree of one cargo target and
/// produces definitions and references using the rust-analyzer SCIP symbol scheme. It does <b>not</b> do name
/// resolution or type inference; see README.md for exactly what is and is not covered.
/// </summary>
public sealed class RustCrateScanner(CargoPackage package, CargoTarget target, ISet<string> seenFiles)
{
    private readonly Dictionary<string, List<(string Symbol, bool IsMacro)>> _itemsByName = [];

    /// <summary>Macros whose expansion defines <c>fn $name</c>: macro name to the index of the name argument.</summary>
    private readonly Dictionary<string, int> _fnMacros = [];

    /// <summary>Signature text (<c>pub(crate) mod x</c>) of file modules, recorded at their <c>mod x;</c> declaration.</summary>
    private readonly Dictionary<string, string> _moduleSigs = [];

    public List<SourceFileResult> Files { get; } = [];

    public void Scan()
    {
        var root = Path.GetFullPath(target.SourcePath);
        var prefix = RustMonikers.CrateDescriptors(target.Kind, target.Name).ToArray();
        var ctx = new ScanContext(prefix, prefix, Path.GetDirectoryName(root)!, AssocKind.None);
        ScanFile(root, ctx, rootModule: true);
    }

    private void ScanFile(string path, ScanContext moduleCtx, bool rootModule)
    {
        if (!seenFiles.Add(path) || !File.Exists(path))
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var file = new SourceFileResult(path, bytes, RustLexer.Lex(bytes));
        Files.Add(file);
        new FileScanner(this, file, rootModule).ScanModuleFile(moduleCtx);
    }

    internal static string Utf8(byte[] bytes, RustToken t) => Encoding.UTF8.GetString(bytes, t.Start, t.End - t.Start);

    private sealed class FileScanner(RustCrateScanner owner, SourceFileResult file, bool rootModule)
    {
        private readonly List<RustToken> _t = file.Tokens;
        private readonly byte[] _b = file.Bytes;
        private int _i;
        private readonly List<string> _moduleDocs = [];

        private string Text(int i) => i >= 0 && i < _t.Count ? Utf8(_b, _t[i]) : "";

        private bool Is(int i, string s) => i >= 0 && i < _t.Count && _t[i].Kind is RustTokenKind.Ident or RustTokenKind.Punct && Text(i) == s;

        private bool IsKind(int i, RustTokenKind k) => i >= 0 && i < _t.Count && _t[i].Kind == k;

        private static bool IsOpen(string s) => s is "(" or "[" or "{";

        private static bool IsClose(string s) => s is ")" or "]" or "}";

        /// <summary>Index just past the bracket matching the opener at <paramref name="i"/>.</summary>
        private int SkipBalanced(int i)
        {
            var depth = 0;
            for (; i < _t.Count; i++)
            {
                if (_t[i].Kind != RustTokenKind.Punct)
                {
                    continue;
                }

                var s = Text(i);
                if (IsOpen(s))
                {
                    depth++;
                }
                else if (IsClose(s) && --depth == 0)
                {
                    return i + 1;
                }
            }

            return _t.Count;
        }

        /// <summary>Skips <c>&lt;...&gt;</c> at <paramref name="i"/> (if present).</summary>
        private int SkipGenerics(int i)
        {
            if (!Is(i, "<"))
            {
                return i;
            }

            var depth = 0;
            while (i < _t.Count)
            {
                var s = _t[i].Kind == RustTokenKind.Punct ? Text(i) : "";
                if (s == "<")
                {
                    depth++;
                }
                else if (s == ">" && --depth == 0)
                {
                    return i + 1;
                }
                else if (IsOpen(s))
                {
                    i = SkipBalanced(i);
                    continue;
                }

                i++;
            }

            return i;
        }

        /// <summary>Advances to the next top-level <c>{</c> or <c>;</c> (not consumed).</summary>
        private int FindHeaderEnd(int i)
        {
            while (i < _t.Count && !Is(i, "{") && !Is(i, ";"))
            {
                i = _t[i].Kind == RustTokenKind.Punct && IsOpen(Text(i)) ? SkipBalanced(i) : i + 1;
            }

            return i;
        }

        private string TypeText(int from, int to)
        {
            // Mirrors the spacing of rust-analyzer's HIR display: `Foo<'a, T>`, `fn f(a: u8) -> bool`, `A + B`, `[T; N]`.
            var sb = new StringBuilder();
            var prev = "";
            for (var i = from; i < to; i++)
            {
                var s = Text(i);
                if (s.Length == 0)
                {
                    continue;
                }

                if (prev == "," && s is ")" or "]" or ">" or ">>")
                {
                    sb.Length--; // trailing comma
                    prev = "";
                }

                if (sb.Length > 0 && NeedsSpace(prev, s, sb[^1]))
                {
                    sb.Append(' ');
                }

                sb.Append(s);
                prev = s;
            }

            return sb.ToString();
        }

        /// <summary>The trait of an impl is displayed by name only (<c>std::hash::Hasher</c> becomes <c>Hasher</c>); arguments stay as written.</summary>
        private static string StripTraitPath(string text)
        {
            var lt = text.IndexOf('<', StringComparison.Ordinal);
            var head = lt < 0 ? text : text[..lt];
            var sep = head.LastIndexOf("::", StringComparison.Ordinal);
            return sep < 0 ? text : text[(sep + 2)..];
        }

        private static bool NeedsSpace(string prev, string s, char last)
        {
            if (IsWordChar(last) && IsWordChar(s[0]))
            {
                return true;
            }

            if (s is "::" or ":" or "," or ";" or ")" or "]" || prev is "::" or "&" or "(" or "[" or "<")
            {
                return false;
            }

            return prev is "," or ";" or ":" or "->" or "=" or "+" or "=>"
                || s is "->" or "=" or "+" or "=>"
                || (prev is ")" or "]" or ">" or ">>" && (IsWordChar(s[0]) || s is "&" or "*"));
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '\'';

        private static string DocText(string raw)
        {
            if (raw.StartsWith("///", StringComparison.Ordinal) || raw.StartsWith("//!", StringComparison.Ordinal))
            {
                var s = raw[3..];
                return s.StartsWith(' ') ? s[1..] : s;
            }

            // /** ... */ or /*! ... */
            var body = raw[3..];
            if (body.EndsWith("*/", StringComparison.Ordinal))
            {
                body = body[..^2];
            }

            return string.Join('\n', body.Split('\n').Select(l => l.Trim().TrimStart('*').Trim()));
        }

        // ---- symbol helpers ------------------------------------------------------------------------------

        internal SourceFileResult Out => file;

        private string FormatSymbol(IReadOnlyList<MonikerDescriptor> descriptors) =>
            RustMonikers.Format(new Moniker(owner.PackageInfo, descriptors));

        private sealed class Def(string symbol, MonikerDescriptor[] descriptors)
        {
            public string Symbol { get; } = symbol;

            public MonikerDescriptor[] Descriptors { get; } = descriptors;
        }

        private Def BeginDef(ScanContext ctx, string name, Suffix suffix) =>
            Descriptor(ctx.Prefix, new MonikerDescriptor(name, suffix));

        private Def Descriptor(MonikerDescriptor[] prefix, params MonikerDescriptor[] more)
        {
            MonikerDescriptor[] all = [.. prefix, .. more];
            return new Def(FormatSymbol(all), all);
        }

        private void EndDef(Def d, int nameTok, Kind kind, int startTok, int endTokExclusive, List<string> docs, string? signature, string displayName, bool occurrence = true)
        {
            var nt = _t[nameTok];
            (int, int)? enclosing = (_t[startTok].Start, _t[Math.Max(startTok, endTokExclusive - 1)].End);
            if (occurrence)
            {
                file.Occurrences.Add(new(nt.Start, nt.End, d.Symbol, true, enclosing));
            }

            file.Consumed.Add(nameTok);
            var info = new SymbolInformation { Symbol = d.Symbol, Kind = kind, DisplayName = displayName };
            if (docs.Count > 0)
            {
                info.Documentation.Add(string.Join('\n', docs));
            }

            if (signature is not null)
            {
                info.SignatureDocumentation = new Signature
                {
                    Language = "rust",
                    Text = signature,
                };
            }

            file.Symbols.Add(info);
        }

        private void RegisterItem(string name, string symbol, bool isMacro)
        {
            if (!owner._itemsByName.TryGetValue(name, out var list))
            {
                owner._itemsByName[name] = list = [];
            }

            list.Add((symbol, isMacro));
        }

        // ---- module files --------------------------------------------------------------------------------

        public void ScanModuleFile(ScanContext moduleCtx)
        {
            // The module defined by a file has the whole file as its range.
            var d = Descriptor(moduleCtx.Prefix);
            file.ModuleRanges.Add((0, _t.Count, d.Symbol));
            var end = _b.Length;
            var docs = new List<string>();
            _i = 0;
            ParseItems(moduleCtx, _t.Count, docs);
            file.Occurrences.Add(new(0, end, d.Symbol, true, null));
            var info = new SymbolInformation
            {
                Symbol = d.Symbol,
                Kind = Kind.Module,
                DisplayName = moduleCtx.Prefix.Length > 0 ? moduleCtx.Prefix[^1].Name : "",
            };
            if (_moduleDocs.Count > 0)
            {
                info.Documentation.Add(string.Join('\n', _moduleDocs));
            }

            if (owner._moduleSigs.TryGetValue(d.Symbol, out var moduleSig))
            {
                info.SignatureDocumentation = new Signature { Language = "rust", Text = moduleSig };
            }

            file.Symbols.Add(info);
            if (!rootModule)
            {
                owner.RegisterItemExternal(info.DisplayName, d.Symbol);
            }
        }

        // ---- items ---------------------------------------------------------------------------------------

        /// <summary>
        /// Declares generic parameters (<c>&lt;T: Bound, 'a, const N: usize&gt;</c>) at <paramref name="lt"/> as local
        /// symbols enclosed by <paramref name="enclosing"/>; returns the scope (End is set by the caller) and the
        /// token index after the closing <c>&gt;</c>.
        /// </summary>
        private (GenericScope? Scope, int Next) DeclareGenerics(int lt, string enclosing, int scopeStart)
        {
            if (!Is(lt, "<"))
            {
                return (null, lt);
            }

            var end = SkipGenerics(lt);
            Dictionary<string, string> names = [];
            var depth = 0;
            var expectName = true;
            for (var k = lt + 1; k < end - 1; k++)
            {
                var s = _t[k].Kind == RustTokenKind.Punct ? Text(k) : "";
                if (IsOpen(s) || s == "<")
                {
                    depth++;
                    continue;
                }

                if (IsClose(s) || s == ">")
                {
                    depth--;
                    continue;
                }

                if (depth == 0 && s == ",")
                {
                    expectName = true;
                    continue;
                }

                if (!expectName || depth != 0)
                {
                    continue;
                }

                var nameTok = k;
                if (Is(k, "const") && IsKind(k + 1, RustTokenKind.Ident))
                {
                    nameTok = k + 1;
                    k++;
                }

                if (_t[nameTok].Kind is RustTokenKind.Ident or RustTokenKind.Lifetime)
                {
                    var n = Text(nameTok);
                    names[n] = DeclareLocal(nameTok, n, Kind.TypeParameter, enclosing);
                    expectName = false;
                }
            }

            var scope = new GenericScope(scopeStart, names);
            file.Generics.Add(scope);
            file.ItemGenerics[enclosing] = scope;
            return (scope, end);
        }

        private void ParseItems(ScanContext ctx, int end, List<string> _)
        {
            while (_i < end)
            {
                var before = _i;
                ParseItem(ctx);
                if (_i <= before)
                {
                    _i = before + 1;
                }
            }
        }

        private void ParseItem(ScanContext ctx)
        {
            var start = _i;
            List<string> docs = [];
            string? pathAttr = null;
            while (_i < _t.Count)
            {
                if (IsKind(_i, RustTokenKind.DocOuter))
                {
                    docs.Add(DocText(Text(_i)));
                    _i++;
                }
                else if (IsKind(_i, RustTokenKind.DocInner))
                {
                    _moduleDocs.Add(DocText(Text(_i)));
                    _i++;
                    start = _i;
                }
                else if (Is(_i, "#"))
                {
                    var j = _i + 1;
                    if (Is(j, "!"))
                    {
                        j++;
                    }

                    if (!Is(j, "["))
                    {
                        break;
                    }

                    var close = SkipBalanced(j);
                    if (Is(j + 1, "path") && Is(j + 2, "=") && IsKind(j + 3, RustTokenKind.Str))
                    {
                        pathAttr = Text(j + 3).Trim('"');
                    }

                    _i = close;
                }
                else
                {
                    break;
                }
            }

            if (_i >= _t.Count)
            {
                return;
            }

            var itemStart = Math.Min(start, _t.Count - 1);
            var sigStart = _i;
            if (Is(_i, "pub"))
            {
                _i++;
                if (Is(_i, "("))
                {
                    _i = SkipBalanced(_i);
                }
            }

            // qualifiers
            while (true)
            {
                if (Is(_i, "default") || Is(_i, "safe") || (Is(_i, "const") && (Is(_i + 1, "fn") || Is(_i + 1, "unsafe") || Is(_i + 1, "async") || Is(_i + 1, "extern")))
                    || (Is(_i, "unsafe") && (Is(_i + 1, "fn") || Is(_i + 1, "impl") || Is(_i + 1, "trait") || Is(_i + 1, "extern") || Is(_i + 1, "auto")))
                    || (Is(_i, "async") && (Is(_i + 1, "fn") || Is(_i + 1, "unsafe"))))
                {
                    _i++;
                }
                else if (Is(_i, "extern") && IsKind(_i + 1, RustTokenKind.Str) && (Is(_i + 2, "fn") || Is(_i + 2, "unsafe")))
                {
                    _i += 2;
                }
                else if (Is(_i, "auto") && Is(_i + 1, "trait"))
                {
                    _i++;
                }
                else
                {
                    break;
                }
            }

            var kw = _t[_i].Kind == RustTokenKind.Ident ? Text(_i) : "";
            if (kw.Length > 0 && Is(_i + 1, "!") && IsOpen(Text(_i + 2)) && owner._fnMacros.TryGetValue(kw, out var nameArg) && ParseFnMacro(ctx, itemStart, docs, nameArg))
            {
                return;
            }

            switch (kw)
            {
                case "mod": ParseMod(ctx, itemStart, sigStart, docs, pathAttr); break;
                case "fn": ParseFn(ctx, itemStart, sigStart, docs); break;
                case "struct" or "union" when kw == "struct" || IsKind(_i + 1, RustTokenKind.Ident):
                    ParseStruct(ctx, itemStart, sigStart, docs, kw == "union" ? Kind.Union : Kind.Struct);
                    break;
                case "enum": ParseEnum(ctx, itemStart, sigStart, docs); break;
                case "trait": ParseTrait(ctx, itemStart, sigStart, docs); break;
                case "impl": ParseImpl(ctx, itemStart, sigStart, docs); break;
                case "const" or "static": ParseConstStatic(ctx, itemStart, sigStart, docs, kw); break;
                case "type": ParseTypeAlias(ctx, itemStart, sigStart, docs); break;
                case "macro_rules" when Is(_i + 1, "!"): ParseMacroRules(ctx, itemStart, docs); break;
                case "extern" when Is(_i + 1, "crate") || Is(_i + 1, "\"") || IsKind(_i + 1, RustTokenKind.Str) && Is(_i + 2, "{"):
                    ParseExtern(ctx);
                    break;
                case "use":
                    {
                        var useStart = _i;
                        SkipToSemicolon();
                        file.Uses.Add((useStart, Math.Min(_i, _t.Count)));
                        break;
                    }
                default:
                    if (_t[_i].Kind == RustTokenKind.Punct && IsOpen(Text(_i)))
                    {
                        _i = SkipBalanced(_i);
                    }
                    else
                    {
                        _i++;
                    }

                    break;
            }
        }

        private void SkipToSemicolon()
        {
            while (_i < _t.Count && !Is(_i, ";"))
            {
                _i = _t[_i].Kind == RustTokenKind.Punct && IsOpen(Text(_i)) ? SkipBalanced(_i) : _i + 1;
            }

            _i++;
        }

        private void ParseExtern(ScanContext ctx)
        {
            // `extern crate x;` or `extern "C" { ... }` (items inside belong to the enclosing module)
            if (Is(_i + 1, "crate"))
            {
                SkipToSemicolon();
                return;
            }

            _i = FindHeaderEnd(_i);
            if (Is(_i, "{"))
            {
                var end = SkipBalanced(_i);
                _i++;
                ParseItems(ctx, end - 1, []);
                _i = end;
            }
        }

        private void ParseMod(ScanContext ctx, int itemStart, int sigStart, List<string> docs, string? pathAttr)
        {
            var nameTok = _i + 1;
            if (!IsKind(nameTok, RustTokenKind.Ident))
            {
                _i++;
                return;
            }

            var name = Text(nameTok);
            var def = BeginDef(ctx, name, Suffix.Namespace);
            _i = nameTok + 1;
            if (Is(_i, ";"))
            {
                _i++;
                var fileDir = Path.GetDirectoryName(file.Path)!;
                var dir = ctx.Dir;
                string[] candidates = pathAttr is not null
                    ? [Path.GetFullPath(Path.Combine(fileDir, pathAttr))]
                    : [Path.Combine(dir, name + ".rs"), Path.Combine(dir, name, "mod.rs")];
                var found = candidates.FirstOrDefault(File.Exists);
                if (found is null)
                {
                    return;
                }

                file.Occurrences.Add(new(_t[nameTok].Start, _t[nameTok].End, def.Symbol, false, null));
                file.Consumed.Add(nameTok);
                owner._moduleSigs[def.Symbol] = SigText(sigStart, nameTok + 1);
                var stem = Path.GetFileNameWithoutExtension(found);
                var childDir = stem == "mod" ? Path.GetDirectoryName(found)! : Path.Combine(Path.GetDirectoryName(found)!, stem);
                var childCtx = new ScanContext(def.Descriptors, def.Descriptors, childDir, AssocKind.None);
                owner.ScanFile(Path.GetFullPath(found), childCtx, rootModule: false);
                return;
            }

            if (!Is(_i, "{"))
            {
                return;
            }

            var close = SkipBalanced(_i);
            var inner = new ScanContext(def.Descriptors, def.Descriptors, Path.Combine(ctx.Dir, name), AssocKind.None);
            file.ModuleRanges.Add((_i, close, def.Symbol));
            _i++;
            ParseItems(inner, close - 1, []);
            _i = close;
            EndDef(def, nameTok, Kind.Module, itemStart, _i, docs, SigText(sigStart, nameTok + 1), name);
            RegisterItem(name, def.Symbol, false);
        }

        private string SigText(int from, int to) => TypeText(from, to);

        private void ParseFn(ScanContext ctx, int itemStart, int sigStart, List<string> docs)
        {
            var nameTok = _i + 1;
            if (!IsKind(nameTok, RustTokenKind.Ident))
            {
                _i++;
                return;
            }

            var name = Text(nameTok);
            var def = BeginDef(ctx, name, Suffix.Method);
            var (fnGenerics, afterGenerics) = DeclareGenerics(nameTok + 1, def.Symbol, itemStart);
            _i = afterGenerics;
            var paramsOpen = _i;
            var paramsEnd = Is(paramsOpen, "(") ? SkipBalanced(paramsOpen) : paramsOpen;
            _i = FindHeaderEnd(paramsEnd);
            var headerEnd = _i;
            var hasBody = Is(headerEnd, "{");
            if (Is(paramsEnd, "->"))
            {
                var retEnd = headerEnd;
                for (var q = paramsEnd + 1; q < headerEnd; q++)
                {
                    if (Is(q, "where"))
                    {
                        retEnd = q;
                        break;
                    }
                }

                file.TypeRanges[def.Symbol] = (paramsEnd + 1, retEnd);
            }

            var hasSelf = false;
            for (var k = paramsOpen + 1; k < paramsEnd - 1; k++)
            {
                if (Is(k, ":") || Is(k, ","))
                {
                    break;
                }

                hasSelf |= Is(k, "self");
            }

            var kind = ctx.Assoc == AssocKind.None
                ? Kind.Function
                : hasSelf ? (hasBody || ctx.Assoc == AssocKind.Impl ? Kind.Method : Kind.TraitMethod) : Kind.StaticMethod;

            List<(string Name, string Symbol)> scope = [];
            if (Is(paramsOpen, "("))
            {
                DeclareParameters(paramsOpen, paramsEnd, def.Symbol, scope);
            }

            if (hasBody)
            {
                var bodyEnd = SkipBalanced(headerEnd);
                new BodyScanner(this, def.Symbol, ctx.ModulePrefix, ctx.Dir, scope).Run(headerEnd, bodyEnd);
                _i = bodyEnd;
            }
            else
            {
                _i = Math.Min(_i + 1, _t.Count);
            }

            if (fnGenerics is not null)
            {
                fnGenerics.End = _i;
            }

            EndDef(def, nameTok, kind, itemStart, _i, docs, SigText(sigStart, headerEnd), name);
            if (ctx.Assoc == AssocKind.None)
            {
                RegisterItem(name, def.Symbol, false);
            }
        }

        private void DeclareParameters(int open, int end, string enclosing, List<(string Name, string Symbol)> scope)
        {
            var partStart = open + 1;
            var depth = 0;
            for (var k = open + 1; k <= end - 1; k++)
            {
                var s = _t[k].Kind == RustTokenKind.Punct ? Text(k) : "";
                if (IsOpen(s) || s == "<")
                {
                    depth++;
                }
                else if (IsClose(s) || s == ">")
                {
                    depth--;
                }

                if (k == end - 1 || (s == "," && depth == 0))
                {
                    DeclareParam(partStart, k, enclosing, scope);
                    partStart = k + 1;
                }
            }
        }

        private void DeclareParam(int from, int to, string enclosing, List<(string Name, string Symbol)> scope)
        {
            var colon = -1;
            var depth = 0;
            for (var k = from; k < to; k++)
            {
                var s = _t[k].Kind == RustTokenKind.Punct ? Text(k) : "";
                if (IsOpen(s))
                {
                    depth++;
                }
                else if (IsClose(s))
                {
                    depth--;
                }
                else if (s == ":" && depth == 0)
                {
                    colon = k;
                    break;
                }
            }

            var patEnd = colon < 0 ? to : colon;
            for (var k = from; k < patEnd; k++)
            {
                if (Is(k, "self"))
                {
                    scope.Add(("self", DeclareLocal(k, "self", Kind.SelfParameter, enclosing)));
                    return;
                }
            }

            foreach (var k in ExtractBindings(from, patEnd))
            {
                var n = Text(k);
                scope.Add((n, DeclareLocal(k, n, Kind.Parameter, enclosing)));
            }
        }

        private string DeclareLocal(int tok, string name, Kind kind, string enclosing)
        {
            var symbol = RustMonikers.LocalSymbol(file.LocalCount++);
            file.Occurrences.Add(new(_t[tok].Start, _t[tok].End, symbol, true, null));
            file.Consumed.Add(tok);
            file.Symbols.Add(new SymbolInformation { Symbol = symbol, Kind = kind, DisplayName = name, EnclosingSymbol = enclosing });
            return symbol;
        }

        private static readonly HashSet<string> PatternKeywords = ["mut", "ref", "box", "true", "false", "in", "if", "self", "_"];

        /// <summary>Token indices of identifiers in a pattern that introduce bindings.</summary>
        private List<int> ExtractBindings(int from, int to)
        {
            List<int> result = [];
            var braces = 0;
            for (var k = from; k < to; k++)
            {
                if (Is(k, "{"))
                {
                    braces++;
                }
                else if (Is(k, "}"))
                {
                    braces--;
                }

                if (_t[k].Kind != RustTokenKind.Ident)
                {
                    continue;
                }

                var s = Text(k);
                if (PatternKeywords.Contains(s) || !(char.IsLower(s[0]) || (s[0] == '_' && s.Length > 1)) || Is(k - 1, "::")
                    || Is(k + 1, "::") || Is(k + 1, "(") || Is(k + 1, "{") || Is(k + 1, "!") || (braces > 0 && Is(k + 1, ":")))
                {
                    continue;
                }

                result.Add(k);
            }

            return result;
        }

        private void ParseStruct(ScanContext ctx, int itemStart, int sigStart, List<string> docs, Kind kind)
        {
            var nameTok = _i + 1;
            if (!IsKind(nameTok, RustTokenKind.Ident))
            {
                _i++;
                return;
            }

            var name = Text(nameTok);
            var def = BeginDef(ctx, name, Suffix.Type);
            var (stGenerics, afterStGenerics) = DeclareGenerics(nameTok + 1, def.Symbol, itemStart);
            _i = afterStGenerics;
            var headerEnd = _i;
            if (Is(_i, "("))
            {
                ParseTupleFields(def, _i);
                _i = SkipBalanced(_i);
                headerEnd = nameTok + 1;
                SkipToSemicolon();
            }
            else
            {
                _i = FindHeaderEnd(_i);
                if (Is(_i, "{"))
                {
                    ParseNamedFields(def, _i);
                }
                else
                {
                    _i++;
                }
            }

            if (stGenerics is not null)
            {
                stGenerics.End = _i;
            }

            EndDef(def, nameTok, kind, itemStart, _i, docs, SigText(sigStart, headerEnd), name);
            RegisterItem(name, def.Symbol, false);
        }

        /// <summary>Defines the fields <c>0</c>, <c>1</c>, ... of a tuple struct / tuple variant at <paramref name="open"/>.</summary>
        private void ParseTupleFields(Def parent, int open)
        {
            var close = SkipBalanced(open);
            var k = open + 1;
            var index = 0;
            while (k < close - 1)
            {
                var start = k;
                while (k < close - 1 && (IsKind(k, RustTokenKind.DocOuter) || Is(k, "#")))
                {
                    k = IsKind(k, RustTokenKind.DocOuter) ? k + 1 : Is(k + 1, "[") ? SkipBalanced(k + 1) : k + 1;
                }

                var sigStart = k;
                if (Is(k, "pub"))
                {
                    k++;
                    if (Is(k, "("))
                    {
                        k = SkipBalanced(k);
                    }
                }

                var typeStart = k;
                var angle = 0;
                var e = k;
                for (; e < close - 1; e++)
                {
                    var s = _t[e].Kind == RustTokenKind.Punct ? Text(e) : "";
                    if (IsOpen(s))
                    {
                        e = SkipBalanced(e) - 1;
                    }
                    else if (s == "<")
                    {
                        angle++;
                    }
                    else if (s == ">")
                    {
                        angle--;
                    }
                    else if (s == "," && angle <= 0)
                    {
                        break;
                    }
                }

                if (e > typeStart)
                {
                    // The field has no name token; the type is the definition range.
                    var name = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var fd = Descriptor(parent.Descriptors, new MonikerDescriptor(name, Suffix.Term));
                    file.Symbols.Add(new SymbolInformation
                    {
                        Symbol = fd.Symbol,
                        Kind = Kind.Field,
                        DisplayName = name,
                        SignatureDocumentation = new Signature { Language = "rust", Text = SigText(sigStart, e) },
                    });
                    file.TypeRanges[fd.Symbol] = (typeStart, e);
                    index++;
                }

                k = e + 1;
            }
        }

        /// <summary>Parses <c>{ a: T, b: U }</c> at <paramref name="open"/>; defines fields under <paramref name="parent"/>.</summary>
        private void ParseNamedFields(Def parent, int open)
        {
            var close = SkipBalanced(open);
            var k = open + 1;
            while (k < close - 1)
            {
                var start = k;
                List<string> docs = [];
                while (k < close - 1 && (IsKind(k, RustTokenKind.DocOuter) || Is(k, "#")))
                {
                    if (IsKind(k, RustTokenKind.DocOuter))
                    {
                        docs.Add(DocText(Text(k)));
                        k++;
                    }
                    else
                    {
                        k = Is(k + 1, "[") ? SkipBalanced(k + 1) : k + 1;
                    }
                }

                var sigStart = k;
                if (Is(k, "pub"))
                {
                    k++;
                    if (Is(k, "("))
                    {
                        k = SkipBalanced(k);
                    }
                }

                var nameTok = k;
                var typeStart = k + 2;
                var angle = 0;
                var e = typeStart;
                for (; e < close - 1; e++)
                {
                    var s = _t[e].Kind == RustTokenKind.Punct ? Text(e) : "";
                    if (IsOpen(s))
                    {
                        e = SkipBalanced(e) - 1;
                    }
                    else if (s == "<")
                    {
                        angle++;
                    }
                    else if (s == ">")
                    {
                        angle--;
                    }
                    else if (s == "," && angle <= 0)
                    {
                        break;
                    }
                }

                if (IsKind(nameTok, RustTokenKind.Ident) && Is(nameTok + 1, ":"))
                {
                    var name = Text(nameTok);
                    var fd = Descriptor(parent.Descriptors, new MonikerDescriptor(name, Suffix.Term));
                    file.TypeRanges[fd.Symbol] = (typeStart, e);
                    EndDef(fd, nameTok, Kind.Field, start, e, docs, SigText(sigStart, e), name);
                }

                k = e + 1;
            }

            _i = close;
        }

        private void ParseEnum(ScanContext ctx, int itemStart, int sigStart, List<string> docs)
        {
            var nameTok = _i + 1;
            if (!IsKind(nameTok, RustTokenKind.Ident))
            {
                _i++;
                return;
            }

            var name = Text(nameTok);
            var def = BeginDef(ctx, name, Suffix.Type);
            var (enGenerics, afterEnGenerics) = DeclareGenerics(nameTok + 1, def.Symbol, itemStart);
            _i = FindHeaderEnd(afterEnGenerics);
            var headerEnd = _i;
            if (Is(_i, "{"))
            {
                var close = SkipBalanced(_i);
                var k = _i + 1;
                while (k < close - 1)
                {
                    var start = k;
                    List<string> vdocs = [];
                    while (k < close - 1 && (IsKind(k, RustTokenKind.DocOuter) || Is(k, "#")))
                    {
                        if (IsKind(k, RustTokenKind.DocOuter))
                        {
                            vdocs.Add(DocText(Text(k)));
                            k++;
                        }
                        else
                        {
                            k = Is(k + 1, "[") ? SkipBalanced(k + 1) : k + 1;
                        }
                    }

                    var vTok = k;
                    if (!IsKind(vTok, RustTokenKind.Ident))
                    {
                        k++;
                        continue;
                    }

                    var vname = Text(vTok);
                    var vd = Descriptor(def.Descriptors, new MonikerDescriptor(vname, Suffix.Type));
                    k++;
                    if (Is(k, "("))
                    {
                        ParseTupleFields(vd, k);
                        k = SkipBalanced(k);
                    }
                    else if (Is(k, "{"))
                    {
                        var fieldsOpen = k;
                        ParseNamedFields(vd, fieldsOpen);
                        k = SkipBalanced(fieldsOpen);
                    }

                    var vEnd = k;
                    while (k < close - 1 && !Is(k, ","))
                    {
                        k = _t[k].Kind == RustTokenKind.Punct && IsOpen(Text(k)) ? SkipBalanced(k) : k + 1;
                    }

                    EndDef(vd, vTok, Kind.EnumMember, start, Math.Max(vEnd, vTok + 1), vdocs, null, vname);
                    k++;
                }

                _i = close;
            }
            else
            {
                _i++;
            }

            if (enGenerics is not null)
            {
                enGenerics.End = _i;
            }

            EndDef(def, nameTok, Kind.Enum, itemStart, _i, docs, SigText(sigStart, headerEnd), name);
            RegisterItem(name, def.Symbol, false);
        }

        private void ParseTrait(ScanContext ctx, int itemStart, int sigStart, List<string> docs)
        {
            var nameTok = _i + 1;
            if (!IsKind(nameTok, RustTokenKind.Ident))
            {
                _i++;
                return;
            }

            var name = Text(nameTok);
            var def = BeginDef(ctx, name, Suffix.Type);
            var (trGenerics, afterTrGenerics) = DeclareGenerics(nameTok + 1, def.Symbol, itemStart);
            _i = FindHeaderEnd(afterTrGenerics);
            var headerEnd = _i;
            if (Is(_i, "{"))
            {
                var close = SkipBalanced(_i);
                file.TraitRanges.Add((itemStart, close, def.Symbol));
                var inner = new ScanContext(def.Descriptors, ctx.ModulePrefix, ctx.Dir, AssocKind.Trait);
                _i++;
                ParseItems(inner, close - 1, []);
                _i = close;
            }
            else
            {
                _i++;
            }

            if (trGenerics is not null)
            {
                trGenerics.End = _i;
            }

            EndDef(def, nameTok, Kind.Trait, itemStart, _i, docs, SigText(sigStart, headerEnd), name);
            RegisterItem(name, def.Symbol, false);
        }

        private void ParseImpl(ScanContext ctx, int itemStart, int sigStart, List<string> docs)
        {
            var implTok = _i;
            var implGenericsTok = _i + 1;
            var k = SkipGenerics(_i + 1);
            if (Is(k, "!"))
            {
                k++;
            }

            // header up to `{` or `where`
            var headerStart = k;
            var angle = 0;
            var forTok = -1;
            while (k < _t.Count && !Is(k, "{") && !Is(k, "where") && !Is(k, ";"))
            {
                if (Is(k, "<"))
                {
                    angle++;
                }
                else if (Is(k, ">"))
                {
                    angle--;
                }
                else if (Is(k, "for") && angle == 0 && forTok < 0)
                {
                    forTok = k;
                }
                else if (_t[k].Kind == RustTokenKind.Punct && IsOpen(Text(k)))
                {
                    k = SkipBalanced(k);
                    continue;
                }

                k++;
            }

            var headerEnd = k;
            var selfText = forTok >= 0 ? TypeText(forTok + 1, headerEnd) : TypeText(headerStart, headerEnd);
            var traitText = forTok >= 0 ? StripTraitPath(TypeText(headerStart, forTok)) : null;
            if (traitText is not null && RustStd.DefaultSelfArgTraits.Contains(traitText))
            {
                traitText += "<Self>"; // rust-analyzer displays the defaulted `Rhs = Self` parameter
            }

            List<MonikerDescriptor> more = [new("impl", Suffix.Type), new(selfText, Suffix.TypeParameter)];
            if (traitText is not null)
            {
                more.Add(new(traitText, Suffix.TypeParameter));
            }

            var def = Descriptor(ctx.Prefix, [.. more]);
            var (implGenerics, _) = DeclareGenerics(implGenericsTok, def.Symbol, itemStart);
            _i = FindHeaderEnd(headerEnd);
            if (Is(_i, "{"))
            {
                var close = SkipBalanced(_i);
                file.ImplRanges.Add(new ImplRange(
                    itemStart, close, def.Symbol,
                    forTok >= 0 ? forTok + 1 : headerStart, headerEnd,
                    forTok >= 0 ? headerStart : -1, forTok >= 0 ? forTok : -1));
                var inner = new ScanContext(def.Descriptors, ctx.ModulePrefix, ctx.Dir, AssocKind.Impl);
                _i++;
                ParseItems(inner, close - 1, []);
                _i = close;
            }
            else
            {
                _i++;
            }

            if (implGenerics is not null)
            {
                implGenerics.End = _i;
            }

            EndDef(def, implTok, Kind.TypeAlias, itemStart, _i, docs, SigText(sigStart, headerEnd), selfText, occurrence: false);
        }

        private void ParseConstStatic(ScanContext ctx, int itemStart, int sigStart, List<string> docs, string kw)
        {
            var k = _i + 1;
            if (kw == "static" && Is(k, "mut"))
            {
                k++;
            }

            if (!IsKind(k, RustTokenKind.Ident) || Is(k, "_"))
            {
                SkipToSemicolon();
                return;
            }

            var name = Text(k);
            var def = BeginDef(ctx, name, Suffix.Term);
            var sigEnd = k + 1;
            // signature runs up to the `=`
            var e = k + 1;
            while (e < _t.Count && !Is(e, "=") && !Is(e, ";"))
            {
                e = _t[e].Kind == RustTokenKind.Punct && IsOpen(Text(e)) ? SkipBalanced(e) : e + 1;
            }

            sigEnd = e;
            _i = e;
            if (Is(k + 1, ":"))
            {
                file.TypeRanges[def.Symbol] = (k + 2, e);
            }

            SkipToSemicolon();
            EndDef(def, k, kw == "const" ? Kind.Constant : Kind.StaticVariable, itemStart, _i, docs, SigText(sigStart, sigEnd), name);
            if (ctx.Assoc == AssocKind.None)
            {
                RegisterItem(name, def.Symbol, false);
            }
        }

        private void ParseTypeAlias(ScanContext ctx, int itemStart, int sigStart, List<string> docs)
        {
            var nameTok = _i + 1;
            if (!IsKind(nameTok, RustTokenKind.Ident))
            {
                _i++;
                return;
            }

            var name = Text(nameTok);
            var def = BeginDef(ctx, name, Suffix.Type);
            var (taGenerics, e) = DeclareGenerics(nameTok + 1, def.Symbol, itemStart);
            var sigEnd = e;
            _i = e;
            if (Is(e, "="))
            {
                var semi = e + 1;
                while (semi < _t.Count && !Is(semi, ";"))
                {
                    semi = _t[semi].Kind == RustTokenKind.Punct && IsOpen(Text(semi)) ? SkipBalanced(semi) : semi + 1;
                }

                file.TypeRanges[def.Symbol] = (e + 1, semi);
            }

            SkipToSemicolon();
            if (taGenerics is not null)
            {
                taGenerics.End = _i;
            }

            EndDef(def, nameTok, ctx.Assoc == AssocKind.None ? Kind.TypeAlias : Kind.AssociatedType, itemStart, _i, docs, SigText(sigStart, sigEnd), name);
            if (ctx.Assoc == AssocKind.None)
            {
                RegisterItem(name, def.Symbol, false);
            }
        }

        /// <summary>
        /// Item-level invocation of a <c>macro_rules!</c> macro that expands to <c>fn $name() { .. }</c> (test-generating
        /// macros such as ripgrep's <c>rgtest!</c>): defines the generated function and scans the arguments as its body.
        /// </summary>
        private bool ParseFnMacro(ScanContext ctx, int itemStart, List<string> docs, int nameArg)
        {
            var open = _i + 2;
            var close = SkipBalanced(open);
            var argStart = open + 1;
            var arg = 0;
            var depth = 0;
            var nameTok = -1;
            for (var k = open + 1; k < close - 1; k++)
            {
                var q = _t[k].Kind == RustTokenKind.Punct ? Text(k) : "";
                if (IsOpen(q))
                {
                    depth++;
                }
                else if (IsClose(q))
                {
                    depth--;
                }
                else if (depth == 0 && q == ",")
                {
                    arg++;
                    argStart = k + 1;
                    continue;
                }

                if (arg == nameArg && depth == 0 && k == argStart && IsKind(k, RustTokenKind.Ident) && (Is(k + 1, ",") || k + 1 == close - 1))
                {
                    nameTok = k;
                }
            }

            if (nameTok < 0)
            {
                return false;
            }

            var def = BeginDef(ctx, Text(nameTok), Suffix.Method);
            new BodyScanner(this, def.Symbol, ctx.ModulePrefix, ctx.Dir, []).Run(open, close - 1);
            _i = close;
            if (Is(_i, ";"))
            {
                _i++;
            }

            EndDef(def, nameTok, Kind.Function, itemStart, _i, docs, null, Text(nameTok));
            return true;
        }

        /// <summary>Position of the <c>$name:ident</c> matcher argument that a macro body turns into <c>fn $name</c>, or -1.</summary>
        private int FnMacroNameArg(int bodyOpen, int bodyEnd)
        {
            var fnName = "";
            for (var j = bodyOpen + 1; j + 2 < bodyEnd; j++)
            {
                if (Is(j, "fn") && Is(j + 1, "$") && IsKind(j + 2, RustTokenKind.Ident))
                {
                    fnName = Text(j + 2);
                    break;
                }
            }

            if (fnName.Length == 0 || !IsOpen(Text(bodyOpen + 1)))
            {
                return -1;
            }

            var m0 = bodyOpen + 1;
            var mEnd = SkipBalanced(m0) - 1;
            var arg = 0;
            var depth = 0;
            for (var k = m0 + 1; k < mEnd; k++)
            {
                var q = _t[k].Kind == RustTokenKind.Punct ? Text(k) : "";
                if (depth == 0 && Is(k, "$") && Is(k + 1, fnName) && Is(k + 2, ":") && Is(k + 3, "ident"))
                {
                    return arg;
                }

                if (IsOpen(q))
                {
                    depth++;
                }
                else if (IsClose(q))
                {
                    depth--;
                }
                else if (depth == 0 && q == ",")
                {
                    arg++;
                }
            }

            return -1;
        }

        private void ParseMacroRules(ScanContext ctx, int itemStart, List<string> docs)
        {
            var nameTok = _i + 2;
            if (!IsKind(nameTok, RustTokenKind.Ident))
            {
                _i++;
                return;
            }

            var name = Text(nameTok);
            var def = BeginDef(ctx, name, Suffix.Macro);
            _i = nameTok + 1;
            if (_i < _t.Count && IsOpen(Text(_i)))
            {
                var nameArg = FnMacroNameArg(_i, SkipBalanced(_i) - 1);
                if (nameArg >= 0)
                {
                    owner._fnMacros[name] = nameArg;
                }

                _i = SkipBalanced(_i);
            }

            if (Is(_i, ";"))
            {
                _i++;
            }

            EndDef(def, nameTok, Kind.Macro, itemStart, _i, docs, "macro_rules! " + name, name);
            RegisterItem(name, def.Symbol, true);
        }

        // ---- function bodies -----------------------------------------------------------------------------

        private bool IsItemStart(int i)
        {
            var j = i;
            while (j < _t.Count)
            {
                if (IsKind(j, RustTokenKind.DocOuter))
                {
                    j++;
                }
                else if (Is(j, "#") && Is(j + 1, "["))
                {
                    j = SkipBalanced(j + 1);
                }
                else
                {
                    break;
                }
            }

            if (Is(j, "pub"))
            {
                j++;
                if (Is(j, "("))
                {
                    j = SkipBalanced(j);
                }
            }

            if (j >= _t.Count || _t[j].Kind != RustTokenKind.Ident)
            {
                return false;
            }

            return Text(j) switch
            {
                "fn" or "struct" or "enum" or "trait" or "impl" or "mod" or "type" or "use" => IsKind(j + 1, RustTokenKind.Ident) || Text(j) == "impl" || Text(j) == "use",
                "macro_rules" => Is(j + 1, "!"),
                "const" => IsKind(j + 1, RustTokenKind.Ident),
                "static" => IsKind(j + 1, RustTokenKind.Ident) && (!Is(j + 1, "move")),
                "union" => IsKind(j + 1, RustTokenKind.Ident) && (Is(j + 2, "{") || Is(j + 2, "<")),
                "unsafe" => Is(j + 1, "fn") || Is(j + 1, "impl") || Is(j + 1, "trait"),
                "async" => Is(j + 1, "fn"),
                "extern" => Is(j + 1, "crate") || IsKind(j + 1, RustTokenKind.Str) && (Is(j + 2, "fn") || Is(j + 2, "{")),
                _ => false,
            };
        }

        private sealed class BodyScanner(FileScanner s, string enclosing, MonikerDescriptor[] modulePrefix, string dir, List<(string Name, string Symbol)> paramScope)
        {
            public void Run(int open, int end)
            {
                List<List<(string Name, string Symbol)>> scopes = [paramScope];
                List<(int Depth, List<int> Bindings)> pending = [];
                HashSet<int> skip = [];
                var depth = 0;
                List<(string Name, string Symbol)>? blockBindings = null;
                var blockDepth = 0;
                var itemCtx = new ScanContext(modulePrefix, modulePrefix, dir, AssocKind.None);
                List<(int From, int To, List<(string Name, string Symbol)> Bindings)> regions = [];
                List<int> active = [];
                for (var i = open; i < end; i++)
                {
                    while (active.Count > 0 && i >= active[^1])
                    {
                        active.RemoveAt(active.Count - 1);
                        scopes.RemoveAt(scopes.Count - 1);
                    }

                    for (var r = 0; r < regions.Count; r++)
                    {
                        if (regions[r].From == i)
                        {
                            scopes.Add([.. regions[r].Bindings]);
                            active.Add(regions[r].To);
                        }
                    }

                    var t = s._t[i];
                    if (t.Kind == RustTokenKind.Ident && s.Text(i) == "match" && !s.Is(i - 1, ".") && !s.Is(i - 1, "::"))
                    {
                        ScanMatchArms(i, end, regions, skip);
                    }

                    if (t.Kind == RustTokenKind.Punct)
                    {
                        var p = s.Text(i);
                        if (p == "{")
                        {
                            depth++;
                            scopes.Add([]);
                            if (blockBindings is not null && depth - 1 == blockDepth)
                            {
                                scopes[^1].AddRange(blockBindings);
                                blockBindings = null;
                            }
                        }
                        else if (p == "|" && IsClosureStart(i, open))
                        {
                            var j = i + 1;
                            var d = 0;
                            while (j < end)
                            {
                                var q = s._t[j].Kind == RustTokenKind.Punct ? s.Text(j) : "";
                                if (IsOpenP(q) || q == "<")
                                {
                                    d++;
                                }
                                else if (IsCloseP(q) || q == ">")
                                {
                                    d--;
                                }
                                else if (q == "|" && d <= 0)
                                {
                                    break;
                                }

                                j++;
                            }

                            var partStart = i + 1;
                            var pd = 0;
                            for (var k = i + 1; k <= j; k++)
                            {
                                var q = s._t[k].Kind == RustTokenKind.Punct ? s.Text(k) : "";
                                if (IsOpenP(q) || q == "<")
                                {
                                    pd++;
                                }
                                else if (IsCloseP(q) || q == ">")
                                {
                                    pd--;
                                }

                                if (k == j || (q == "," && pd == 0))
                                {
                                    var colon = -1;
                                    var cd = 0;
                                    for (var c = partStart; c < k; c++)
                                    {
                                        var cq = s._t[c].Kind == RustTokenKind.Punct ? s.Text(c) : "";
                                        if (IsOpenP(cq))
                                        {
                                            cd++;
                                        }
                                        else if (IsCloseP(cq))
                                        {
                                            cd--;
                                        }
                                        else if (cq == ":" && cd == 0)
                                        {
                                            colon = c;
                                            break;
                                        }
                                    }

                                    foreach (var b in s.ExtractBindings(partStart, colon < 0 ? k : colon))
                                    {
                                        var n = s.Text(b);
                                        scopes[^1].Add((n, s.DeclareLocal(b, n, Kind.Parameter, enclosing)));
                                    }

                                    partStart = k + 1;
                                }
                            }

                            i = j;
                        }
                        else if (p is "(" or "[")
                        {
                            depth++;
                        }
                        else if (p is ")" or "]")
                        {
                            depth--;
                        }
                        else if (p == "}")
                        {
                            depth--;
                            if (scopes.Count > 1)
                            {
                                scopes.RemoveAt(scopes.Count - 1);
                            }

                            pending.RemoveAll(x => x.Depth > depth);
                        }
                        else if (p == ";")
                        {
                            for (var q = pending.Count - 1; q >= 0; q--)
                            {
                                if (pending[q].Depth != depth)
                                {
                                    continue;
                                }

                                foreach (var b in pending[q].Bindings)
                                {
                                    var n = s.Text(b);
                                    scopes[^1].Add((n, s.DeclareLocal(b, n, Kind.Variable, enclosing)));
                                }

                                pending.RemoveAt(q);
                            }
                        }

                        continue;
                    }

                    if (t.Kind != RustTokenKind.Ident)
                    {
                        if (t.Kind is RustTokenKind.DocOuter && s.IsItemStart(i))
                        {
                            i = RunItem(i, itemCtx) - 1;
                        }

                        continue;
                    }

                    var atStmtStart = i > open && s.Is(i - 1, "{") || s.Is(i - 1, ";") || s.Is(i - 1, "}") || i == open + 1;
                    if (atStmtStart && s.IsItemStart(i))
                    {
                        i = RunItem(i, itemCtx) - 1;
                        continue;
                    }

                    var text = s.Text(i);
                    if (text == "let")
                    {
                        var j = i + 1;
                        var d = 0;
                        while (j < end)
                        {
                            var q = s._t[j].Kind == RustTokenKind.Punct ? s.Text(j) : "";
                            if (IsOpenP(q))
                            {
                                d++;
                            }
                            else if (IsCloseP(q))
                            {
                                d--;
                            }

                            if (d < 0 || (d == 0 && q is "=" or ":" or ";"))
                            {
                                break;
                            }

                            j++;
                        }

                        var bindings = s.ExtractBindings(i + 1, j);
                        foreach (var b in bindings)
                        {
                            skip.Add(b);
                        }

                        if (s.Is(i - 1, "if") || s.Is(i - 1, "while") || s.Is(i - 1, "&"))
                        {
                            // `if let` / `while let`: bindings are visible in the following block only.
                            blockBindings = [];
                            blockDepth = depth;
                            foreach (var b in bindings)
                            {
                                var n = s.Text(b);
                                blockBindings.Add((n, s.DeclareLocal(b, n, Kind.Variable, enclosing)));
                            }
                        }
                        else
                        {
                            pending.Add((depth, bindings));
                        }

                        continue;
                    }

                    if (text == "for" && !s.Is(i + 1, "<") && !s.Is(i - 1, ">"))
                    {
                        var j = i + 1;
                        var d = 0;
                        while (j < end)
                        {
                            var q = s._t[j].Kind == RustTokenKind.Punct ? s.Text(j) : "";
                            if (IsOpenP(q))
                            {
                                d++;
                            }
                            else if (IsCloseP(q))
                            {
                                d--;
                            }

                            if (d < 0 || (d == 0 && s.Is(j, "in")))
                            {
                                break;
                            }

                            j++;
                        }

                        blockBindings = [];
                        blockDepth = depth;
                        foreach (var b in s.ExtractBindings(i + 1, j))
                        {
                            var n = s.Text(b);
                            blockBindings.Add((n, s.DeclareLocal(b, n, Kind.Variable, enclosing)));
                        }

                        i = j;
                        continue;
                    }

                    if (skip.Contains(i) || s.Is(i - 1, ".") || s.Is(i + 1, "::") || s.Is(i + 1, "!"))
                    {
                        continue;
                    }

                    if (s.Is(i + 1, ":") && (s.Is(i - 1, "{") || s.Is(i - 1, ",")) && !s.Is(i + 2, ":"))
                    {
                        continue;
                    }

                    for (var sc = scopes.Count - 1; sc >= 0; sc--)
                    {
                        var hit = scopes[sc].FindLastIndex(x => x.Name == text);
                        if (hit >= 0)
                        {
                            s.Out.Occurrences.Add(new(t.Start, t.End, scopes[sc][hit].Symbol, false, null));
                            s.Out.Consumed.Add(i);
                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// Declares the bindings of every arm pattern of the <c>match</c> at <paramref name="matchTok"/>; each arm's
            /// bindings are visible from the start of its pattern (guards) to the end of its body.
            /// </summary>
            private void ScanMatchArms(int matchTok, int end, List<(int From, int To, List<(string Name, string Symbol)> Bindings)> regions, HashSet<int> skip)
            {
                var depth = 0;
                var open = -1;
                for (var k = matchTok + 1; k < end; k++)
                {
                    var q = s._t[k].Kind == RustTokenKind.Punct ? s.Text(k) : "";
                    if (q == "{" && depth == 0)
                    {
                        open = k;
                        break;
                    }

                    if (IsOpenP(q))
                    {
                        depth++;
                    }
                    else if (IsCloseP(q))
                    {
                        depth--;
                        if (depth < 0)
                        {
                            return;
                        }
                    }
                }

                if (open < 0)
                {
                    return;
                }

                var closeIdx = s.SkipBalanced(open) - 1;
                var pos = open + 1;
                while (pos < closeIdx)
                {
                    var patStart = pos;
                    while (s.Is(pos, "#") && s.Is(pos + 1, "["))
                    {
                        pos = s.SkipBalanced(pos + 1);
                    }

                    var arrow = -1;
                    var guard = -1;
                    var d = 0;
                    for (var k = pos; k < closeIdx; k++)
                    {
                        var q = s._t[k].Kind == RustTokenKind.Punct ? s.Text(k) : "";
                        if (IsOpenP(q))
                        {
                            d++;
                        }
                        else if (IsCloseP(q))
                        {
                            d--;
                        }
                        else if (d == 0 && q == "=>")
                        {
                            arrow = k;
                            break;
                        }
                        else if (d == 0 && guard < 0 && s._t[k].Kind == RustTokenKind.Ident && s.Text(k) == "if")
                        {
                            guard = k;
                        }
                    }

                    if (arrow < 0)
                    {
                        return;
                    }

                    var bodyEnd = arrow + 1;
                    if (s.Is(bodyEnd, "{"))
                    {
                        bodyEnd = s.SkipBalanced(bodyEnd);
                        if (s.Is(bodyEnd, ","))
                        {
                            bodyEnd++;
                        }
                    }
                    else
                    {
                        d = 0;
                        while (bodyEnd < closeIdx)
                        {
                            var q = s._t[bodyEnd].Kind == RustTokenKind.Punct ? s.Text(bodyEnd) : "";
                            if (IsOpenP(q))
                            {
                                d++;
                            }
                            else if (IsCloseP(q))
                            {
                                d--;
                            }
                            else if (d == 0 && q == ",")
                            {
                                bodyEnd++;
                                break;
                            }

                            bodyEnd++;
                        }
                    }

                    List<(string Name, string Symbol)> bindings = [];
                    HashSet<string> seen = [];
                    foreach (var b in s.ExtractBindings(pos, guard < 0 ? arrow : guard))
                    {
                        var n = s.Text(b);
                        skip.Add(b);
                        // or-patterns bind the same name in every alternative: one symbol, several definitions
                        var sym = bindings.FirstOrDefault(x => x.Name == n).Symbol;
                        if (sym is null)
                        {
                            sym = s.DeclareLocal(b, n, Kind.Variable, enclosing);
                            bindings.Add((n, sym));
                        }
                        else
                        {
                            s.Out.Occurrences.Add(new(s._t[b].Start, s._t[b].End, sym, true, null));
                            s.Out.Consumed.Add(b);
                        }
                    }

                    regions.Add((patStart, Math.Min(bodyEnd, closeIdx), bindings));
                    pos = bodyEnd;
                }
            }

            private bool IsClosureStart(int i, int open)
            {
                if (i <= open)
                {
                    return false;
                }

                var prev = s._t[i - 1];
                var pt = s.Text(i - 1);
                if (prev.Kind == RustTokenKind.Ident)
                {
                    return pt is "move" or "return" or "in" or "else" or "break";
                }

                return prev.Kind == RustTokenKind.Punct && pt is "(" or "," or "=" or "{" or ";" or "=>" or "[" or ":" or "!";
            }

            private int RunItem(int i, ScanContext itemCtx)
            {
                var saved = s._i;
                s._i = i;
                var before = s.Out.Symbols.Count;
                s.ParseItem(itemCtx);
                for (var q = before; q < s.Out.Symbols.Count; q++)
                {
                    s.Out.BodyItems.Add(s.Out.Symbols[q].Symbol);
                }

                var next = Math.Max(s._i, i + 1);
                s._i = saved;
                return next;
            }

            private static bool IsOpenP(string x) => x is "(" or "[" or "{";

            private static bool IsCloseP(string x) => x is ")" or "]" or "}";
        }
    }

    private PackageInformation PackageInfo => package.Information;

    private void RegisterItemExternal(string name, string symbol)
    {
        if (!_itemsByName.TryGetValue(name, out var list))
        {
            _itemsByName[name] = list = [];
        }

        list.Add((symbol, false));
    }
}
