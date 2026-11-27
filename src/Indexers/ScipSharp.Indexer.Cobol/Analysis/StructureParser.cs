using System.Globalization;
using System.Text;
using ScipSharp.Indexer.Cobol.Syntax;

namespace ScipSharp.Indexer.Cobol.Analysis;

/// <summary>Output of <see cref="StructureParser"/>: the program units plus per-token annotations used by the resolver.</summary>
internal sealed class ParsedSource(List<Token> tokens)
{
    public List<Token> Tokens { get; } = tokens;

    public ProgramUnit?[] UnitOf { get; } = new ProgramUnit?[tokens.Count];

    public Region[] RegionOf { get; } = new Region[tokens.Count];

    public ExecKind[] ExecOf { get; } = new ExecKind[tokens.Count];

    /// <summary>Token index → the definition whose name it is.</summary>
    public Definition?[] DefAt { get; } = new Definition?[tokens.Count];

    /// <summary>Token index → a reference already known structurally (FD name, END PROGRAM name).</summary>
    public Definition?[] RefAt { get; } = new Definition?[tokens.Count];

    /// <summary>Token index → the section in effect (procedure division), for unqualified paragraph references.</summary>
    public Procedure?[] SectionOf { get; } = new Procedure?[tokens.Count];

    /// <summary>Token index of a REDEFINES / RENAMES operand → the entry containing it (resolved among its siblings).</summary>
    public Dictionary<int, DataItem> DataClauseRefs { get; } = [];

    public List<ProgramUnit> Units { get; } = [];
}

/// <summary>
/// Pass 1: recognises program units (PROGRAM-ID / CLASS-ID / METHOD-ID / FUNCTION-ID, nesting via END PROGRAM),
/// divisions, comment entries, EXEC blocks, FILE-CONTROL SELECTs, SPECIAL-NAMES, FD/SD, data description entries
/// (level hierarchy with 66/77/78/88), INDEXED BY names, sections, paragraphs and ENTRY points.
/// It is deliberately tolerant: a missing period, an unknown clause or an unterminated program never aborts parsing.
/// </summary>
internal sealed class StructureParser
{
    private static readonly HashSet<string> UnitKeywords = new(StringComparer.Ordinal) { "PROGRAM-ID", "CLASS-ID", "METHOD-ID", "FUNCTION-ID", "INTERFACE-ID" };

    private static readonly HashSet<string> EndKinds = new(StringComparer.Ordinal) { "PROGRAM", "CLASS", "METHOD", "FUNCTION", "INTERFACE", "FACTORY", "OBJECT" };

    private static readonly HashSet<string> DataSections = new(StringComparer.Ordinal)
    {
        "FILE", "WORKING-STORAGE", "LOCAL-STORAGE", "LINKAGE", "SCREEN", "REPORT", "COMMUNICATION", "THREAD-LOCAL-STORAGE", "OBJECT-STORAGE",
    };

    private static readonly HashSet<string> EnvParagraphs = new(StringComparer.Ordinal)
    {
        "SOURCE-COMPUTER", "OBJECT-COMPUTER", "SPECIAL-NAMES", "FILE-CONTROL", "I-O-CONTROL", "REPOSITORY",
    };

    private static readonly HashSet<string> NumberExpecting = new(StringComparer.Ordinal)
    {
        "VALUE", "VALUES", "OCCURS", "TO", "THRU", "THROUGH", "IS", "ARE", "FROM", "BY", "CONTAINS", "SIZE", "LINE", "LINES",
        "COLUMN", "COL", "PLUS", "MINUS", "AT", "TOP", "BOTTOM", "FOOTING", "LIMIT", "LIMITS",
    };

    private readonly List<Token> _t;
    private readonly ParsedSource _r;
    private readonly int _fileId;
    private readonly List<ProgramUnit> _stack = [];
    private readonly Dictionary<string, int> _endCounts = new(StringComparer.Ordinal);
    private readonly ProgramUnit? _pseudo;
    private readonly List<DataItem> _levels = [];
    private readonly Dictionary<ProgramUnit, int> _endTokens = [];
    private Region _region;
    private int _headerStart = -1;
    private bool _sentenceStart = true;
    private string _envParagraph = "";
    private FileDefinition? _fd;
    private DataItem? _lastItem;
    private Procedure? _section;

    private StructureParser(List<Token> tokens, int fileId, bool standalone)
    {
        _t = tokens;
        _r = new ParsedSource(tokens);
        _fileId = fileId;
        if (standalone)
        {
            _pseudo = new ProgramUnit("", null, UnitKind.Program, null, fileId) { IsPseudo = true, StartIndex = 0, EndIndex = tokens.Count - 1 };
            _r.Units.Add(_pseudo);
            var first = tokens.FirstOrDefault();
            _region = first is not null && (IsLevel(first) || first.Is("FD") || first.Is("SD")) ? Region.Data : Region.Procedure;
        }
    }

    /// <summary>Parses a preprocessed token stream. <paramref name="standalone"/> parses a copybook on its own (no program around it).</summary>
    public static ParsedSource Parse(List<Token> tokens, int fileId, bool standalone = false)
    {
        var p = new StructureParser(tokens, fileId, standalone);
        p.Run();
        return p._r;
    }

    private ProgramUnit? Current => _stack.Count > 0 ? _stack[^1] : _pseudo;

    private Token? At(int i) => i >= 0 && i < _t.Count ? _t[i] : null;

    private bool NextIs(int i, string upper) => At(i + 1) is { } n && n.Is(upper);

    private void Mark(int from, int toExclusive, Region region)
    {
        var unit = Current;
        for (var k = Math.Max(0, from); k < Math.Min(toExclusive, _t.Count); k++)
        {
            _r.UnitOf[k] = unit;
            _r.RegionOf[k] = region;
            _r.SectionOf[k] = _section;
        }
    }

    private int PeriodAfter(int i)
    {
        var k = i;
        while (k < _t.Count && _t[k].Kind != TokenKind.Period)
        {
            k++;
        }

        return k;
    }

    private int SkipPeriod(int i) => i < _t.Count && _t[i].Kind == TokenKind.Period ? i + 1 : i;

    private void Run()
    {
        CountEnds();
        var i = 0;
        while (i < _t.Count)
        {
            i = Step(i);
        }

        foreach (var u in _stack.ToList())
        {
            Close(u, _t.Count - 1);
        }

        _stack.Clear();
        FinishProcedures();
    }

    private void CountEnds()
    {
        for (var i = 0; i + 2 < _t.Count; i++)
        {
            if (_t[i].Is("END") && EndKinds.Contains(_t[i + 1].Upper) && _t[i + 2].Kind is TokenKind.Word or TokenKind.Literal)
            {
                var name = _t[i + 2].Value.ToUpperInvariant();
                _endCounts[name] = _endCounts.GetValueOrDefault(name) + 1;
            }
        }
    }

    private int Step(int i)
    {
        var t = _t[i];
        if (t.IsWord)
        {
            if ((t.Is("IDENTIFICATION") || t.Is("ID")) && NextIs(i, "DIVISION"))
            {
                _headerStart = i;
                var end = SkipPeriod(i + 2);
                Mark(i, end, Region.Identification);
                _region = Region.Identification;
                return end;
            }

            if (UnitKeywords.Contains(t.Upper) && At(SkipPeriod(i + 1)) is { Kind: TokenKind.Word or TokenKind.Literal })
            {
                return StartUnit(i);
            }

            if (t.Is("END") && At(i + 1) is { } kind && EndKinds.Contains(kind.Upper))
            {
                return EndUnit(i);
            }

            if (t.Is("ENVIRONMENT") && NextIs(i, "DIVISION"))
            {
                _region = Region.Environment;
                _envParagraph = "";
                var end = SkipPeriod(i + 2);
                Mark(i, end, Region.Environment);
                return end;
            }

            if (t.Is("DATA") && NextIs(i, "DIVISION"))
            {
                _region = Region.Data;
                _levels.Clear();
                _fd = null;
                _sentenceStart = true;
                var end = SkipPeriod(i + 2);
                Mark(i, end, Region.Data);
                return end;
            }

            if (t.Is("PROCEDURE") && NextIs(i, "DIVISION"))
            {
                _region = Region.Procedure;
                _section = null;
                var end = SkipPeriod(PeriodAfter(i));
                Mark(i, end, Region.Procedure);
                _sentenceStart = true;
                return end;
            }

            if ((t.Is("EXEC") || t.Is("EXECUTE")) && At(i + 1) is { IsWord: true } what)
            {
                return Exec(i, what);
            }
        }

        Mark(i, i + 1, _region);
        var next = _region switch
        {
            Region.Identification => Identification(i),
            Region.CommentEntry => Identification(i),
            Region.Environment => Environment(i),
            Region.Data => Data(i),
            Region.Procedure => ProcedureDivision(i),
            _ => -1,
        };
        if (next > i)
        {
            _sentenceStart = true;
            return next;
        }

        _sentenceStart = t.Kind == TokenKind.Period;
        return i + 1;
    }

    private int Exec(int i, Token what)
    {
        var kind = what.Upper switch
        {
            "SQL" or "SQLIMS" => ExecKind.Sql,
            "CICS" or "DLI" => ExecKind.Cics,
            _ => ExecKind.Other,
        };
        var k = i + 1;
        while (k < _t.Count && !_t[k].Is("END-EXEC"))
        {
            k++;
        }

        if (k >= _t.Count)
        {
            k = Math.Min(PeriodAfter(i), _t.Count - 1);
        }

        Mark(i, k + 1, Region.Exec);
        for (var m = i; m <= k && m < _t.Count; m++)
        {
            _r.ExecOf[m] = kind;
        }

        _sentenceStart = false;
        return k + 1;
    }

    // ---- program units ----------------------------------------------------------------------------------------------

    private int StartUnit(int i)
    {
        var kind = _t[i].Upper switch
        {
            "CLASS-ID" or "INTERFACE-ID" => UnitKind.Class,
            "METHOD-ID" => UnitKind.Method,
            "FUNCTION-ID" => UnitKind.Function,
            _ => UnitKind.Program,
        };
        var nameIndex = SkipPeriod(i + 1);
        var nameToken = _t[nameIndex];
        var end = PeriodAfter(nameIndex);
        var isCommon = false;
        for (var k = nameIndex + 1; k < end; k++)
        {
            isCommon |= _t[k].Is("COMMON");
        }

        var start = _headerStart >= 0 && _headerStart < i && i - _headerStart <= 4 ? _headerStart : i;
        _headerStart = -1;
        while (_stack.Count > 0 && _endCounts.GetValueOrDefault(_stack[^1].Name) <= 0)
        {
            Close(_stack[^1], start - 1);
        }

        var parent = _stack.Count > 0 ? _stack[^1] : null;
        var unit = new ProgramUnit(nameToken.Value, nameToken, kind, parent, _fileId) { IsCommon = isCommon, StartIndex = start };
        parent?.Children.Add(unit);
        _stack.Add(unit);
        _r.Units.Add(unit);
        _r.DefAt[nameIndex] = unit;
        _region = Region.Identification;
        _levels.Clear();
        _fd = null;
        _section = null;
        _lastItem = null;
        var after = SkipPeriod(end);
        Mark(start, after, Region.Identification);
        unit.Documentation = Text(i, end - 1);
        return after;
    }

    private int EndUnit(int i)
    {
        var kindToken = _t[i + 1];
        var end = SkipPeriod(PeriodAfter(i));
        if (kindToken.Upper is "FACTORY" or "OBJECT")
        {
            Mark(i, end, _region);
            return end;
        }

        var nameIndex = At(i + 2) is { Kind: TokenKind.Word or TokenKind.Literal } ? i + 2 : -1;
        var name = nameIndex >= 0 ? _t[nameIndex].Value.ToUpperInvariant() : null;
        var target = name is null ? null : _stack.LastOrDefault(u => u.Name == name);
        target ??= _stack.LastOrDefault();
        Mark(i, end, Region.None);
        if (target is null)
        {
            return end;
        }

        while (_stack.Count > 0 && _stack[^1] != target)
        {
            Close(_stack[^1], i - 1);
        }

        if (nameIndex >= 0 && name == target.Name)
        {
            _r.RefAt[nameIndex] = target;
            _endCounts[name] = _endCounts.GetValueOrDefault(name) - 1;
        }

        for (var k = i; k < end; k++)
        {
            _r.UnitOf[k] = target;
        }

        _endTokens[target] = i;
        Close(target, end - 1);
        _region = Region.None;
        return end;
    }

    private void Close(ProgramUnit unit, int endIndex)
    {
        unit.EndIndex = Math.Max(unit.StartIndex, endIndex);
        _stack.Remove(unit);
    }

    // ---- identification division ------------------------------------------------------------------------------------

    private int Identification(int i)
    {
        var t = _t[i];
        if (!t.IsWord || !Keywords.CommentParagraphs.Contains(t.Upper))
        {
            return -1;
        }

        var k = SkipPeriod(i + 1);
        while (k < _t.Count && !(_t[k].FirstOnLine && StartsIdentificationItem(k)))
        {
            k++;
        }

        Mark(i, i + 1, Region.Identification);
        Mark(i + 1, k, Region.CommentEntry);
        return k;
    }

    private bool StartsIdentificationItem(int k)
    {
        var t = _t[k];
        if (!t.IsWord)
        {
            return false;
        }

        if (UnitKeywords.Contains(t.Upper) || Keywords.CommentParagraphs.Contains(t.Upper))
        {
            return true;
        }

        return (t.Upper is "ENVIRONMENT" or "DATA" or "PROCEDURE" or "IDENTIFICATION" or "ID" && NextIs(k, "DIVISION"))
            || (t.Is("END") && At(k + 1) is { } n && EndKinds.Contains(n.Upper));
    }

    // ---- environment division ---------------------------------------------------------------------------------------

    private int Environment(int i)
    {
        var t = _t[i];
        var unit = Current;
        if (!t.IsWord || unit is null)
        {
            return -1;
        }

        if (t.Upper is "CONFIGURATION" or "INPUT-OUTPUT" && NextIs(i, "SECTION"))
        {
            var end = SkipPeriod(i + 2);
            Mark(i, end, Region.Environment);
            return end;
        }

        if (EnvParagraphs.Contains(t.Upper) && At(i + 1)?.Kind == TokenKind.Period)
        {
            _envParagraph = t.Upper;
            Mark(i, i + 2, Region.Environment);
            return i + 2;
        }

        switch (_envParagraph)
        {
            case "FILE-CONTROL" when t.Is("SELECT"):
            {
                var j = i + 1;
                if (At(j) is { } opt && opt.Is("OPTIONAL"))
                {
                    j++;
                }

                if (At(j) is not { IsUserWordShape: true } nameToken)
                {
                    return -1;
                }

                var end = PeriodAfter(j);
                if (!unit.Files.TryGetValue(nameToken.Upper, out var file))
                {
                    file = new FileDefinition(nameToken.Text, nameToken) { StartIndex = i, EndIndex = Math.Min(end, _t.Count - 1) };
                    file.Documentation = Text(i, end - 1);
                    unit.Files[file.Name] = file;
                    _r.DefAt[j] = file;
                }
                else
                {
                    _r.RefAt[j] = file;
                }

                for (var k = j; k < end; k++)
                {
                    file.IsGlobal |= _t[k].Is("GLOBAL");
                }

                Mark(i, j + 1, Region.Environment);
                return j + 1;
            }

            case "SPECIAL-NAMES":
            {
                var isDefinition = t.Is("CLASS")
                    || (t.Is("IS") && !(At(i - 1) is { } prev && (prev.Is("CURSOR") || prev.Is("CURRENCY") || prev.Is("SIGN")
                        || (prev.Is("STATUS") && At(i - 2) is { } crt && crt.Is("CRT")))));
                if (isDefinition && At(i + 1) is { IsUserWordShape: true } special && !Keywords.IsReserved(special.Upper))
                {
                    var def = new SimpleDefinition(special.Text, special, SimpleKind.Special, null) { StartIndex = i - 1, EndIndex = i + 1 };
                    def.Documentation = Text(Math.Max(0, i - 1), i + 1);
                    unit.Specials.TryAdd(def.Name, def);
                    _r.DefAt[i + 1] = def;
                    Mark(i, i + 2, Region.Environment);
                    return i + 2;
                }

                return -1;
            }

            default:
                return -1;
        }
    }

    // ---- data division ----------------------------------------------------------------------------------------------

    private static bool IsLevel(Token t) =>
        t.IsInteger && t.Text.Length <= 2 && int.Parse(t.Text, CultureInfo.InvariantCulture) is (>= 1 and <= 49) or 66 or 77 or 78 or 88;

    private int Data(int i)
    {
        var t = _t[i];
        var unit = Current;
        if (unit is null)
        {
            return -1;
        }

        if (t.IsWord && DataSections.Contains(t.Upper) && NextIs(i, "SECTION"))
        {
            _levels.Clear();
            _fd = null;
            _lastItem = null;
            var end = SkipPeriod(i + 2);
            Mark(i, end, Region.Data);
            return end;
        }

        if (!_sentenceStart)
        {
            return -1;
        }

        if ((t.Is("FD") || t.Is("SD")) && At(i + 1) is { IsUserWordShape: true } fileName)
        {
            var end = PeriodAfter(i);
            if (!unit.Files.TryGetValue(fileName.Upper, out var file))
            {
                file = new FileDefinition(fileName.Text, fileName) { StartIndex = i, EndIndex = Math.Min(end, _t.Count - 1) };
                file.Documentation = Text(i, end - 1);
                unit.Files[file.Name] = file;
                _r.DefAt[i + 1] = file;
            }
            else
            {
                _r.RefAt[i + 1] = file;
            }

            for (var k = i; k < end; k++)
            {
                file.IsGlobal |= _t[k].Is("GLOBAL");
            }

            _fd = file;
            _levels.Clear();
            _lastItem = null;
            Mark(i, i + 2, Region.Data);
            return i + 2; // the remaining FD clauses are ordinary tokens (DATA RECORD IS x, LINAGE IS y, ...)
        }

        if ((t.Is("RD") || t.Is("CD")) && At(i + 1) is { IsUserWordShape: true })
        {
            _fd = null;
            _levels.Clear();
            _lastItem = null;
            return -1;
        }

        return IsLevel(t) ? Entry(i, unit) : -1;
    }

    private int Entry(int i, ProgramUnit unit)
    {
        var level = int.Parse(_t[i].Text, CultureInfo.InvariantCulture);
        var j = i + 1;
        Token? nameToken = null;
        var nameIndex = -1;
        if (At(j) is { IsWord: true } candidate)
        {
            if (candidate.Is("FILLER"))
            {
                j++;
            }
            else if (candidate.IsUserWordShape && !Keywords.DataClauses.Contains(candidate.Upper))
            {
                nameToken = candidate;
                nameIndex = j;
                j++;
            }
        }

        var k = j;
        var redefines = -1;
        var renames = new List<int>();
        var indexNames = new List<int>();
        var isGlobal = false;
        string? picture = null;
        while (k < _t.Count)
        {
            var tk = _t[k];
            if (tk.Kind == TokenKind.Period)
            {
                break;
            }

            if (k > j && tk.FirstOnLine && IsLevel(tk) && !ExpectsNumber(_t[k - 1], level))
            {
                break; // missing period before the next entry
            }

            if (tk.IsWord && At(k + 1) is { } after && (after.Is("SECTION") || after.Is("DIVISION")))
            {
                break;
            }

            if (tk.Is("REDEFINES") && At(k + 1) is { IsUserWordShape: true })
            {
                redefines = k + 1;
                k += 2;
                continue;
            }

            if (tk.Is("RENAMES"))
            {
                for (var m = k + 1; m < _t.Count && m <= k + 3; m++)
                {
                    if (_t[m].IsUserWordShape && !_t[m].Is("THRU") && !_t[m].Is("THROUGH"))
                    {
                        renames.Add(m);
                    }
                }
            }

            if (tk.Kind == TokenKind.Picture)
            {
                picture = tk.Text;
            }

            isGlobal |= tk.Is("GLOBAL");
            if (tk.Is("INDEXED"))
            {
                var m = k + 1;
                if (At(m) is { } by && by.Is("BY"))
                {
                    m++;
                }

                while (m < _t.Count && (_t[m].Kind == TokenKind.Comma || (_t[m].IsUserWordShape && !Keywords.IsReserved(_t[m].Upper))))
                {
                    if (_t[m].IsWord)
                    {
                        indexNames.Add(m);
                    }

                    m++;
                }

                k = m;
                continue;
            }

            k++;
        }

        DataItem? parent;
        switch (level)
        {
            case 88:
                parent = _lastItem;
                break;
            case 66:
                parent = null;
                break;
            case 1 or 77 or 78:
                _levels.Clear();
                parent = null;
                break;
            default:
                while (_levels.Count > 0 && _levels[^1].Level >= level)
                {
                    _levels.RemoveAt(_levels.Count - 1);
                }

                parent = _levels.Count > 0 ? _levels[^1] : null;
                break;
        }

        var endIndex = k < _t.Count && _t[k].Kind == TokenKind.Period ? k : k - 1;
        var item = new DataItem(nameToken?.Text ?? "FILLER", nameToken, level, parent)
        {
            StartIndex = i,
            EndIndex = endIndex,
            Picture = picture,
            IsGlobal = isGlobal || (parent?.IsGlobal ?? false),
            File = parent is null ? (level == 1 ? _fd : null) : parent.File,
        };
        item.Documentation = Text(i, k - 1);
        parent?.Children.Add(item);
        unit.AddItem(item);
        if (level != 88 && level != 66)
        {
            if (level is not (77 or 78))
            {
                _levels.Add(item);
            }

            _lastItem = item;
        }

        if (nameIndex >= 0)
        {
            _r.DefAt[nameIndex] = item;
        }

        if (redefines >= 0)
        {
            _r.DataClauseRefs[redefines] = item;
        }

        foreach (var m in renames)
        {
            _r.DataClauseRefs[m] = item;
        }

        foreach (var m in indexNames)
        {
            var idx = new SimpleDefinition(_t[m].Text, _t[m], SimpleKind.Index, item) { StartIndex = m, EndIndex = m };
            idx.Documentation = $"INDEXED BY {_t[m].Text} (index of {item.DisplayName})";
            unit.Indexes.TryAdd(idx.Name, idx);
            _r.DefAt[m] = idx;
        }

        var next = k < _t.Count && _t[k].Kind == TokenKind.Period ? k + 1 : k;
        Mark(i, next, Region.Data);
        return next;
    }

    private static bool ExpectsNumber(Token previous, int level) =>
        previous.Kind is TokenKind.Comma or TokenKind.Operator
        || (previous.IsWord && NumberExpecting.Contains(previous.Upper))
        || (level == 88 && (previous.Kind == TokenKind.Literal || previous.IsInteger));

    // ---- procedure division -----------------------------------------------------------------------------------------

    private int ProcedureDivision(int i)
    {
        var t = _t[i];
        var unit = Current;
        if (unit is null || !t.IsWord)
        {
            return -1;
        }

        if (t.Is("ENTRY") && At(i + 1) is { Kind: TokenKind.Literal } literal && !(At(i - 1) is { } prev && prev.Is("TO")))
        {
            var entry = new SimpleDefinition(literal.Value, literal, SimpleKind.Entry, null) { StartIndex = i, EndIndex = PeriodAfter(i) };
            entry.Documentation = Text(i, Math.Min(PeriodAfter(i), _t.Count) - 1);
            unit.Entries.Add(entry);
            _r.DefAt[i + 1] = entry;
            Mark(i, i + 2, Region.Procedure);
            return i + 2;
        }

        if (!_sentenceStart || t.Is("DECLARATIVES"))
        {
            return -1;
        }

        if (NextIs(i, "SECTION") && t.IsUserWordShape && !Keywords.IsReserved(t.Upper) || (NextIs(i, "SECTION") && t.IsInteger))
        {
            var section = new Procedure(t.Text, t, isSection: true, section: null) { StartIndex = i };
            var j = i + 2;
            if (At(j) is { IsInteger: true })
            {
                j++;
            }

            section.Documentation = $"{t.Text} SECTION";
            _section = section;
            unit.AddProcedure(section);
            _r.DefAt[i] = section;
            var end = SkipPeriod(j);
            Mark(i, end, Region.Procedure);
            return end;
        }

        if (At(i + 1)?.Kind == TokenKind.Period && ((t.IsUserWordShape && !Keywords.IsReserved(t.Upper)) || (t.IsInteger && t.AreaA)))
        {
            var paragraph = new Procedure(t.Text, t, isSection: false, section: _section) { StartIndex = i };
            paragraph.Documentation = _section is null ? $"{t.Text}." : $"{t.Text}. (in section {_section.DisplayName})";
            unit.AddProcedure(paragraph);
            _r.DefAt[i] = paragraph;
            Mark(i, i + 2, Region.Procedure);
            return i + 2;
        }

        return -1;
    }

    private void FinishProcedures()
    {
        foreach (var unit in _r.Units)
        {
            var limit = unit.EndIndex >= 0 ? unit.EndIndex : _t.Count - 1;
            foreach (var child in unit.Children)
            {
                limit = Math.Min(limit, child.StartIndex - 1);
            }

            // The last token of the unit's own text, excluding END PROGRAM.
            while (limit > 0 && _r.UnitOf[limit] != unit)
            {
                limit--;
            }

            if (_endTokens.TryGetValue(unit, out var endToken))
            {
                limit = Math.Min(limit, endToken - 1);
            }

            var procedures = unit.Procedures.OrderBy(p => p.StartIndex).ToList();
            for (var n = 0; n < procedures.Count; n++)
            {
                var p = procedures[n];
                var next = procedures.Skip(n + 1).FirstOrDefault(q => !p.IsSection || q.IsSection);
                p.EndIndex = Math.Max(p.StartIndex, next is not null ? next.StartIndex - 1 : limit);
            }
        }
    }

    /// <summary>Renders tokens [from, to] as source-like text (single spaces, documentation only).</summary>
    private string Text(int from, int to)
    {
        var b = new StringBuilder();
        for (var k = Math.Max(0, from); k <= to && k < _t.Count; k++)
        {
            var tok = _t[k];
            if (b.Length > 0 && tok.SpaceBefore)
            {
                b.Append(' ');
            }

            b.Append(tok.Text);
            if (b.Length > 400)
            {
                b.Append(" ...");
                break;
            }
        }

        return b.ToString();
    }
}
