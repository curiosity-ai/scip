using System.Collections.Generic;
using ScipSharp.Indexer.Php.Composer;
using ScipSharp.Indexer.Php.Syntax;

namespace ScipSharp.Indexer.Php;

/// <summary>Maps byte offsets to zero-based line / character (UTF-8 code unit) positions.</summary>
internal sealed class LineIndex
{
    private readonly List<int> _starts = [0];

    public LineIndex(string source)
    {
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                _starts.Add(i + 1);
            }
        }
    }

    public (int Line, int Character) At(int offset)
    {
        var i = _starts.BinarySearch(offset);
        if (i < 0)
        {
            i = ~i - 1;
        }

        return (i, offset - _starts[i]);
    }

}

/// <summary>Port of <c>ScipPhp\DocIndexer</c>: turns the nodes of one file into SCIP occurrences and symbol information.</summary>
internal sealed class DocIndexer
{
    private readonly ComposerProject _composer;
    private readonly SymbolNamer _namer;
    private readonly Types _types;
    private readonly DocGenerator _docGenerator;
    private readonly LineIndex _lines;

    public DocIndexer(ComposerProject composer, SymbolNamer namer, Types types, string source)
    {
        _composer = composer;
        _namer = namer;
        _types = types;
        _docGenerator = new DocGenerator(source);
        _lines = new LineIndex(source);
    }

    public OrderedDictionary<string, SymbolInformation> Symbols { get; } = [];

    public Dictionary<string, SymbolInformation> ExtSymbols { get; } = [];

    public List<Occurrence> Occurrences { get; } = [];

    public void Index(Node n)
    {
        // ------- Definitions -------
        switch (n)
        {
            case ClassConst cc:
                foreach (var c in cc.Consts)
                {
                    Def(c, c.Name, SyntaxKind.IdentifierConstant);
                }

                return;
            case ClassLike { Name: null } anon:
            {
                // Anonymous classes have no definition occurrence, but `static::`/`self::` and members refer to them.
                if (_namer.Name(anon) is { } anonSymbol)
                {
                    anonSymbol = SymbolNamer.Escape(anonSymbol);
                    if (!Symbols.ContainsKey(anonSymbol))
                    {
                        var anonInfo = new SymbolInformation { Symbol = anonSymbol };
                        Symbols[anonSymbol] = anonInfo;
                    }
                }

                return;
            }

            case ClassLike { Name: { } cname } cl:
            {
                Def(cl, cname, SyntaxKind.IdentifierType);
                var name = _namer.Name(cl);
                if (name == null)
                {
                    return;
                }

                var tags = _types.Tags(cl);
                foreach (var p in tags.Properties)
                {
                    var propName = p.Name.TrimStart('$');
                    if (p.Name == "" || propName == "")
                    {
                        continue;
                    }

                    DocDef(_namer.NameProp(name, propName), p.NameStart, p.NameEnd);
                }

                foreach (var m in tags.Methods)
                {
                    if (m.Name == "")
                    {
                        continue;
                    }

                    DocDef(_namer.NameMeth(name, m.Name), m.NameStart, m.NameEnd);
                }

                return;
            }

            case ClassMethod m:
                Def(m, m.Name, SyntaxKind.Identifier);
                return;
            case EnumCase ec:
                Def(ec, ec.Name, SyntaxKind.IdentifierConstant);
                return;
            case FunctionStmt f:
                Def(f, f.Name, SyntaxKind.IdentifierFunctionDefinition);
                return;
            case Param { Var.Name: { } } p:
                // Constructor property promotion.
                Def(p.Flags != Mod.None ? Types.Promote(p) : (Node)p, p.Var, SyntaxKind.IdentifierParameter);
                return;
            case PropertyStmt ps:
                foreach (var item in ps.Props)
                {
                    Def(item, item.Name, SyntaxKind.Identifier);
                }

                return;
        }

        // ------- Usages -------
        switch (n)
        {
            case ClassConstFetch { NameNode: Ident { Name: not "" } id } cf:
            {
                var symbol = _types.ConstDef(cf.Class, id.Name);
                if (symbol != null)
                {
                    Ref(symbol, id, SyntaxKind.IdentifierConstant);
                }

                return;
            }

            case MethodCall { NameNode: Ident { Name: not "" } id } mc:
            {
                var symbol = _types.MethDef(mc.Var, id.Name);
                if (symbol != null)
                {
                    Ref(symbol, id, SyntaxKind.Identifier);
                }

                return;
            }

            case StaticCall { NameNode: Ident { Name: not "" } id } sc:
            {
                var symbol = _types.MethDef(sc.Class, id.Name);
                if (symbol != null)
                {
                    Ref(symbol, id, SyntaxKind.Identifier);
                }

                return;
            }

            case Name name:
            {
                if (name.Parent is NamespaceStmt)
                {
                    return;
                }

                var symbol = _types.NameDef(name);
                if (symbol != null)
                {
                    Ref(symbol, name, SyntaxKind.Identifier);
                }

                return;
            }

            case PropertyFetch { NameNode: Ident { Name: not "" } id } pf:
            {
                var symbol = _types.PropDef(pf.Var, id.Name);
                if (symbol != null)
                {
                    Ref(symbol, id, SyntaxKind.Identifier);
                }

                return;
            }

            case StaticPropertyFetch { NameNode: Ident { Name: not "" } id } spf:
            {
                var symbol = _types.PropDef(spf.Class, id.Name);
                if (symbol != null)
                {
                    Ref(symbol, id, SyntaxKind.Identifier);
                }

                return;
            }
        }
    }

    private void Def(Node n, Node posNode, SyntaxKind kind)
    {
        var symbol = _namer.Name(n);
        if (symbol == null)
        {
            return;
        }

        symbol = SymbolNamer.Escape(symbol);
        var info = new SymbolInformation { Symbol = symbol };
        info.Documentation.AddRange(_docGenerator.Create(n));
        Symbols[symbol] = info;
        Occurrences.Add(MakeOccurrence(posNode.Start, posNode.End, symbol, SymbolRole.Definition, kind));
    }

    private void DocDef(string symbol, int start, int end)
    {
        symbol = SymbolNamer.Escape(symbol);
        Symbols.TryAdd(symbol, new SymbolInformation { Symbol = symbol }); // lint: every definition needs SymbolInformation
        Occurrences.Add(MakeOccurrence(start, end, symbol, SymbolRole.Definition, SyntaxKind.Identifier));
    }

    private void Ref(string symbol, Node posNode, SyntaxKind kind)
    {
        if (!symbol.StartsWith("local ", StringComparison.Ordinal))
        {
            var ident = _namer.ExtractIdent(symbol);
            if (ident != null && _composer.IsDependency(ident))
            {
                ExtSymbols[SymbolNamer.Escape(symbol)] = new SymbolInformation { Symbol = SymbolNamer.Escape(symbol) };
            }
        }

        symbol = SymbolNamer.Escape(symbol);

        Occurrences.Add(MakeOccurrence(posNode.Start, posNode.End, symbol, SymbolRole.UnspecifiedSymbolRole, kind));
    }

    private Occurrence MakeOccurrence(int start, int end, string symbol, SymbolRole role, SyntaxKind kind)
    {
        var occ = new Occurrence { Symbol = symbol, SymbolRoles = (int)role, SyntaxKind = kind };
        var (sl, sc) = _lines.At(start);
        var (el, ec) = _lines.At(end);
        if (sl == el)
        {
            occ.SingleLineRange = new SingleLineRange { Line = sl, StartCharacter = sc, EndCharacter = ec };
        }
        else
        {
            occ.MultiLineRange = new MultiLineRange { StartLine = sl, StartCharacter = sc, EndLine = el, EndCharacter = ec };
        }

        return occ;
    }
}
