namespace Scip.Indexer.Rust;

/// <summary>A scanned cargo target.</summary>
public sealed record ScanUnit(CargoPackage Package, CargoTarget Target, RustCrateScanner Scanner);

/// <summary>
/// Drives the semantic part after scanning: builds the cross-crate item graph (incl. the built-in std model), wires
/// dependencies, then resolves references in every file and collects the external symbols that were referenced.
/// </summary>
public static class RustAnalysis
{
    /// <summary>Members of trait impls without their own documentation show the documentation of the trait item.</summary>
    private static void InheritTraitDocs(RustWorld world, IEnumerable<CrateData> crates)
    {
        Dictionary<string, SymbolInformation> infos = [];
        foreach (var crate in crates)
        {
            foreach (var file in crate.Files)
            {
                foreach (var info in file.Symbols)
                {
                    infos.TryAdd(info.Symbol, info);
                }
            }
        }

        foreach (var impl in world.AllImpls)
        {
            if (impl.TraitDef is not { IsExternal: false } trait)
            {
                continue;
            }

            foreach (var (name, members) in impl.Members)
            {
                if (infos.TryGetValue(members[0].Symbol, out var info) && info.Documentation.Count == 0
                    && trait.Members.TryGetValue(name, out var traitMembers)
                    && infos.TryGetValue(traitMembers[0].Symbol, out var traitInfo) && traitInfo.Documentation.Count > 0)
                {
                    info.Documentation.AddRange(traitInfo.Documentation);
                }
            }
        }
    }

    /// <param name="units">All scanned targets.</param>
    /// <param name="isExternalPackage">Packages whose sources are outside the project (their documents are not emitted).</param>
    /// <returns>SymbolInformation of referenced symbols defined outside the index (std, external path deps).</returns>
    public static IReadOnlyList<SymbolInformation> Analyze(IReadOnlyList<ScanUnit> units, Func<CargoPackage, bool>? isExternalPackage = null)
    {
        var world = new RustWorld();
        Dictionary<ScanUnit, CrateData> crates = [];
        Dictionary<string, CrateData> libByPackage = [];
        foreach (var u in units)
        {
            var crate = new CrateData(u.Target.Name, u.Package.Information, u.Target.Kind, isStd: false)
            {
                External = isExternalPackage?.Invoke(u.Package) ?? false,
            };
            world.AddCrate(crate, u.Scanner.Files);
            crates[u] = crate;
            if (u.Target.Kind is "lib" or "proc-macro")
            {
                libByPackage[u.Package.Name] = crate;
            }
        }

        foreach (var (u, crate) in crates)
        {
            foreach (var dep in u.Package.Deps)
            {
                if (libByPackage.TryGetValue(dep.PackageName, out var lib))
                {
                    crate.Deps[dep.CrateName] = lib;
                }
            }

            if (u.Target.Kind is not ("lib" or "proc-macro") && libByPackage.TryGetValue(u.Package.Name, out var own))
            {
                crate.Deps[own.Name] = own;
            }
        }

        world.Prepare();
        InheritTraitDocs(world, crates.Values);
        Dictionary<string, Item> externals = [];
        foreach (var crate in crates.Values)
        {
            foreach (var file in crate.Files)
            {
                new RustResolver(world, world.Files[file], externals).Run();
            }
        }

        return
        [
            .. externals.Values.OrderBy(i => i.Symbol, StringComparer.Ordinal).Select(i => new SymbolInformation
            {
                Symbol = i.Symbol,
                Kind = i.Kind,
                DisplayName = i.Name,
            }),
        ];
    }
}
