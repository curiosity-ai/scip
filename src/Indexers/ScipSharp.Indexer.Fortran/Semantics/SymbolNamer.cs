using static ScipSharp.SymbolDescriptor.Types;

namespace ScipSharp.Indexer.Fortran.Semantics;

/// <summary>
/// Builds SCIP symbols: <c>scip-fortran &lt;manager&gt; &lt;package&gt; &lt;version&gt; &lt;descriptors&gt;</c>. Names are
/// lower-cased (Fortran is case-insensitive). Returns null for entities that get <c>local N</c> symbols.
/// </summary>
internal sealed class SymbolNamer(string manager, string package, string version)
{
    public const string Scheme = "scip-fortran";

    public string Manager { get; } = manager;

    public string Package { get; } = package;

    public string Version { get; } = version;

    /// <summary>The global symbol of <paramref name="e"/>, or null when it is local to its document.</summary>
    public string? Symbol(Entity e)
    {
        if (e.CachedSymbol is not null)
        {
            return e.CachedSymbol.Length == 0 ? null : e.CachedSymbol;
        }

        var descriptors = Descriptors(e, 0);
        string result;
        if (descriptors is null)
        {
            result = "";
        }
        else
        {
            var unknown = e.Kind == EntityKind.Unknown && e.UnknownModule is not null;
            var symbol = new Symbol
            {
                Scheme = Scheme,
                Package = unknown
                    ? new Package { Manager = "", Name = "", Version = "" }
                    : new Package { Manager = Manager, Name = Package, Version = Version },
            };
            symbol.Descriptors.AddRange(descriptors);
            result = SymbolFormatter.Verbose.Format(symbol);
        }

        e.CachedSymbol = result;
        return result.Length == 0 ? null : result;
    }

    private List<SymbolDescriptor>? Descriptors(Entity e, int depth)
    {
        if (depth > 32)
        {
            return null;
        }

        if (e.Canonical is not null && e.Canonical != e)
        {
            return Descriptors(e.Canonical, depth + 1);
        }

        switch (e.Kind)
        {
            case EntityKind.Intrinsic:
            case EntityKind.Label:
            case EntityKind.ConstructName:
            case EntityKind.Dummy:
                return null;
            case EntityKind.Module:
            case EntityKind.Program:
            case EntityKind.BlockData:
                return [D(e.Name.Length == 0 ? "main" : e.Name, Suffix.Namespace)];
            case EntityKind.Submodule:
                return [D(e.Body?.AncestorName ?? "_", Suffix.Namespace), D(e.Name, Suffix.Namespace)];
            case EntityKind.CommonBlock:
                return [D("/" + e.Name + "/", Suffix.Type)];
            case EntityKind.CommonMember when e.Common is not null:
                return [D("/" + e.Common.Name + "/", Suffix.Type), D(e.Name, Suffix.Term)];
            case EntityKind.Unknown:
                if (e.IsExternalProc)
                {
                    return [D(e.Name, Suffix.Method)];
                }

                if (e.UnknownModule is { Length: 0 })
                {
                    return [D(e.Name, Suffix.Namespace)];
                }

                return e.UnknownModule is null ? null : [D(e.UnknownModule, Suffix.Namespace), D(e.Name, Suffix.Term)];
        }

        if (e.IsExternalProc)
        {
            return [Own(e)];
        }

        var prefix = Prefix(e.Scope, e, depth);
        if (prefix is null)
        {
            // Entities declared by an INCLUDEd file inside a subprogram are shared by every includer.
            if (e.DefToken is { File.IsInclude: true } tok && e.Kind is not EntityKind.StatementFunction)
            {
                return [D(tok.File.RelativePath, Suffix.Namespace), Own(e)];
            }

            return null;
        }

        prefix.Add(Own(e));
        return prefix;
    }

    private List<SymbolDescriptor>? Prefix(Scope scope, Entity e, int depth)
    {
        switch (scope.Kind)
        {
            case ScopeKind.File:
                return e.IsProcedureLike ? [] : null;
            case ScopeKind.Module:
            case ScopeKind.Submodule:
                return scope.Owner is null ? null : Descriptors(scope.Owner, depth + 1);
            case ScopeKind.Program:
            case ScopeKind.BlockData:
            case ScopeKind.Procedure:
                if (scope.IsInterfaceBody || scope.Owner is null || e.Kind is not (EntityKind.Subroutine or EntityKind.Function
                        or EntityKind.AbstractInterface or EntityKind.Generic))
                {
                    return null;
                }

                return Descriptors(scope.Owner, depth + 1);
            case ScopeKind.DerivedType:
                return scope.Owner is null ? null : Descriptors(scope.Owner, depth + 1);
            case ScopeKind.Interface:
                return scope.Parent is null ? null : Prefix(scope.Parent, e, depth + 1);
            default:
                return null;
        }
    }

    private static SymbolDescriptor Own(Entity e) => e.Kind switch
    {
        EntityKind.Subroutine or EntityKind.Function or EntityKind.AbstractInterface or EntityKind.Binding
            or EntityKind.GenericBinding or EntityKind.StatementFunction => D(e.Name, Suffix.Method),
        EntityKind.Generic => new SymbolDescriptor { Name = e.Name, Suffix = Suffix.Method, Disambiguator = e.GenericCollision ? "generic" : "" },
        EntityKind.DerivedType => D(e.Name, Suffix.Type),
        EntityKind.TypeParam => D(e.Name, Suffix.TypeParameter),
        EntityKind.Module or EntityKind.Submodule or EntityKind.Program or EntityKind.BlockData => D(e.Name, Suffix.Namespace),
        _ => D(e.Name, Suffix.Term),
    };

    private static SymbolDescriptor D(string name, Suffix suffix) => new() { Name = name, Suffix = suffix };
}
