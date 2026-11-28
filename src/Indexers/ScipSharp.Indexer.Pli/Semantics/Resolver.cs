using System.Runtime.CompilerServices;
using ScipSharp.Indexer.Pli.Syntax;

namespace ScipSharp.Indexer.Pli.Semantics;

/// <summary>A resolved name occurrence.</summary>
/// <param name="Token">The name token.</param>
/// <param name="Decl">What it refers to.</param>
/// <param name="Roles">SCIP symbol roles.</param>
internal sealed record RawOccurrence(Token Token, Decl Decl, int Roles);

/// <summary>
/// Second pass: resolves the recorded token ranges against the block tree. Lookup walks from the innermost block outwards;
/// structure references may be fully or partially qualified (<c>A.B.C</c>, <c>A.C</c>, <c>C</c>) and follow
/// <c>LIKE</c>/<c>TYPE</c> templates. Unresolved names become built-in references, keywords (skipped), external entries
/// (CALL targets, <c>seg$entry</c>) or implicit declarations in the outermost procedure.
/// </summary>
internal sealed class Resolver(SymbolNamer namer, Func<string, string> normalize)
{
    private const int MaxTemplateDepth = 16;

    private readonly Dictionary<string, Decl> _implicitExternals = new(StringComparer.Ordinal);
    private Unit _unit = null!;
    private List<RawOccurrence> _out = null!;

    public List<RawOccurrence> Resolve(Unit unit)
    {
        _unit = unit;
        _out = [];
        _implicitExternals.Clear();
        foreach (var d in unit.Decls.ToList())
        {
            foreach (var t in d.Definitions)
            {
                _out.Add(new RawOccurrence(t, d, (int)SymbolRole.Definition));
            }

            foreach (var t in d.DeclarationReferences)
            {
                _out.Add(new RawOccurrence(t, d, 0));
            }
        }

        foreach (var site in unit.References)
        {
            Scan(site, site.Start, site.End, site.Mode == RefMode.Attributes, depth0: true, first: true);
        }

        return _out;
    }

    /// <summary>Declarations created while resolving (implicit declarations and implicit external entries).</summary>
    public IEnumerable<Decl> ImplicitExternals => _implicitExternals.Values;

    private void Scan(RefSite s, int from, int to, bool attributes, bool depth0, bool first)
    {
        // Parentheses and call arguments recurse: on pathological nesting leave the inner names unresolved.
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return;
        }

        var t = s.Tokens;
        var j = from;
        var isFirstChain = first;
        while (j < to)
        {
            var tok = t[j];
            if (tok.Is("("))
            {
                var close = UnitParser.MatchParen(t, j, to);
                Scan(s, j + 1, close, attributes: false, depth0: false, first: false);
                j = close + 1;
                continue;
            }

            if (s.Mode == RefMode.Sql)
            {
                if (tok.Is(":") && j + 1 < to && t[j + 1].IsIdent)
                {
                    j = Chain(s, j + 1, to, attributes, depth0, ref isFirstChain);
                    continue;
                }

                j++;
                continue;
            }

            if (!tok.IsIdent)
            {
                j++;
                continue;
            }

            if (attributes && (Keywords.Attributes.Contains(tok.Text) || Keywords.AttributeListAttributes.Contains(tok.Text)) && !IsDeclared(s, tok))
            {
                j++;
                if (j < to && t[j].Is("("))
                {
                    var close = UnitParser.MatchParen(t, j, to);
                    if (!Keywords.OpaqueAttributes.Contains(tok.Text))
                    {
                        Scan(s, j + 1, close, Keywords.AttributeListAttributes.Contains(tok.Text), depth0: false, first: false);
                    }

                    j = close + 1;
                }

                continue;
            }

            if (depth0 && s.Options is { } options && options.Contains(tok.Text))
            {
                j++;
                continue;
            }

            j = Chain(s, j, to, attributes, depth0, ref isFirstChain);
        }
    }

    /// <summary>True when an attribute keyword is redeclared as a program name (BUILTIN declarations do not count).</summary>
    private bool IsDeclared(RefSite s, Token tok) =>
        Lookup(s.Block, [normalize(tok.Text)], s.Preprocessor) is [{ Kind: not DeclKind.Builtin }, ..];

    /// <summary>Parses a reference <c>a(args).b.c(args)</c> at <paramref name="j"/>, resolves it and scans the arguments.</summary>
    private int Chain(RefSite s, int j, int to, bool attributes, bool depth0, ref bool isFirstChain)
    {
        var t = s.Tokens;
        var parts = new List<Token> { t[j] };
        var args = new List<(int, int)>();
        j++;
        while (j < to)
        {
            if (t[j].Is("("))
            {
                var close = UnitParser.MatchParen(t, j, to);
                args.Add((j + 1, close));
                j = close + 1;
                continue;
            }

            if (j + 1 < to && t[j].Is(".") && t[j + 1].IsIdent)
            {
                parts.Add(t[j + 1]);
                j += 2;
                continue;
            }

            break;
        }

        var call = s.Mode == RefMode.Call && isFirstChain && depth0;
        var write = s.Write && depth0;
        isFirstChain = false;
        ResolveChain(s, parts, args.Count > 0, call, write, attributes);
        foreach (var (a, b) in args)
        {
            Scan(s, a, b, attributes: false, depth0: false, first: false);
        }

        return j;
    }

    private void ResolveChain(RefSite s, List<Token> parts, bool hasArgs, bool call, bool write, bool attributes)
    {
        var names = parts.ConvertAll(p => normalize(p.Text));
        var roles = write ? (int)SymbolRole.WriteAccess : 0;
        var match = Lookup(s.Block, names, s.Preprocessor);
        if (match is not null)
        {
            Emit(parts, match, roles);
            return;
        }

        if (parts.Count > 1 && Lookup(s.Block, [names[0]], s.Preprocessor) is { } head)
        {
            Emit([parts[0]], head, roles);
            return;
        }

        if (s.Mode is RefMode.DeclaredOnly or RefMode.Exports or RefMode.Sql || attributes || s.Preprocessor)
        {
            return;
        }

        var tok = parts[0];
        var name = names[0];
        if (call || name.IndexOf('$', 1) > 0)
        {
            _out.Add(new RawOccurrence(tok, External(tok, name), roles));
            return;
        }

        if (hasArgs && namer.Globals.TryGetValue(name, out var global))
        {
            _out.Add(new RawOccurrence(tok, global, roles));
            return;
        }

        if (Keywords.Builtins.Contains(name))
        {
            _out.Add(new RawOccurrence(tok, namer.Builtin(tok.Text, name), roles));
            return;
        }

        if (Keywords.IsKeyword(name) || (s.FormatItems && Keywords.FormatItems.Contains(name)))
        {
            return;
        }

        var outer = s.Block.Outermost;
        var implicitDecl = new Decl(name, tok.Text, DeclKind.Implicit, outer) { Signature = $"{tok.Text} (implicitly declared)" };
        outer.Add(implicitDecl);
        _unit.Decls.Add(implicitDecl);
        _out.Add(new RawOccurrence(tok, implicitDecl, roles));
    }

    private Decl External(Token tok, string name)
    {
        if (namer.Globals.TryGetValue(name, out var global))
        {
            return global;
        }

        if (!_implicitExternals.TryGetValue(name, out var d))
        {
            d = new Decl(name, tok.Text, DeclKind.EntryDeclaration, _unit.Root)
            {
                Symbol = namer.ExternalEntrySymbol(name),
                Signature = $"DCL {tok.Text} ENTRY EXTERNAL (implicitly declared)",
                IsExternal = true,
            };
            _implicitExternals[name] = d;
        }

        return d;
    }

    private void Emit(List<Token> parts, List<Decl> match, int roles)
    {
        for (var k = 0; k < parts.Count && k < match.Count; k++)
        {
            _out.Add(new RawOccurrence(parts[k], match[k], k == parts.Count - 1 ? roles : 0));
        }
    }

    /// <summary>
    /// Finds the declarations matching a (possibly partially) qualified name: innermost block first; within a block the
    /// match with the fewest omitted qualification levels wins.
    /// </summary>
    internal List<Decl>? Lookup(Block block, List<string> names, bool preprocessor)
    {
        if (!preprocessor && names.Count == 1 && _unit.Preprocessor.TryGetValue(names[0], out var pp))
        {
            return [pp];
        }

        for (var b = block; b is not null; b = b.Parent)
        {
            if (!b.ByName.TryGetValue(names[0], out var candidates))
            {
                continue;
            }

            List<Decl>? best = null;
            var bestScore = int.MaxValue;
            foreach (var c in candidates)
            {
                foreach (var (path, score) in Match(c, names, 1, [c], c.Level > 1 ? 1 : 0, 0))
                {
                    if (score < bestScore)
                    {
                        best = path;
                        bestScore = score;
                    }
                }
            }

            if (best is not null)
            {
                return best;
            }
        }

        if (preprocessor && names.Count == 1 && _unit.Preprocessor.TryGetValue(names[0], out var ppd))
        {
            return [ppd];
        }

        return null;
    }

    private IEnumerable<(List<Decl> Path, int Score)> Match(Decl decl, List<string> names, int i, List<Decl> path, int score, int depth)
    {
        if (i == names.Count)
        {
            yield return (path, score);
            yield break;
        }

        if (depth > MaxTemplateDepth)
        {
            yield break;
        }

        foreach (var (child, skipped) in Descendants(decl, 0, depth))
        {
            if (child.Name == names[i])
            {
                foreach (var r in Match(child, names, i + 1, [.. path, child], score + skipped, depth + 1))
                {
                    yield return r;
                }
            }
        }
    }

    /// <summary>All structure members below <paramref name="decl"/> with the number of skipped levels.</summary>
    private IEnumerable<(Decl Child, int Skipped)> Descendants(Decl decl, int skipped, int depth)
    {
        if (depth > MaxTemplateDepth)
        {
            yield break;
        }

        foreach (var c in EffectiveChildren(decl, depth))
        {
            yield return (c, skipped);
            foreach (var r in Descendants(c, skipped + 1, depth + 1))
            {
                yield return r;
            }
        }
    }

    private List<Decl> EffectiveChildren(Decl decl, int depth)
    {
        if (decl.Children.Count > 0)
        {
            return decl.Children;
        }

        return Template(decl, depth)?.Children ?? [];
    }

    private Decl? Template(Decl decl, int depth)
    {
        if (decl.TemplateResolved)
        {
            return decl.ResolvedTemplate;
        }

        decl.TemplateResolved = true;
        var chain = decl.LikeTarget ?? decl.TypeTarget;
        if (chain is { Count: > 0 } && depth < MaxTemplateDepth)
        {
            var path = Lookup(decl.Block, chain.ConvertAll(c => normalize(c.Text)), preprocessor: false);
            decl.ResolvedTemplate = path is { Count: > 0 } && path[^1] != decl ? path[^1] : null;
        }

        return decl.ResolvedTemplate;
    }
}
