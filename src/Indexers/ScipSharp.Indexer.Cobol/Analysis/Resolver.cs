using ScipSharp.Indexer.Cobol.Syntax;

namespace ScipSharp.Indexer.Cobol.Analysis;

/// <summary>
/// Pass 2: names every definition, records definition occurrences with enclosing ranges, and resolves references —
/// data names with OF/IN qualification (including file-name qualifiers and GLOBAL items of containing programs),
/// procedure names after PERFORM / THRU / GO TO / ALTER / INPUT|OUTPUT PROCEDURE (with OF section qualification),
/// file, index and special names, CALL / CANCEL 'literal' and EXEC CICS PROGRAM('literal') targets across programs,
/// INVOKE 'method', user FUNCTIONs, host variables in EXEC SQL and operands in EXEC CICS.
/// </summary>
internal sealed class Resolver
{
    private readonly ParsedSource _src;
    private readonly List<Token> _t;
    private readonly SymbolNamer _namer;
    private readonly ProjectCatalog _catalog;
    private readonly Func<int, DocumentAccumulator?> _document;
    private readonly Dictionary<string, SymbolInformation> _external;
    private readonly bool[] _consumed;

    public Resolver(ParsedSource source, SymbolNamer namer, ProjectCatalog catalog, Func<int, DocumentAccumulator?> document,
        Dictionary<string, SymbolInformation> external)
    {
        _src = source;
        _t = source.Tokens;
        _namer = namer;
        _catalog = catalog;
        _document = document;
        _external = external;
        _consumed = new bool[_t.Count];
    }

    public void Run(IEnumerable<CopyDirective> copies)
    {
        NameDefinitions();
        foreach (var copy in copies)
        {
            CopyReference(copy);
        }

        References();
    }

    // ---- definitions ------------------------------------------------------------------------------------------------

    private void NameDefinitions()
    {
        foreach (var unit in _src.Units)
        {
            if (!unit.IsPseudo)
            {
                unit.Symbol = _namer.ForUnit(unit);
                Register(unit, unit.FileId);
            }

            foreach (var item in unit.Items)
            {
                item.Symbol = _namer.ForItem(unit, item);
                Register(item, unit.FileId);
            }

            foreach (var p in unit.Procedures)
            {
                p.Symbol = _namer.ForProcedure(unit, p);
                Register(p, unit.FileId);
            }

            foreach (var f in unit.Files.Values)
            {
                f.Symbol = _namer.ForFile(unit, f);
                Register(f, unit.FileId);
            }

            foreach (var d in unit.Specials.Values.Concat(unit.Indexes.Values).Concat(unit.Entries))
            {
                d.Symbol = _namer.ForSimple(unit, d);
                Register(d, unit.FileId);
            }
        }

        // Indexes / specials / entries with duplicate names are not in the dictionaries but still have definition tokens.
        for (var i = 0; i < _t.Count; i++)
        {
            if (_src.DefAt[i] is { Symbol.Length: 0 } d && _src.UnitOf[i] is { } unit)
            {
                d.Symbol = d switch
                {
                    SimpleDefinition s => _namer.ForSimple(unit, s),
                    FileDefinition f => _namer.ForFile(unit, f),
                    _ => "",
                };
                if (d.Symbol.Length > 0)
                {
                    Register(d, unit.FileId);
                }
            }
        }
    }

    private void Register(Definition d, int mainFile)
    {
        var owner = _namer.EffectiveOwner(d is ProgramUnit ? -1 : d.Owner);
        var home = owner >= 0 ? owner : mainFile;
        var info = new SymbolInformation { Symbol = d.Symbol, Kind = d.Kind, DisplayName = d.DisplayName };
        if (d.Documentation.Length > 0)
        {
            info.Documentation.Add("```cobol\n" + d.Documentation + "\n```");
        }

        if (d is ProgramUnit { IsCommon: true })
        {
            info.Documentation.Add("COMMON program");
        }

        if (d is DataItem { Parent: null, File: { } file })
        {
            info.Documentation.Add($"Record of file `{file.DisplayName}`");
        }

        _document(home)?.AddSymbol(info);
    }

    private Range? Enclosing(Definition d)
    {
        if (d.StartIndex < 0 || d.EndIndex < d.StartIndex || d.EndIndex >= _t.Count
            || _t[d.StartIndex].Location is not { } start || _t[d.EndIndex].Location is not { } end
            || d.NameToken?.Location is not { } name || start.FileId != name.FileId || end.FileId != name.FileId
            || (end.Line, end.End).CompareTo((start.Line, start.Start)) < 0)
        {
            return null;
        }

        return new Range(start.Line, start.Start, end.Line, end.End);
    }

    private void Emit(Token token, string symbol, SymbolRole roles, Range? enclosing = null)
    {
        if (token.Location is not { } loc || symbol.Length == 0 || _document(loc.FileId) is not { } doc)
        {
            return;
        }

        var start = loc.Start;
        var end = loc.End;
        if (token.Kind == TokenKind.Literal && token.Text.Length == end - start && token.Text.Length >= 2 && token.Text[0] is '\'' or '"' && token.Text[^1] == token.Text[0])
        {
            start++;
            end--;
        }

        doc.AddOccurrence(new Range(loc.Line, start, loc.Line, end), symbol, roles, enclosing);
    }

    /// <summary>Defines the copybook's own symbol (a zero-width definition at the start of its document).</summary>
    public string? DefineCopybook(int fileId)
    {
        if (fileId < 0 || _document(fileId) is not { } target)
        {
            return null;
        }

        var symbol = _namer.CopybookSymbol(fileId);
        var info = new SymbolInformation { Symbol = symbol, Kind = SymbolInformation.Types.Kind.File, DisplayName = Path.GetFileName(target.RelativePath) };
        info.Documentation.Add($"copybook `{target.RelativePath}`");
        target.AddSymbol(info);
        target.AddOccurrence(new Range(0, 0, 0, 0), symbol, SymbolRole.Definition, null);
        return symbol;
    }

    private void CopyReference(CopyDirective copy)
    {
        if (DefineCopybook(copy.TargetFileId) is { } symbol)
        {
            Emit(copy.NameToken, symbol, SymbolRole.ReadAccess);
        }
    }

    // ---- references -------------------------------------------------------------------------------------------------

    private void References()
    {
        var goList = false;
        var execDepth = 0;
        for (var i = 0; i < _t.Count; i++)
        {
            var t = _t[i];
            if (_src.DefAt[i] is { } def)
            {
                Emit(t, def.Symbol, SymbolRole.Definition, Enclosing(def));
                goList = false;
                continue;
            }

            if (_src.RefAt[i] is { } known)
            {
                Emit(t, known.Symbol, SymbolRole.ReadAccess);
                continue;
            }

            var region = _src.RegionOf[i];
            if (region == Region.Exec)
            {
                if (t.Is("EXEC") || t.Is("EXECUTE"))
                {
                    execDepth = 0;
                }
                else if (t.Kind == TokenKind.LParen)
                {
                    execDepth++;
                }
                else if (t.Kind == TokenKind.RParen)
                {
                    execDepth = Math.Max(0, execDepth - 1);
                }
            }

            if (_consumed[i] || _src.UnitOf[i] is not { } unit)
            {
                continue;
            }

            switch (region)
            {
                case Region.Exec:
                    ExecReference(i, unit, execDepth);
                    continue;
                case Region.Data or Region.Environment or Region.Procedure:
                    break;
                default:
                    continue;
            }

            if (t.Kind == TokenKind.Literal)
            {
                if (region == Region.Procedure)
                {
                    LiteralReference(i, unit);
                }

                goList = false;
                continue;
            }

            if (!t.IsWord)
            {
                goList &= t.Kind == TokenKind.Comma;
                continue;
            }

            if (region == Region.Procedure)
            {
                if (t.Is("GO") || t.Is("ALTER"))
                {
                    goList = true;
                    continue;
                }

                if (goList && (t.Is("TO") || t.Is("PROCEED")))
                {
                    continue;
                }

                if (t.Is("DEPENDING") || (goList && Keywords.IsReserved(t.Upper)))
                {
                    goList = false;
                }
            }

            if (!t.IsUserWordShape)
            {
                continue;
            }

            var prev = Previous(i);
            if (prev is not null && prev.Is("FUNCTION"))
            {
                if (_catalog.Functions.TryGetValue(t.Upper, out var function))
                {
                    Emit(t, function, SymbolRole.ReadAccess);
                }

                continue;
            }

            if (_src.DataClauseRefs.TryGetValue(i, out var owner) && ResolveClauseOperand(i, unit, owner))
            {
                continue;
            }

            var procedureFirst = region == Region.Procedure && (goList || (prev is not null
                && (prev.Is("PERFORM") || prev.Is("THRU") || prev.Is("THROUGH") || prev.Is("PROCEDURE")
                    || (prev.Is("IS") && Previous(i - 1) is { } p2 && p2.Is("PROCEDURE")))));
            Resolve(i, unit, procedureFirst, allowProcedureFallback: false);
        }
    }

    private Token? Previous(int i)
    {
        for (var k = i - 1; k >= 0; k--)
        {
            if (_t[k].Kind != TokenKind.Comma)
            {
                return _t[k];
            }
        }

        return null;
    }

    private List<int> Qualifiers(int i)
    {
        var quals = new List<int>();
        var k = i + 1;
        while (k + 1 < _t.Count && (_t[k].Is("OF") || _t[k].Is("IN")) && _t[k + 1].IsUserWordShape)
        {
            quals.Add(k + 1);
            k += 2;
        }

        return quals;
    }

    private bool Resolve(int i, ProgramUnit unit, bool procedureFirst, bool allowProcedureFallback)
    {
        var t = _t[i];
        var quals = Qualifiers(i);
        if (procedureFirst && ResolveProcedure(i, unit, quals))
        {
            return true;
        }

        if (ResolveData(t.Upper, unit, quals) is { } match)
        {
            Emit(t, match.Item.Symbol, SymbolRole.ReadAccess);
            for (var q = 0; q < match.Qualifiers.Count; q++)
            {
                Emit(_t[quals[q]], match.Qualifiers[q].Symbol, SymbolRole.ReadAccess);
                _consumed[quals[q]] = true;
            }

            return true;
        }

        if (quals.Count == 0 && ResolveOther(t.Upper, unit) is { } other)
        {
            Emit(t, other.Symbol, SymbolRole.ReadAccess);
            return true;
        }

        return allowProcedureFallback && !procedureFirst && ResolveProcedure(i, unit, quals);
    }

    private sealed record DataMatch(DataItem Item, List<Definition> Qualifiers);

    private static DataMatch? ResolveData(string name, ProgramUnit unit, List<int> quals, List<Token> tokens)
    {
        DataItem? fallback = null;
        foreach (var scope in unit.SelfAndAncestors())
        {
            if (!scope.ItemsByName.TryGetValue(name, out var candidates))
            {
                continue;
            }

            foreach (var item in candidates)
            {
                if (scope != unit && !item.IsGlobal && scope.UnitKind != UnitKind.Class)
                {
                    continue;
                }

                fallback ??= item;
                if (Qualify(item, quals, tokens) is { } targets)
                {
                    return new DataMatch(item, targets);
                }
            }
        }

        return fallback is null ? null : new DataMatch(fallback, []);
    }

    private DataMatch? ResolveData(string name, ProgramUnit unit, List<int> quals) => ResolveData(name, unit, quals, _t);

    private static List<Definition>? Qualify(DataItem item, List<int> quals, List<Token> tokens)
    {
        var targets = new List<Definition>();
        var p = item.Parent;
        for (var q = 0; q < quals.Count; q++)
        {
            var name = tokens[quals[q]].Upper;
            var found = false;
            while (p is not null)
            {
                var current = p;
                p = p.Parent;
                if (current.IsNamed && current.Name == name)
                {
                    targets.Add(current);
                    found = true;
                    break;
                }
            }

            if (!found && q == quals.Count - 1 && item.Root.File is { } file && file.Name == name)
            {
                targets.Add(file);
                found = true;
            }

            if (!found)
            {
                return null;
            }
        }

        return targets;
    }

    private static Definition? ResolveOther(string name, ProgramUnit unit)
    {
        foreach (var scope in unit.SelfAndAncestors())
        {
            var own = scope == unit;
            if (scope.Files.TryGetValue(name, out var file) && (own || file.IsGlobal))
            {
                return file;
            }

            if (scope.Indexes.TryGetValue(name, out var index) && (own || (index.Item?.IsGlobal ?? false)))
            {
                return index;
            }

            if (scope.Specials.TryGetValue(name, out var special))
            {
                return special;
            }
        }

        return null;
    }

    private bool ResolveProcedure(int i, ProgramUnit unit, List<int> quals)
    {
        if (!unit.ProceduresByName.TryGetValue(_t[i].Upper, out var list))
        {
            return false;
        }

        Procedure? target = null;
        Procedure? sectionTarget = null;
        if (quals.Count > 0)
        {
            var sectionName = _t[quals[0]].Upper;
            target = list.FirstOrDefault(p => p.Section?.Name == sectionName);
            sectionTarget = target?.Section;
        }

        var current = _src.SectionOf[i];
        target ??= list.FirstOrDefault(p => p.Section == current && current is not null)
            ?? list.FirstOrDefault(p => p.IsSection)
            ?? list.FirstOrDefault(p => p.Section is null)
            ?? list[0];
        Emit(_t[i], target.Symbol, SymbolRole.ReadAccess);
        if (sectionTarget is not null)
        {
            Emit(_t[quals[0]], sectionTarget.Symbol, SymbolRole.ReadAccess);
            _consumed[quals[0]] = true;
        }

        return true;
    }

    /// <summary>REDEFINES / RENAMES operands refer to a preceding entry of the same record (or the preceding record).</summary>
    private bool ResolveClauseOperand(int i, ProgramUnit unit, DataItem owner)
    {
        var name = _t[i].Upper;
        DataItem? target = null;
        if (owner.Level == 66)
        {
            // RENAMES: an item of the immediately preceding record.
            var idx = unit.Items.IndexOf(owner);
            for (var k = idx - 1; k >= 0 && target is null; k--)
            {
                if (unit.Items[k].Name == name && unit.Items[k].IsNamed)
                {
                    target = unit.Items[k];
                }
            }
        }
        else
        {
            var siblings = owner.Parent?.Children ?? unit.Items.Where(x => x.Parent is null && x.Level == owner.Level).ToList();
            var at = siblings.IndexOf(owner);
            for (var k = (at < 0 ? siblings.Count : at) - 1; k >= 0 && target is null; k--)
            {
                if (siblings[k].IsNamed && siblings[k].Name == name)
                {
                    target = siblings[k];
                }
            }
        }

        if (target is null)
        {
            return false;
        }

        Emit(_t[i], target.Symbol, SymbolRole.ReadAccess);
        return true;
    }

    private void LiteralReference(int i, ProgramUnit unit)
    {
        var prev = Previous(i);
        if (prev is null)
        {
            return;
        }

        if (prev.Is("CALL") || prev.Is("CANCEL") || (prev.Is("ENTRY") && Previous(i - 1) is { } to && to.Is("TO")))
        {
            CallTarget(_t[i], unit);
            return;
        }

        if (i >= 2 && _t[i - 2].Is("INVOKE") && _catalog.Methods.TryGetValue(_t[i].Value.ToUpperInvariant(), out var methods) && methods.Count == 1)
        {
            Emit(_t[i], methods[0], SymbolRole.ReadAccess);
        }
    }

    private void CallTarget(Token literal, ProgramUnit unit)
    {
        var name = literal.Value.Trim().ToUpperInvariant();
        if (name.Length == 0)
        {
            return;
        }

        var local = unit.Children.FirstOrDefault(c => c.Name == name)
            ?? unit.Parent?.Children.FirstOrDefault(c => c.Name == name)
            ?? unit.SelfAndAncestors().FirstOrDefault(c => c.Name == name);
        if (local is not null && !local.IsPseudo)
        {
            Emit(literal, local.Symbol, SymbolRole.ReadAccess);
            return;
        }

        if (unit.SelfAndAncestors().SelectMany(u => u.Entries).FirstOrDefault(e => e.Name == name) is { } ownEntry)
        {
            Emit(literal, ownEntry.Symbol, SymbolRole.ReadAccess);
            return;
        }

        if (_catalog.Programs.TryGetValue(name, out var program))
        {
            Emit(literal, program.Symbol, SymbolRole.ReadAccess);
            return;
        }

        if (_catalog.Entries.TryGetValue(name, out var entry))
        {
            Emit(literal, entry, SymbolRole.ReadAccess);
            return;
        }

        var symbol = _namer.ForExternalProgram(name);
        if (!_external.ContainsKey(symbol))
        {
            var info = new SymbolInformation { Symbol = symbol, Kind = SymbolInformation.Types.Kind.Module, DisplayName = literal.Value.Trim() };
            info.Documentation.Add("external program (not found in the indexed sources)");
            _external[symbol] = info;
        }

        Emit(literal, symbol, SymbolRole.ReadAccess);
    }

    private void ExecReference(int i, ProgramUnit unit, int depth)
    {
        var t = _t[i];
        var kind = _src.ExecOf[i];
        if (kind == ExecKind.Sql)
        {
            if (t.IsUserWordShape && i > 0 && _t[i - 1].Kind == TokenKind.Colon && !t.SpaceBefore)
            {
                Resolve(i, unit, procedureFirst: false, allowProcedureFallback: false);
            }

            return;
        }

        if (depth == 0)
        {
            return;
        }

        if (t.Kind == TokenKind.Literal)
        {
            if (i >= 2 && _t[i - 1].Kind == TokenKind.LParen && _t[i - 2].Is("PROGRAM"))
            {
                CallTarget(t, unit);
            }

            return;
        }

        if (t.IsUserWordShape && !(i > 0 && _t[i - 1].Is("FUNCTION")))
        {
            Resolve(i, unit, procedureFirst: false, allowProcedureFallback: true);
        }
    }
}
