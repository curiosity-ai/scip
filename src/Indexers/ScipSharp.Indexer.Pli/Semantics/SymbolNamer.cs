using ScipSharp.Indexer.Pli.Syntax;
using Suffix = ScipSharp.SymbolDescriptor.Types.Suffix;

namespace ScipSharp.Indexer.Pli.Semantics;

/// <summary>
/// Builds <c>scip-pli</c> symbols. External linkage in PL/I is by name only, so external procedures, their secondary
/// entry points and EXTERNAL variables are top-level descriptors (<c>NAME().</c>, <c>NAME.</c>) shared by every file of
/// the package; everything else nests under its block (<c>OUTER().INNER().X.</c>, <c>PKG/P().(PARM)</c>).
/// </summary>
internal sealed class SymbolNamer(string packageName, string packageVersion)
{
    public const string Scheme = "scip-pli";
    public const string Manager = "pli";
    public const string BuiltinPackage = "builtin";

    private readonly Dictionary<string, Decl> _globals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Decl> _builtins = new(StringComparer.Ordinal);

    /// <summary>External procedures and entry points of all units, by normalized name (also <c>SEG$ENTRY</c>).</summary>
    public IReadOnlyDictionary<string, Decl> Globals => _globals;

    public IEnumerable<Decl> UsedBuiltins => _builtins.Values;

    public string Format(IEnumerable<SymbolDescriptor> descriptors, string? package = null)
    {
        var symbol = new Symbol
        {
            Scheme = Scheme,
            Package = new Package { Manager = Manager, Name = package ?? packageName, Version = package is null ? packageVersion : "" },
        };
        symbol.Descriptors.AddRange(descriptors);
        return SymbolFormatter.Verbose.Format(symbol);
    }

    private static SymbolDescriptor D(string name, Suffix suffix) => new() { Name = name, Suffix = suffix };

    /// <summary>Registers the externally visible procedures of a parsed unit (call before <see cref="AssignSymbols"/>).</summary>
    public void RegisterGlobals(Unit unit)
    {
        foreach (var d in unit.Decls)
        {
            if (d.Kind is DeclKind.Procedure && d.OwnBlock is { IsExternalProcedure: true } && d.Definitions.Count > 0)
            {
                d.Symbol ??= ProcedureSymbol(d);
                _globals.TryAdd(d.Name, d);
                _globals.TryAdd(d.Name + "$" + d.Name, d);
            }
            else if (d.Kind is DeclKind.Entry && d.OwnBlock is { IsExternalProcedure: true } proc)
            {
                d.Symbol ??= EntrySymbol(d);
                _globals.TryAdd(d.Name, d);
                if (proc.Decl is { } p)
                {
                    _globals.TryAdd(p.Name + "$" + d.Name, d);
                }
            }
        }
    }

    /// <summary>Assigns <see cref="Decl.Symbol"/> to every declaration of the unit (locals stay null).</summary>
    public void AssignSymbols(Unit unit)
    {
        foreach (var d in unit.Decls)
        {
            d.Symbol ??= SymbolFor(d);
        }
    }

    public Decl Builtin(string displayName, string normalized)
    {
        var key = normalized.ToUpperInvariant();
        if (!_builtins.TryGetValue(key, out var d))
        {
            d = new Decl(key, displayName, DeclKind.Builtin, new Block(BlockKind.File, null, null))
            {
                Symbol = BuiltinSymbol(key),
                Signature = Keywords.StandardFiles.Contains(key) ? $"{key} FILE (standard file)" : $"{key} BUILTIN",
            };
            _builtins[key] = d;
        }

        return d;
    }

    private string BuiltinSymbol(string name) =>
        Format([D(name, Keywords.StandardFiles.Contains(name) ? Suffix.Term : Suffix.Method)], BuiltinPackage);

    /// <summary>A symbol for an undeclared external entry (CALL of an unknown name, Multics <c>seg$entry</c>).</summary>
    public string ExternalEntrySymbol(string name) => Format([D(name, Suffix.Method)]);

    private string? SymbolFor(Decl d)
    {
        switch (d.Kind)
        {
            case DeclKind.Implicit:
                return null;
            case DeclKind.Builtin:
                return Builtin(d.DisplayName, d.Name).Symbol;
            case DeclKind.Procedure:
                return ProcedureSymbol(d);
            case DeclKind.Entry:
                return EntrySymbol(d);
            case DeclKind.EntryDeclaration when d.Parent is null:
                {
                    // An internal procedure of that name in scope, a known external procedure, or an external entry.
                    var target = FindProcedure(d.Block, d.Name) ?? (_globals.TryGetValue(d.Name, out var g) ? g : null);
                    if (target is not null && target != d)
                    {
                        target.Symbol ??= SymbolFor(target);
                        d.DeclarationReferences.AddRange(d.Definitions);
                        d.Definitions.Clear();
                        return target.Symbol;
                    }

                    return ExternalEntrySymbol(d.Name);
                }

            case DeclKind.Package:
                return Format([D(d.Name, Suffix.Namespace)]);
            case DeclKind.BeginBlock when d.OwnBlock is { } b:
                return Format(BlockPrefix(b));
            case DeclKind.PreprocessorVariable or DeclKind.PreprocessorProcedure:
                return Format([.. BlockPrefix(Root(d.Block)), D(d.Name, Suffix.Macro)]);
            case DeclKind.OrdinalValue:
                return Format([.. BlockPrefix(d.Block), D(d.Name, Suffix.Term)]);
            default:
                {
                    var path = new List<Decl>();
                    for (var x = d; x is not null; x = x.Parent)
                    {
                        path.Add(x);
                    }

                    path.Reverse();

                    var root = path[0];
                    var descriptors = root.IsExternal && root.Kind != DeclKind.Parameter ? [] : BlockPrefix(root.Block);
                    foreach (var x in path)
                    {
                        descriptors.Add(x == root
                            ? root.Kind switch
                            {
                                DeclKind.Parameter => D(x.Name, Suffix.Parameter),
                                DeclKind.Type => D(x.Name, Suffix.Type),
                                _ => D(x.Name, Suffix.Term),
                            }
                            : D(x.Name, Suffix.Term));
                    }

                    return Format(descriptors);
                }
        }
    }

    private static Decl? FindProcedure(Block block, string name)
    {
        for (var b = block; b is not null; b = b.Parent)
        {
            if (b.ByName.TryGetValue(name, out var list) && list.Find(static x => x.Kind is DeclKind.Procedure or DeclKind.Entry) is { } p)
            {
                return p;
            }
        }

        return null;
    }

    private static Block Root(Block b)
    {
        while (b.Parent is not null)
        {
            b = b.Parent;
        }

        return b;
    }

    private string ProcedureSymbol(Decl d) => Format(BlockPrefix(d.OwnBlock!));

    private string EntrySymbol(Decl d)
    {
        var proc = d.OwnBlock!;
        var prefix = proc.Parent is { Kind: BlockKind.File } ? [] : BlockPrefix(proc.Parent ?? proc);
        prefix.Add(new SymbolDescriptor { Name = d.Name, Suffix = Suffix.Method });
        return Format(prefix);
    }

    /// <summary>Descriptors naming a block; external procedures (directly in the file) are top level.</summary>
    internal static List<SymbolDescriptor> BlockPrefix(Block b)
    {
        // Iterative (blocks can nest arbitrarily deep in malformed input): collect innermost first, then reverse.
        var prefix = new List<SymbolDescriptor>();
        for (Block? x = b; x is not null; x = x.Parent)
        {
            var name = x.Decl?.Name ?? x.AnonymousName ?? "$block";
            if (x.Kind is BlockKind.File or BlockKind.Package)
            {
                prefix.Add(D(name, Suffix.Namespace));
                break;
            }

            prefix.Add(D(name, x.Kind switch
            {
                BlockKind.Procedure => Suffix.Method,
                BlockKind.PreprocessorProcedure => Suffix.Macro,
                _ => Suffix.Namespace,
            }));
            if (x.Kind == BlockKind.Procedure && x.Parent is null or { Kind: BlockKind.File })
            {
                break;
            }
        }

        prefix.Reverse();
        return prefix;
    }
}
