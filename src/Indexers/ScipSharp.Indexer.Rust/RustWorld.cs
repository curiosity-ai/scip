using System.Runtime.CompilerServices;
using System.Text;

namespace ScipSharp.Indexer.Rust;

using Kind = SymbolInformation.Types.Kind;
using Suffix = SymbolDescriptor.Types.Suffix;

/// <summary>Per-file lookup tables painted from the scanner's recorded ranges.</summary>
internal sealed class FileInfo
{
    public required SourceFileResult File { get; init; }

    public required CrateData Crate { get; init; }

    public Item?[] ModuleAt { get; set; } = [];

    public ImplData?[] ImplAt { get; set; } = [];

    public Item?[] TraitAt { get; set; } = [];

    public bool IsStdText { get; init; }

    public Dictionary<int, string> LocalAtTok { get; } = [];

    public Dictionary<string, int> LocalDeclTok { get; } = [];

    public Dictionary<string, SymbolInformation> LocalInfo { get; } = [];

    // Innermost generic scope at each token and each scope's enclosing scope (generic scopes nest like items).
    private int[]? _genericAt;
    private int[]? _genericParent;

    /// <summary>
    /// Symbol of the generic parameter <paramref name="name"/> declared by the innermost generic scope enclosing token
    /// <paramref name="tok"/>. Walks outwards from a per-token table instead of testing every scope of the file for
    /// every identifier (quadratic in files with thousands of generic items).
    /// </summary>
    public string? GenericSymbol(int tok, string name)
    {
        var scopes = File.Generics;
        if (scopes.Count == 0)
        {
            return null;
        }

        if (_genericAt is null)
        {
            var n = File.Tokens.Count;
            var at = new int[n];
            Array.Fill(at, -1);
            var parent = new int[scopes.Count];
            Array.Fill(parent, -1);
            // longest first so inner scopes overwrite outer ones; among equal ranges the earliest ends on top
            foreach (var k in Enumerable.Range(0, scopes.Count).OrderByDescending(k => scopes[k].End - scopes[k].Start).ThenByDescending(k => k))
            {
                var (s, e) = (Math.Max(0, scopes[k].Start), Math.Min(n, scopes[k].End));
                if (s < e)
                {
                    parent[k] = at[s];
                    Array.Fill(at, k, s, e - s);
                }
            }

            (_genericAt, _genericParent) = (at, parent);
        }

        if (tok < 0 || tok >= _genericAt.Length)
        {
            return null;
        }

        for (var g = _genericAt[tok]; g >= 0; g = _genericParent![g])
        {
            var scope = scopes[g];
            if (scope.Start <= tok && tok < scope.End && scope.Names.TryGetValue(name, out var sym))
            {
                return sym;
            }
        }

        return null;
    }
}

/// <summary>
/// The cross-crate item graph plus name resolution (modules, <c>use</c> imports, associated items, std) and a small
/// type model. Built after all crates were scanned; consumed by <see cref="RustResolver"/>.
/// </summary>
internal sealed class RustWorld
{
    public List<CrateData> Crates { get; } = [];


    public Dictionary<SourceFileResult, FileInfo> Files { get; } = [];


    private readonly List<ImplData> _allImpls = [];

    private readonly Dictionary<Item, List<UseBinding>> _imports = [];

    private readonly Dictionary<string, Item> _stdTypeNames = [];

    private readonly Dictionary<string, Item> _prelude = [];

    private readonly Dictionary<string, RType?> _stdTypeCache = [];

    public CrateData Core { get; }

    public CrateData Alloc { get; }

    public CrateData Std { get; }

    private readonly Dictionary<string, Item> _blanket = [];

    /// <summary>Blanket trait impls (<c>impl&lt;T: Display&gt; ToString for T</c>) used when the receiver type is unknown.</summary>
    public Item? Blanket(string method) => _blanket.GetValueOrDefault(method);

    private readonly Dictionary<string, Item> _stdAttrs = [];

    /// <summary>Built-in attribute (<c>test</c>, <c>derive</c>) or derive macro (<c>Debug</c>) of the std model.</summary>
    public Item? StdAttr(string name) => _stdAttrs.GetValueOrDefault(name);

    public Item? StdNamed(string name) => _stdTypeNames.GetValueOrDefault(name);

    public Item StrItem { get; private set; } = null!;

    public Item SliceItem { get; private set; } = null!;

    public RustWorld()
    {
        Core = NewStdCrate("core");
        Alloc = NewStdCrate("alloc");
        Std = NewStdCrate("std");
        BuildStd();
    }

    // ===================================================================================================
    //  Building
    // ===================================================================================================

    private CrateData NewStdCrate(string name)
    {
        var crate = new CrateData(name, new PackageInformation(name, RustStd.RepoUrl + name), "other", isStd: true);
        crate.Root = NewItem(crate, [new MonikerDescriptor(name, Suffix.Namespace)], Kind.Module);
        Crates.Add(crate);
        return crate;
    }

    private int _stdItemCount;

    private Item NewItem(CrateData crate, MonikerDescriptor[] descriptors, Kind kind)
    {
        _stdItemCount++;
        var symbol = RustMonikers.Format(new Moniker(crate.Package, descriptors));
        var item = new Item(descriptors[^1].Name, symbol, kind, crate);
        crate.BySymbol[symbol] = item;
        return item;
    }

    private static CrateData StdCrateOf(WorldPath p, RustWorld w) => p.Crate switch { "core" => w.Core, "alloc" => w.Alloc, _ => w.Std };

    private readonly record struct WorldPath(string Crate, string[] Parts);

    private static WorldPath ParseWorldPath(string path)
    {
        var parts = path.Split('/');
        return new WorldPath(parts[0], parts);
    }

    private Item EnsureModule(string[] parts, int count)
    {
        var crate = StdCrateOf(new WorldPath(parts[0], parts), this);
        var cur = crate.Root!;
        for (var i = 1; i < count; i++)
        {
            if (cur.Members.TryGetValue(parts[i], out var list) && list.FirstOrDefault(x => x.Kind == Kind.Module) is { } m)
            {
                cur = m;
                continue;
            }

            var descriptors = parts.Take(i + 1).Select(p => new MonikerDescriptor(p, Suffix.Namespace)).ToArray();
            var mod = NewItem(crate, descriptors, Kind.Module);
            mod.Parent = cur;
            cur.AddMember(mod);
            cur = mod;
        }

        return cur;
    }

    private Item? WalkStd(string path)
    {
        var parts = path.Split('/');
        var cur = StdCrateOf(new WorldPath(parts[0], parts), this).Root;
        for (var i = 1; i < parts.Length && cur is not null; i++)
        {
            cur = cur.Members.TryGetValue(parts[i], out var l) ? l[0] : null;
        }

        return cur;
    }

    private void BuildStd()
    {
        foreach (var raw in RustStd.Data.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var f = line.Split('|');
            switch (f[0])
            {
                case "struct" or "enum" or "trait":
                    AddStdType(f);
                    break;
                case "impl":
                    {
                        var type = WalkStd(f[1]);
                        if (type is not null)
                        {
                            AddStdImpl(type, f[2], EnsureModule(f[3].Split('/'), f[3].Split('/').Length), f[4], trait: null, f.Length > 5 ? f[5] : null);
                        }

                        break;
                    }

                case "mod":
                    {
                        var parts = f[1].Split('/');
                        EnsureModule(parts, parts.Length);
                        break;
                    }

                case "modre":
                    {
                        var parts = f[1].Split('/');
                        _reexports.Add((EnsureModule(parts, parts.Length), f[2]));
                        break;
                    }

                case "fn":
                    {
                        var parts = f[1].Split('/');
                        var module = EnsureModule(parts, parts.Length - 1);
                        var d = parts.Take(parts.Length - 1).Select(p => new MonikerDescriptor(p, Suffix.Namespace))
                            .Append(new MonikerDescriptor(parts[^1], Suffix.Method)).ToArray();
                        var fn = NewItem(module.Crate, d, Kind.Function);
                        fn.StdTy = f[2];
                        fn.Parent = module;
                        module.AddMember(fn);
                        break;
                    }

                case "macro":
                    {
                        var parts = f[1].Split('/');
                        var module = EnsureModule(parts, parts.Length - 1);
                        var crate = module.Crate;
                        var d = parts.Take(parts.Length - 1).Select(p => new MonikerDescriptor(p, Suffix.Namespace))
                            .Append(new MonikerDescriptor(parts[^1], Suffix.Macro)).ToArray();
                        var it = NewItem(crate, d, Kind.Macro);
                        it.Parent = module;
                        module.AddMember(it);
                        break;
                    }

                case "attr":
                    {
                        // attribute / derive macros live in their own namespace (`:` suffix) and are not nameable members
                        var parts = f[1].Split('/');
                        var module = EnsureModule(parts, parts.Length - 1);
                        var d = parts.Take(parts.Length - 1).Select(p => new MonikerDescriptor(p, Suffix.Namespace))
                            .Append(new MonikerDescriptor(parts[^1], Suffix.Meta)).ToArray();
                        _stdAttrs[parts[^1]] = NewItem(module.Crate, d, Kind.Attribute);
                        break;
                    }

                case "alias":
                    {
                        var target = WalkStd(f[2]);
                        if (target is null)
                        {
                            break;
                        }

                        var parts = f[1].Split('/');
                        EnsureModule(parts, parts.Length - 1).AddMemberAs(parts[^1], target);
                        break;
                    }

                case "prelude":
                    foreach (var p in f[1].Split(','))
                    {
                        if (WalkStd(p) is { } it)
                        {
                            _prelude[it.Name] = it;
                        }
                    }

                    break;
            }
        }

        foreach (var (module, from) in _reexports)
        {
            if (WalkStd(from) is { } source)
            {
                foreach (var (name, list) in source.Members.ToList())
                {
                    if (!module.Members.ContainsKey(name))
                    {
                        foreach (var m in list)
                        {
                            module.AddMemberAs(name, m);
                        }
                    }
                }
            }
        }

        AddPrimitives();
        AddBlanket("alloc/string", "ToString", "to_string", "String", "T");
        AddBlanket("core/convert", "Into<U>", "into", "()", "T");
        StrItem = MakeBuiltin("str");
        SliceItem = MakeBuiltin("slice");
        _stdTypeNames["str"] = StrItem;
        _stdTypeNames["slice"] = SliceItem;
    }

    private void AddBlanket(string modulePath, string traitText, string method, string ret, string selfText)
    {
        var parts = modulePath.Split('/');
        var module = EnsureModule(parts, parts.Length);
        var holder = NewItem(module.Crate, [.. SymbolPrefix(module), new("blanket " + method, Suffix.Type)], Kind.Struct);
        AddStdImpl(holder, selfText, module, method + "(s)->" + ret, null, traitText);
        _blanket[method] = holder.Impls[^1].Members[method][0];
    }

    private readonly Dictionary<string, Item> _prims = [];

    public Item? Primitive(string name) => _prims.GetValueOrDefault(name);

    /// <summary>
    /// Primitive types with their operator trait impls (<c>core ops/arith/impl#[usize][`Add&lt;Self&gt;`]add()</c> ...);
    /// they only exist to resolve operators, never as referenced symbols themselves.
    /// </summary>
    private void AddPrimitives()
    {
        string[] ints = ["u8", "u16", "u32", "u64", "u128", "usize", "i8", "i16", "i32", "i64", "i128", "isize"];
        string[] floats = ["f32", "f64"];
        (string Trait, string Method)[] arith = [("Add", "add"), ("Sub", "sub"), ("Mul", "mul"), ("Div", "div"), ("Rem", "rem")];
        (string Trait, string Method)[] bits = [("BitAnd", "bitand"), ("BitOr", "bitor"), ("BitXor", "bitxor")];
        foreach (var name in ints.Concat(floats).Append("bool").Append("char"))
        {
            var d = new[] { new MonikerDescriptor("core", Suffix.Namespace), new MonikerDescriptor(name, Suffix.Type) };
            var item = NewItem(Core, d, Kind.Struct);
            _prims[name] = item;
            var numeric = name is not ("bool" or "char");
            if (numeric)
            {
                AddOps(item, name, "core/ops/arith", arith, name, assign: true);
            }

            if (ints.Contains(name) || name == "bool")
            {
                AddOps(item, name, "core/ops/bit", bits, name, assign: true);
                AddOps(item, name, "core/ops/bit", [("Not", "not")], name, assign: false, selfArg: false);
            }

            AddOps(item, name, "core/cmp/impls", [("PartialEq", "eq"), ("PartialEq", "ne"), ("PartialOrd", "lt"), ("PartialOrd", "le"), ("PartialOrd", "gt"), ("PartialOrd", "ge")], "bool", assign: false);
        }
    }

    private void AddOps(Item prim, string name, string modulePath, (string Trait, string Method)[] ops, string ret, bool assign, bool selfArg = true)
    {
        var parts = modulePath.Split('/');
        var module = EnsureModule(parts, parts.Length);
        List<(string Trait, string Method)> all = [.. ops];
        if (assign)
        {
            all.AddRange(ops.Select(o => (o.Trait + "Assign", o.Method + "_assign")));
        }

        // one impl block per trait (`impl#[usize][`Add<Self>`]`), members grouped by trait
        foreach (var group in all.GroupBy(o => o.Trait))
        {
            var traitPath = modulePath == "core/cmp/impls" ? "core/cmp/" + group.Key : modulePath + "/" + group.Key;
            var trait = WalkStd(traitPath);
            if (trait is null)
            {
                continue;
            }

            var isAssign = group.Key.EndsWith("Assign", StringComparison.Ordinal);
            var members = string.Join(';', group.Select(g => g.Method + "(s)->" + (isAssign ? "()" : ret)));
            AddStdImpl(prim, name, module, members, trait, selfArg && group.Key is not "Not" ? group.Key + "<Self>" : group.Key);
        }
    }

    private Item MakeBuiltin(string name)
    {
        var d = new[] { new MonikerDescriptor("core", Suffix.Namespace), new MonikerDescriptor(name, Suffix.Type) };
        return NewItem(Core, d, Kind.Struct);
    }

    private void AddStdType(string[] f)
    {
        var parts = f[1].Split('/');
        var module = EnsureModule(parts, parts.Length - 1);
        var crate = module.Crate;
        var d = parts.Take(parts.Length - 1).Select(p => new MonikerDescriptor(p, Suffix.Namespace))
            .Append(new MonikerDescriptor(parts[^1], Suffix.Type)).ToArray();
        var kind = f[0] switch { "enum" => Kind.Enum, "trait" => Kind.Trait, _ => Kind.Struct };
        var item = NewItem(crate, d, kind);
        item.Parent = module;
        module.AddMember(item);
        _stdTypeNames.TryAdd(item.Name, item);
        if (f[0] == "trait")
        {
            AddStdMembers(item, d, f[3], inTrait: true);
            return;
        }

        if (f[0] == "enum")
        {
            foreach (var v in f[3].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                MonikerDescriptor[] vd = [.. d, new(v, Suffix.Type)];
                var vi = NewItem(crate, vd, Kind.EnumMember);
                vi.Parent = item;
                item.AddMember(vi);
            }
        }

        if (f.Length > 4 && f[4].Length > 0)
        {
            AddStdImpl(item, f[2], module, f[4], trait: null);
        }
    }

    private readonly List<(Item Module, string From)> _reexports = [];

    private void AddStdImpl(Item type, string selfText, Item module, string members, Item? trait, string? traitText = null)
    {
        var crate = module.Crate;
        var prefix = SymbolPrefix(module);
        List<MonikerDescriptor> dl = [.. prefix, new("impl", Suffix.Type), new(selfText, Suffix.TypeParameter)];
        if (traitText is not null)
        {
            dl.Add(new(traitText, Suffix.TypeParameter));
        }

        var d = dl.ToArray();
        var impl = new ImplData(RustMonikers.Format(new Moniker(crate.Package, d)), crate)
        {
            Module = module,
            StdSelfText = selfText,
            TraitDef = trait,
        };
        _allImpls.Add(impl);
        type.Impls.Add(impl);
        impl.SelfType = null;
        AddStdMembers(null, d, members, inTrait: false, impl);
    }

    private static MonikerDescriptor[] SymbolPrefix(Item module)
    {
        List<MonikerDescriptor> r = [];
        for (var m = module; m is not null; m = m.Parent)
        {
            r.Insert(0, new MonikerDescriptor(m.Name, Suffix.Namespace));
        }

        return [.. r];
    }

    private void AddStdMembers(Item? owner, MonikerDescriptor[] prefix, string members, bool inTrait, ImplData? impl = null)
    {
        foreach (var m in members.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var open = m.IndexOf('(');
            var name = m[..open];
            var hasSelf = m[open..].StartsWith("(s)", StringComparison.Ordinal);
            var ret = m.Contains("->", StringComparison.Ordinal) ? m[(m.IndexOf("->", StringComparison.Ordinal) + 2)..] : "()";
            var crate = (owner?.Crate ?? impl!.Crate);
            MonikerDescriptor[] d = [.. prefix, new(name, Suffix.Method)];
            var kind = hasSelf ? (inTrait ? Kind.TraitMethod : Kind.Method) : Kind.StaticMethod;
            var item = NewItem(crate, d, kind);
            item.StdTy = ret;
            if (owner is not null)
            {
                item.Parent = owner;
                owner.AddMember(item);
            }
            else
            {
                item.ParentImpl = impl;
                impl!.AddMember(item);
            }
        }
    }

    // ---- scanned crates -----------------------------------------------------------------------------

    public CrateData AddCrate(CrateData crate, IReadOnlyList<SourceFileResult> files)
    {
        Crates.Add(crate);
        List<(Item Item, Symbol Parsed)> created = [];
        List<(ImplData Impl, Symbol Parsed)> impls = [];
        foreach (var f in files)
        {
            crate.Files.Add(f);
            Files[f] = new FileInfo { File = f, Crate = crate };
            Dictionary<string, ImplRange> implRanges = [];
            foreach (var r in f.ImplRanges)
            {
                implRanges.TryAdd(r.Symbol, r); // first wins, as FirstOrDefault did (a linear search per impl was quadratic)
            }

            foreach (var info in f.Symbols)
            {
                if (info.Symbol.StartsWith("local ", StringComparison.Ordinal) || crate.BySymbol.ContainsKey(info.Symbol)
                    || !SymbolParser.TryParse(info.Symbol, out var parsed, out _) || parsed.Descriptors.Count == 0)
                {
                    continue;
                }

                var last = parsed.Descriptors[^1];
                if (info.Kind == Kind.TypeAlias && last.Suffix == Suffix.TypeParameter)
                {
                    if (!crate.ImplBySymbol.ContainsKey(info.Symbol))
                    {
                        var impl = new ImplData(info.Symbol, crate)
                        {
                            File = f,
                            Range = implRanges.GetValueOrDefault(info.Symbol),
                        };
                        crate.ImplBySymbol[info.Symbol] = impl;
                        _allImpls.Add(impl);
                        impls.Add((impl, parsed));
                    }

                    continue;
                }

                var item = new Item(last.Name, info.Symbol, info.Kind, crate) { File = f, BodyLocal = f.BodyItems.Contains(info.Symbol) };
                if (f.TypeRanges.TryGetValue(info.Symbol, out var tr))
                {
                    item.TyFrom = tr.From;
                    item.TyTo = tr.To;
                }

                if (f.ItemGenerics.TryGetValue(info.Symbol, out var g))
                {
                    item.Generics = g;
                }

                crate.BySymbol[info.Symbol] = item;
                created.Add((item, parsed));
                if (info.Kind == Kind.Macro)
                {
                    crate.Macros[item.Name] = item;
                }
            }
        }

        foreach (var (impl, parsed) in impls)
        {
            var idx = -1;
            for (var i = 0; i < parsed.Descriptors.Count; i++)
            {
                if (parsed.Descriptors[i] is { Suffix: Suffix.Type, Name: "impl" })
                {
                    idx = i;
                }
            }

            if (idx >= 0 && crate.BySymbol.TryGetValue(FormatPrefix(parsed, idx), out var module))
            {
                impl.Module = module;
            }
        }

        foreach (var (item, parsed) in created)
        {
            if (parsed.Descriptors is [{ Name: "crate", Suffix: Suffix.Namespace }] && item.Kind == Kind.Module && crate.Root is null)
            {
                crate.Root = item;
                continue;
            }

            var parentSym = FormatPrefix(parsed, parsed.Descriptors.Count - 1);
            if (crate.ImplBySymbol.TryGetValue(parentSym, out var impl))
            {
                item.ParentImpl = impl;
                impl.AddMember(item);
            }
            else if (crate.BySymbol.TryGetValue(parentSym, out var parent))
            {
                item.Parent = parent;
                parent.AddMember(item);
            }
            else if (crate.Root is not null && item != crate.Root)
            {
                // e.g. items nested in function bodies whose module chain is unknown
                item.Parent = crate.Root;
                crate.Root.AddMember(item);
            }
        }

        if (crate.Root is null)
        {
            crate.Root = created.Select(c => c.Item).FirstOrDefault(i => i.Kind == Kind.Module);
        }

        return crate;
    }

    private static string FormatPrefix(Symbol parsed, int count)
    {
        var s = new Symbol { Scheme = parsed.Scheme, Package = parsed.Package };
        if (count == 0)
        {
            s.Descriptors.Add(new SymbolDescriptor { Name = "crate", Suffix = Suffix.Namespace });
        }

        for (var i = 0; i < count; i++)
        {
            s.Descriptors.Add(parsed.Descriptors[i]);
        }

        return SymbolFormatter.Verbose.Format(s);
    }

    /// <summary>Paints per-token module/impl/trait tables and parses <c>use</c> trees. Call after all crates were added.</summary>
    public void Prepare()
    {
        foreach (var (file, fi) in Files)
        {
            var n = file.Tokens.Count + 1;
            fi.ModuleAt = new Item?[n];
            fi.ImplAt = new ImplData?[n];
            fi.TraitAt = new Item?[n];
            foreach (var (s, e, sym) in file.ModuleRanges.OrderByDescending(r => r.End - r.Start))
            {
                if (fi.Crate.BySymbol.TryGetValue(sym, out var m))
                {
                    Array.Fill(fi.ModuleAt, m, s, Math.Min(e, n) - s);
                }
            }

            foreach (var r in file.ImplRanges.OrderByDescending(r => r.End - r.Start))
            {
                if (fi.Crate.ImplBySymbol.TryGetValue(r.Symbol, out var impl))
                {
                    var copy = impl.Range == r ? impl : impl;
                    Array.Fill(fi.ImplAt, copy, r.Start, Math.Min(r.End, n) - r.Start);
                }
            }

            foreach (var (s, e, sym) in file.TraitRanges)
            {
                if (fi.Crate.BySymbol.TryGetValue(sym, out var t))
                {
                    Array.Fill(fi.TraitAt, t, s, Math.Min(e, n) - s);
                }
            }

            // locals: token index of every local occurrence
            var starts = new Dictionary<int, int>(file.Tokens.Count);
            for (var i = 0; i < file.Tokens.Count; i++)
            {
                starts[file.Tokens[i].Start] = i;
            }

            foreach (var o in file.Occurrences)
            {
                if (o.Symbol.StartsWith("local ", StringComparison.Ordinal) && starts.TryGetValue(o.Start, out var tok))
                {
                    fi.LocalAtTok[tok] = o.Symbol;
                    if (o.IsDefinition)
                    {
                        fi.LocalDeclTok[o.Symbol] = tok;
                    }
                }
            }

            foreach (var s in file.Symbols)
            {
                if (s.Symbol.StartsWith("local ", StringComparison.Ordinal))
                {
                    fi.LocalInfo[s.Symbol] = s;
                }
            }

            foreach (var (us, ue) in file.Uses)
            {
                var module = fi.ModuleAt[us] ?? fi.Crate.Root;
                if (module is null)
                {
                    continue;
                }

                var parser = new UseParser(file);
                foreach (var b in parser.Parse(us + 1, ue))
                {
                    b.File = file;
                    b.Module = module;
                    if (!_imports.TryGetValue(module, out var list))
                    {
                        _imports[module] = list = [];
                    }

                    list.Add(b);
                }
            }
        }

        foreach (var impl in _allImpls)
        {
            LinkImpl(impl);
        }

        // The item graph and imports are complete: from now on module lookups are pure and can be filtered/memoised.
        _knownNames = [];
        foreach (var crate in Crates)
        {
            _knownNames.UnionWith(crate.MemberNames);
        }

        foreach (var list in _imports.Values)
        {
            foreach (var u in list)
            {
                if (u.Name is { } n)
                {
                    _knownNames.Add(n);
                }
            }
        }
    }

    /// <summary>Names that any module/type member or import binding has; null until <see cref="Prepare"/> finished.</summary>
    private HashSet<string>? _knownNames;

    /// <summary>Results of top-level <see cref="LookupIn"/> calls (each walks the glob-import graph).</summary>
    private readonly Dictionary<(Item Module, string Name, NameSpaces Want), Item?> _lookupCache = [];

    /// <summary>Number of use bindings currently being resolved (lookups made meanwhile depend on that state).</summary>
    private int _resolvingUses;

    public IEnumerable<ImplData> AllImpls => _allImpls;

    public IEnumerable<UseBinding> ImportsOf(Item module) => _imports.TryGetValue(module, out var l) ? l : [];

    // ===================================================================================================
    //  Impl linking
    // ===================================================================================================

    private void LinkImpl(ImplData impl)
    {
        if (impl.Linked)
        {
            return;
        }

        impl.Linked = true;
        if (impl.StdSelfText is not null)
        {
            return;
        }

        if (impl.Range is not { } r || impl.File is null)
        {
            return;
        }

        var fi = Files[impl.File];
        var cx = new Ctx(fi, r.SelfFrom);
        var k = r.SelfFrom;
        impl.SelfType = ParseType(cx, ref k, r.SelfTo);
        if (r.TraitFrom >= 0)
        {
            var tk = r.TraitFrom;
            var t = ParseType(new Ctx(fi, tk), ref tk, r.TraitTo);
            impl.TraitDef = t?.Def;
        }

        impl.SelfType?.Def?.Impls.Add(impl);
    }

    // ===================================================================================================
    //  Lookup
    // ===================================================================================================

    /// <summary>A position in a file: provides the module / impl / generics visible there.</summary>
    internal readonly struct Ctx(FileInfo file, int tok)
    {
        public FileInfo File { get; } = file;

        public int Tok { get; } = tok;

        public Item? Module => Tok < File.ModuleAt.Length ? File.ModuleAt[Tok] ?? File.Crate.Root : File.Crate.Root;

        public ImplData? Impl => Tok < File.ImplAt.Length ? File.ImplAt[Tok] : null;

        public Item? Trait => Tok < File.TraitAt.Length ? File.TraitAt[Tok] : null;

        public CrateData Crate => File.Crate;

        public string? GenericSymbol(string name) => File.GenericSymbol(Tok, name);
    }

    private static bool Matches(Item i, NameSpaces want) => (i.Spaces & want) != 0;

    public Item? LookupIn(Item module, string name, NameSpaces want, HashSet<(Item, string)>? visited = null)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return null; // a very long chain of glob re-exports
        }

        if (_knownNames is not null && !_knownNames.Contains(name))
        {
            return null; // nothing anywhere is called that: skip walking every glob import (quadratic in big crates)
        }

        if (visited is null && _knownNames is not null && _resolvingUses == 0)
        {
            // A fresh top-level lookup on the finished graph is a pure function of its arguments.
            var key = (module, name, want);
            if (!_lookupCache.TryGetValue(key, out var cached))
            {
                _lookupCache[key] = cached = LookupIn(module, name, want, []);
            }

            return cached;
        }

        visited ??= [];
        if (!visited.Add((module, name)))
        {
            return null;
        }

        Item? deferred = null;
        if (module.Members.TryGetValue(name, out var direct))
        {
            var hit = direct.FirstOrDefault(i => Matches(i, want) && !i.BodyLocal);
            if (hit is not null)
            {
                return hit;
            }

            deferred = direct.FirstOrDefault(i => Matches(i, want));
        }

        if (module.Kind == Kind.Module)
        {
            foreach (var u in ImportsOf(module))
            {
                if (u.Glob || u.Name != name || u.Resolving)
                {
                    continue;
                }

                var target = ResolveUse(u, want, visited);
                if (target is not null && Matches(target, want))
                {
                    return target;
                }
            }

            if (deferred is not null)
            {
                return deferred;
            }

            foreach (var u in ImportsOf(module))
            {
                if (!u.Glob || u.Resolving)
                {
                    continue;
                }

                var target = ResolveUsePath(u, u.Path.Count, NameSpaces.Type, visited);
                if (target is null || target == module)
                {
                    continue;
                }

                var hit = LookupIn(target, name, want, visited);
                if (hit is not null)
                {
                    return hit;
                }
            }
        }

        return deferred;
    }

    private Item? ResolveUse(UseBinding u, NameSpaces want, HashSet<(Item, string)> visited)
    {
        var path = u.Path;
        return path.Count == 0 ? null : ResolveUsePath(u, path.Count, want, visited);
    }

    private Item? ResolveUsePath(UseBinding u, int count, NameSpaces want, HashSet<(Item, string)> visited)
    {
        u.Resolving = true;
        _resolvingUses++;
        try
        {
            var fi = Files[u.File!];
            var cx = new Ctx(fi, u.Path.Count > 0 ? u.Path[0].Tok : 0);
            return ResolveSegments(cx, [.. u.Path.Take(count).Select(s => s.Name)], want, null, visited);
        }
        finally
        {
            u.Resolving = false;
            _resolvingUses--;
        }
    }

    /// <summary>First segment of a path: local scope chain, extern crates, prelude.</summary>
    private Item? ResolveHead(Ctx cx, string name, NameSpaces want, HashSet<(Item, string)>? visited)
    {
        var module = cx.Module;
        switch (name)
        {
            case "crate":
                return cx.Crate.Root;
            case "self":
                return module;
            case "super":
                return module?.Parent is { } p ? ModuleOf(p) : module;
            case "Self":
                return cx.Impl?.SelfType?.Def ?? cx.Trait;
        }

        if (cx.File.IsStdText)
        {
            return _stdTypeNames.GetValueOrDefault(name) ?? _prelude.GetValueOrDefault(name);
        }

        if (module is not null)
        {
            var hit = LookupIn(module, name, want, visited);
            if (hit is not null)
            {
                return hit;
            }
        }

        if (cx.Crate.Deps.TryGetValue(name, out var dep) && dep.Root is not null)
        {
            return dep.Root;
        }

        if (name is "std" or "core" or "alloc")
        {
            return (name == "std" ? Std : name == "core" ? Core : Alloc).Root;
        }

        if ((want & NameSpaces.Macro) != 0 && cx.Crate.Macros.TryGetValue(name, out var mac))
        {
            // a module-local macro that shadows a std macro (`write!`) is only in scope inside its own module tree
            var local = false;
            for (var m = module; m is not null && !local; m = m.Parent)
            {
                local = m == mac.Parent;
            }

            if (local || mac.Parent == cx.Crate.Root || !_prelude.TryGetValue(name, out var stdMacro) || stdMacro.Kind != Kind.Macro)
            {
                return mac;
            }
        }

        if (_prelude.TryGetValue(name, out var pre) && Matches(pre, want))
        {
            return pre;
        }

        // `Some`/`None` etc. when asked in a wider namespace
        return _prelude.TryGetValue(name, out var pre2) ? pre2 : null;
    }

    public static Item? ModuleOf(Item? item)
    {
        while (item is not null && item.Kind != Kind.Module)
        {
            item = item.Parent;
        }

        return item;
    }

    /// <summary>Resolves a path; <paramref name="steps"/> (if given) receives the item of every resolved segment.</summary>
    public Item? ResolveSegments(Ctx cx, IReadOnlyList<string> names, NameSpaces want, List<Item?>? steps, HashSet<(Item, string)>? visited = null)
    {
        Item? cur = null;
        for (var i = 0; i < names.Count; i++)
        {
            var last = i == names.Count - 1;
            var w = last ? want : NameSpaces.Type;
            if (i == 0)
            {
                cur = ResolveHead(cx, names[0], last ? want : NameSpaces.Type | NameSpaces.Macro, visited);
                if (cur is null && !last)
                {
                    cur = ResolveHead(cx, names[0], NameSpaces.Any, visited);
                }
            }
            else if (cur is null)
            {
                steps?.Add(null);
                continue;
            }
            else
            {
                cur = Step(cur, names[i], w, visited);
            }

            steps?.Add(cur);
            if (cur is null)
            {
                for (var j = i + 1; j < names.Count; j++)
                {
                    steps?.Add(null);
                }

                return null;
            }
        }

        return cur;
    }

    private Item? Step(Item cur, string name, NameSpaces want, HashSet<(Item, string)>? visited)
    {
        switch (cur.Kind)
        {
            case Kind.Module:
                return LookupIn(cur, name, want, visited) ?? LookupIn(cur, name, NameSpaces.Any, visited);
            case Kind.TypeAlias:
                {
                    var t = DeclaredType(cur);
                    return t?.Def is { } d && d != cur ? Step(d, name, want, visited) : null;
                }

            default:
                return FindAssoc(cur, name);
        }
    }

    /// <summary>Associated item lookup: enum variants, inherent impl items, trait impl items, trait items.</summary>
    public Item? FindAssoc(Item type, string name, bool methodsOnly = false)
    {
        if (_knownNames is null)
        {
            return FindAssocUncached(type, name, methodsOnly);
        }

        // after Prepare the impl graph is fixed: memoise (a type with thousands of impls was re-sorted per call)
        var key = (type, name, methodsOnly);
        if (!_assocCache.TryGetValue(key, out var found))
        {
            _assocCache[key] = found = FindAssocUncached(type, name, methodsOnly);
        }

        return found;
    }

    private readonly Dictionary<(Item Type, string Name, bool MethodsOnly), Item?> _assocCache = [];

    private Item? FindAssocUncached(Item type, string name, bool methodsOnly)
    {
        if (type.Members.TryGetValue(name, out var direct) && direct.FirstOrDefault(x => !methodsOnly || x.Kind != Kind.Field) is { } directHit)
        {
            return directHit;
        }

        Item? viaTrait = null;
        foreach (var impl in type.Impls.OrderBy(i => i.TraitDef is null ? 0 : 1))
        {
            if (impl.Members.TryGetValue(name, out var l))
            {
                return l[0];
            }

            if (viaTrait is null && impl.TraitDef is { } t && t.Members.TryGetValue(name, out var tl))
            {
                viaTrait = tl[0];
            }
        }

        if (viaTrait is not null)
        {
            return viaTrait;
        }

        if (type.Crate.IsStd && type.Name is "Iter" or "Entry" && _stdTypeNames.TryGetValue("Iterator", out var iterator) && iterator.Members.TryGetValue(name, out var im))
        {
            return im[0];
        }

        if (type.Crate.IsStd && type.Name is "Rc" or "Arc" or "Box" or "String")
        {
            // blanket / derive-provided trait methods (clone, to_string, into ...)
            foreach (var tn in new[] { "Clone", "ToString", "Into", "ToOwned", "AsRef", "PartialEq" })
            {
                if (_stdTypeNames.TryGetValue(tn, out var tr) && tr.Members.TryGetValue(name, out var tm))
                {
                    return tm[0];
                }
            }
        }

        // trait items reachable through generic/trait objects: type is a trait
        return null;
    }

    // ===================================================================================================
    //  Types
    // ===================================================================================================

    private static readonly HashSet<string> Transparent = ["Box", "Rc", "Arc", "Ref", "RefMut", "Cow", "Pin", "MutexGuard"];

    public RType? ParseType(Ctx cx, ref int k, int to, int depth = 0)
    {
        var t = cx.File.File.Tokens;
        string Txt(int i) => Encoding.UTF8.GetString(cx.File.File.Bytes, t[i].Start, t[i].End - t[i].Start);
        bool IsP(int i, string s) => i < to && i < t.Count && t[i].Kind == RustTokenKind.Punct && Txt(i) == s;
        bool IsI(int i, string s) => i < to && i < t.Count && t[i].Kind == RustTokenKind.Ident && Txt(i) == s;
        if (depth > 6)
        {
            return null;
        }

        // prefixes
        while (k < to)
        {
            if (IsP(k, "&") || IsP(k, "*") || IsP(k, "?") || t[k].Kind == RustTokenKind.Lifetime || IsI(k, "mut") || IsI(k, "dyn") || IsI(k, "impl") || IsI(k, "const") || IsI(k, "unsafe"))
            {
                k++;
            }
            else
            {
                break;
            }
        }

        if (k >= to)
        {
            return null;
        }

        if (IsP(k, "["))
        {
            var close = SkipBalanced(t, k, Txt);
            var inner = k + 1;
            var elem = ParseType(cx, ref inner, Math.Min(close - 1, to), depth + 1);
            k = close;
            return RType.Of(SliceItem, elem);
        }

        if (IsP(k, "(") || IsP(k, "!"))
        {
            k = IsP(k, "(") ? SkipBalanced(t, k, Txt) : k + 1;
            return null;
        }

        if (IsP(k, "<"))
        {
            // qualified path <T as Trait>::Name : not modelled
            k = to;
            return null;
        }

        if (t[k].Kind != RustTokenKind.Ident)
        {
            k++;
            return null;
        }

        List<string> names = [];
        RType?[] args = [];
        var first = k;
        while (k < to && t[k].Kind == RustTokenKind.Ident)
        {
            names.Add(Txt(k));
            k++;
            if (IsP(k, "<") || (IsP(k, "::") && IsP(k + 1, "<")))
            {
                if (IsP(k, "::"))
                {
                    k++;
                }

                var close = SkipAngles(t, k, to, Txt);
                List<RType?> list = [];
                var a = k + 1;
                while (a < close - 1)
                {
                    if (IsP(a, ","))
                    {
                        a++;
                        continue;
                    }

                    var before = a;
                    // associated type bindings `Item = T`
                    if (t[a].Kind == RustTokenKind.Ident && IsP(a + 1, "=") )
                    {
                        a += 2;
                    }

                    var ty = ParseType(cx, ref a, close - 1, depth + 1);
                    if (a == before)
                    {
                        a++;
                    }

                    while (a < close - 1 && !IsP(a, ","))
                    {
                        a = t[a].Kind == RustTokenKind.Punct && Txt(a) is "(" or "[" or "{" ? SkipBalanced(t, a, Txt) : a + 1;
                    }

                    list.Add(ty);
                }

                args = [.. list];
                k = close;
            }

            if (IsP(k, "::") && k + 1 < to && t[k + 1].Kind == RustTokenKind.Ident)
            {
                k++;
                continue;
            }

            break;
        }

        // `+ Bound` / `where` handled by caller (stop here)
        var single = names.Count == 1 ? names[0] : null;
        if (single is not null && cx.GenericSymbol(single) is not null)
        {
            return RType.ParamOf(single);
        }

        if (single is "Self")
        {
            return cx.Impl?.SelfType ?? (cx.Trait is { } tr ? RType.Of(tr) : null);
        }

        if (cx.File.IsStdText && single is not null && single.Length == 1 && char.IsUpper(single[0]))
        {
            return RType.ParamOf(single);
        }

        if (single is "str")
        {
            return RType.Of(StrItem);
        }

        if (single is not null && _prims.TryGetValue(single, out var prim))
        {
            return RType.Of(prim);
        }

        var item = ResolveSegments(new Ctx(cx.File, first), names, NameSpaces.Type, null);
        if (item is null)
        {
            return single is not null && single.Length == 1 && char.IsUpper(single[0]) ? RType.ParamOf(single) : null;
        }

        if (item.Kind == Kind.TypeAlias && item.File is not null && item.TyTo > item.TyFrom && depth < 5)
        {
            var ak = item.TyFrom;
            return ParseType(new Ctx(Files[item.File], ak), ref ak, item.TyTo, depth + 1);
        }

        return item.IsType || item.Kind == Kind.EnumMember ? new RType(item, null, args) : null;
    }

    private static int SkipBalanced(List<RustToken> t, int i, Func<int, string> txt)
    {
        var depth = 0;
        for (; i < t.Count; i++)
        {
            if (t[i].Kind != RustTokenKind.Punct)
            {
                continue;
            }

            var s = txt(i);
            if (s is "(" or "[" or "{")
            {
                depth++;
            }
            else if (s is ")" or "]" or "}" && --depth == 0)
            {
                return i + 1;
            }
        }

        return t.Count;
    }

    private static int SkipAngles(List<RustToken> t, int i, int to, Func<int, string> txt)
    {
        var depth = 0;
        for (; i < to && i < t.Count; i++)
        {
            if (t[i].Kind != RustTokenKind.Punct)
            {
                continue;
            }

            var s = txt(i);
            if (s == "<")
            {
                depth++;
            }
            else if (s == ">" && --depth == 0)
            {
                return i + 1;
            }
            else if (s is "(" or "[" or "{")
            {
                i = SkipBalanced(t, i, txt) - 1;
            }
        }

        return to;
    }

    /// <summary>Parses std type text such as <c>Option&lt;T&gt;</c> (cached).</summary>
    public RType? StdType(string text)
    {
        if (_stdTypeCache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var file = new SourceFileResult("<std>", bytes, RustLexer.Lex(bytes));
        var fi = new FileInfo { File = file, Crate = Std, IsStdText = true };
        var k = 0;
        var r = text == "()" ? null : ParseType(new Ctx(fi, 0), ref k, file.Tokens.Count);
        _stdTypeCache[text] = r;
        return r;
    }

    /// <summary>The declared type of a field/const/fn return (or alias target) of <paramref name="item"/>.</summary>
    public RType? DeclaredType(Item item)
    {
        if (item.StdTy is { } std)
        {
            return StdType(std);
        }

        if (item.File is null || item.TyTo <= item.TyFrom)
        {
            return null;
        }

        var k = item.TyFrom;
        return ParseType(new Ctx(Files[item.File], k), ref k, item.TyTo);
    }

    /// <summary>The self type of the impl (or trait) enclosing <paramref name="item"/> member.</summary>
    public RType? SelfOf(Item item)
    {
        if (item.ParentImpl is { } impl)
        {
            return ImplSelf(impl);
        }

        return item.Parent is { } p && p.Kind == Kind.Trait ? RType.Of(p) : null;
    }

    public RType? ImplSelf(ImplData impl)
    {
        if (impl.SelfType is null && impl.StdSelfText is { } txt)
        {
            impl.SelfType = StdType(txt);
        }

        return impl.SelfType;
    }

    public static RType? Subst(RType? t, Dictionary<string, RType?> map)
    {
        if (t is null)
        {
            return null;
        }

        if (t.Param is { } p)
        {
            return map.TryGetValue(p, out var r) ? r : t;
        }

        return t.Args.Length == 0 ? t : t with { Args = [.. t.Args.Select(a => Subst(a, map))] };
    }

    public void Unify(RType? pattern, RType? actual, Dictionary<string, RType?> map)
    {
        if (pattern is null || actual is null)
        {
            return;
        }

        if (pattern.Param is { } p)
        {
            map.TryAdd(p, actual);
            return;
        }

        var compatible = pattern.Def == actual.Def || (pattern.Def == SliceItem && actual.Def?.Name is "Vec" or "VecDeque");
        if (!compatible)
        {
            return;
        }

        for (var i = 0; i < Math.Min(pattern.Args.Length, actual.Args.Length); i++)
        {
            Unify(pattern.Args[i], actual.Args[i], map);
        }
    }

    /// <summary>Return type of a method/function as seen from <paramref name="receiver"/>.</summary>
    public RType? ReturnType(Item fn, RType? receiver)
    {
        var ret = DeclaredType(fn);
        if (ret is null)
        {
            return null;
        }

        Dictionary<string, RType?> map = [];
        var self = SelfOf(fn);
        if (receiver is not null)
        {
            if (fn.ParentImpl is { } impl)
            {
                Unify(ImplSelf(impl), receiver, map);
            }
            else if (fn.Parent is { Kind: Kind.Trait } trait)
            {
                // std traits are modelled with a single `T` standing for the receiver's first argument
                if (trait.Crate.IsStd && receiver.Args.Length > 0)
                {
                    map.TryAdd("T", receiver.Args[0]);
                }

                map["Self"] = receiver;
            }
        }

        if (self is not null && !map.ContainsKey("Self"))
        {
            map["Self"] = receiver ?? self;
        }

        var result = Subst(ret, map);
        return result is { Param: "Self" } ? receiver : result;
    }

    /// <summary>The element type produced when iterating <paramref name="t"/>.</summary>
    public static RType? ElementType(RType? t)
    {
        if (t?.Def is null)
        {
            return null;
        }

        return t.Def.Name switch
        {
            "Vec" or "VecDeque" or "slice" or "Iter" or "HashSet" or "BTreeSet" or "Option" => t.Args.FirstOrDefault(),
            _ => null,
        };
    }

    /// <summary>Smart-pointer / deref hop used when a method is not found on <paramref name="t"/>.</summary>
    public RType? DerefOf(RType t)
    {
        if (t.Def is null)
        {
            return null;
        }

        if (t.Def.Crate.IsStd && Transparent.Contains(t.Def.Name))
        {
            return t.Args.FirstOrDefault();
        }

        return t.Def.Name switch
        {
            "String" => RType.Of(StrItem),
            "Vec" => RType.Of(SliceItem, t.Args.FirstOrDefault()),
            "PathBuf" when _stdTypeNames.GetValueOrDefault("Path") is { } path => RType.Of(path),
            "OsString" when _stdTypeNames.GetValueOrDefault("OsStr") is { } os => RType.Of(os),
            _ => null,
        };
    }

    public Item? FindMethod(RType? receiver, string name)
    {
        for (var hops = 0; receiver?.Def is not null && hops < 4; hops++)
        {
            var found = FindAssoc(receiver.Def, name, methodsOnly: true);
            if (found is not null)
            {
                return found;
            }

            receiver = DerefOf(receiver);
        }

        return null;
    }

    public RType? ReceiverAfterHops(RType? receiver, string name)
    {
        for (var hops = 0; receiver?.Def is not null && hops < 4; hops++)
        {
            if (FindAssoc(receiver.Def, name, methodsOnly: true) is not null)
            {
                return receiver;
            }

            receiver = DerefOf(receiver);
        }

        return receiver;
    }
}
