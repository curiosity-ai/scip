using System.Text;

namespace Scip.Reprolang;

/// <summary>An identifier: <c>local name</c>, <c>project descriptors</c> or a bare name.</summary>
internal sealed class Identifier(string value, bool isLocal, Range position)
{
    /// <summary>Lookup key: the name, or <c>global &lt;project&gt; &lt;descriptors&gt;</c> for cross-project identifiers.</summary>
    public string Value { get; } = value;

    public bool IsLocal { get; } = isLocal;

    public Range Position { get; } = position;

    /// <summary>The SCIP symbol, filled in by name resolution.</summary>
    public string Symbol { get; set; } = "";
}

internal sealed class RelationSet
{
    public Identifier? Implements { get; set; }

    public Identifier? References { get; set; }

    public Identifier? TypeDefines { get; set; }

    public Identifier? DefinedBy { get; set; }

    /// <summary>Non-null relation identifiers in the order implements, references, type_defines, defined_by.</summary>
    public IEnumerable<Identifier> Identifiers()
    {
        if (Implements is not null) yield return Implements;
        if (References is not null) yield return References;
        if (TypeDefines is not null) yield return TypeDefines;
        if (DefinedBy is not null) yield return DefinedBy;
    }
}

internal sealed record DefinitionStatement(string Docstring, Identifier Name, RelationSet Relations);

internal sealed record ReferenceStatement(Identifier Name, bool IsForwardDef);

internal sealed record RelationshipsStatement(Identifier Name, RelationSet Relations);

/// <summary>
/// Hand-written equivalent of the tree-sitter grammar in <c>grammar.js</c>. Like tree-sitter's keyword extraction,
/// a word is a keyword only where the grammar accepts that keyword there; elsewhere it is an ordinary <c>name</c>.
/// Unparseable words are skipped (tree-sitter error recovery; the upstream indexer ignores ERROR nodes).
/// Columns are UTF-8 byte offsets, as the index declares a UTF-8 text document encoding.
/// </summary>
internal sealed class ReproParser
{
    private const string DocstringPrefix = "# docstring:";
    private static readonly string[] DefinitionRelations = ["implements", "type_defines", "references"];
    private static readonly string[] AllRelations = ["implements", "type_defines", "references", "defined_by"];
    private static readonly string[] StatementStarts = ["definition", "reference", "relationships"];

    private readonly string _text;
    private readonly int[] _lineStarts;
    private int _pos;

    private ReproParser(string text)
    {
        _text = text;
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        _lineStarts = [.. starts];
    }

    public List<DefinitionStatement> Definitions { get; } = [];

    public List<ReferenceStatement> References { get; } = [];

    public List<RelationshipsStatement> Relationships { get; } = [];

    public static ReproParser Parse(string text)
    {
        var parser = new ReproParser(text);
        parser.ParseSourceFile();
        return parser;
    }

    private void ParseSourceFile()
    {
        string? docstring = null;
        while (true)
        {
            SkipWhitespace();
            if (_pos >= _text.Length)
            {
                return;
            }

            if (TryDocstring(out var found))
            {
                docstring = found;
                continue;
            }

            if (TryComment())
            {
                docstring = null;
                continue;
            }

            var (_, end) = PeekWord();
            var word = _text[_pos..end];
            _pos = end;
            switch (word)
            {
                case "definition":
                    ParseDefinition(docstring ?? "");
                    break;
                case "reference":
                    ParseReference();
                    break;
                case "relationships":
                    ParseRelationshipsStatement();
                    break;
                default:
                    break; // error recovery: skip the unexpected word
            }

            docstring = null;
        }
    }

    private void ParseDefinition(string docstring)
    {
        if (ParseIdentifier(DefinitionRelations) is { } name)
        {
            Definitions.Add(new DefinitionStatement(docstring, name, ParseRelations(DefinitionRelations)));
        }
    }

    private void ParseRelationshipsStatement()
    {
        if (ParseIdentifier(AllRelations) is { } name)
        {
            Relationships.Add(new RelationshipsStatement(name, ParseRelations(AllRelations)));
        }
    }

    private void ParseReference()
    {
        var forward = false;
        SkipWhitespace();
        if (_pos < _text.Length)
        {
            var (start, end) = PeekWord();
            if (_text.AsSpan(start, end - start).SequenceEqual("forward_definition"))
            {
                forward = true;
                _pos = end;
            }
        }

        if (ParseIdentifier([]) is { } name)
        {
            References.Add(new ReferenceStatement(name, forward));
        }
    }

    private RelationSet ParseRelations(string[] relationKeywords)
    {
        var rels = new RelationSet();
        while (true)
        {
            SkipWhitespace();
            if (_pos >= _text.Length)
            {
                return rels;
            }

            var (_, end) = PeekWord();
            var keyword = _text[_pos..end];
            if (Array.IndexOf(relationKeywords, keyword) < 0)
            {
                return rels;
            }

            _pos = end;
            if (ParseIdentifier(relationKeywords) is not { } identifier)
            {
                return rels;
            }

            switch (keyword)
            {
                case "implements": rels.Implements = identifier; break;
                case "type_defines": rels.TypeDefines = identifier; break;
                case "references": rels.References = identifier; break;
                default: rels.DefinedBy = identifier; break;
            }
        }
    }

    /// <summary>
    /// Parses <c>local name | name name | name</c>. <paramref name="relationKeywords"/> are the keywords that may
    /// follow the identifier in the current statement; they, statement starts and comments end a bare name.
    /// </summary>
    private Identifier? ParseIdentifier(string[] relationKeywords)
    {
        SkipWhitespace();
        if (_pos >= _text.Length)
        {
            return null;
        }

        var (s1, e1) = PeekWord();
        _pos = e1;
        var first = _text[s1..e1];
        if (first == "local")
        {
            SkipWhitespace();
            if (_pos >= _text.Length)
            {
                return null;
            }

            var (s2, e2) = PeekWord();
            _pos = e2;
            return new Identifier(_text[s2..e2], true, RangeOf(s1, e2));
        }

        SkipWhitespace();
        if (_pos < _text.Length && !AtKeywordOrComment(relationKeywords))
        {
            var (s2, e2) = PeekWord();
            _pos = e2;
            return new Identifier($"global {first} {_text[s2..e2]}", false, RangeOf(s1, e2));
        }

        return new Identifier(first, false, RangeOf(s1, e1));
    }

    private bool AtKeywordOrComment(string[] relationKeywords)
    {
        if (IsComment(_pos) || _text.AsSpan(_pos).StartsWith(DocstringPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        var (start, end) = PeekWord();
        var word = _text[start..end];
        return Array.IndexOf(StatementStarts, word) >= 0 || Array.IndexOf(relationKeywords, word) >= 0;
    }

    /// <summary>A lone <c>#</c> word starts a comment; <c>#foo</c> is an ordinary name.</summary>
    private bool IsComment(int pos) =>
        _text[pos] == '#' && (pos + 1 == _text.Length || char.IsWhiteSpace(_text[pos + 1]));

    private bool TryComment()
    {
        if (!IsComment(_pos))
        {
            return false;
        }

        SkipToEndOfLine();
        return true;
    }

    private bool TryDocstring(out string? docstring)
    {
        docstring = null;
        if (!_text.AsSpan(_pos).StartsWith(DocstringPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var start = _pos + DocstringPrefix.Length;
        SkipToEndOfLine();
        docstring = _text[start.._pos].Trim();
        return true;
    }

    private void SkipToEndOfLine()
    {
        var nl = _text.IndexOf('\n', _pos);
        _pos = nl < 0 ? _text.Length : nl;
    }

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
        {
            _pos++;
        }
    }

    private (int Start, int End) PeekWord()
    {
        var end = _pos;
        while (end < _text.Length && !char.IsWhiteSpace(_text[end]))
        {
            end++;
        }

        return (_pos, end);
    }

    private Range RangeOf(int start, int end)
    {
        var (sl, sc) = LineAndColumn(start);
        var (el, ec) = LineAndColumn(end);
        return new Range(new Position(sl, sc), new Position(el, ec));
    }

    private (int Line, int Column) LineAndColumn(int offset)
    {
        var line = Array.BinarySearch(_lineStarts, offset);
        if (line < 0)
        {
            line = ~line - 1;
        }

        return (line, Encoding.UTF8.GetByteCount(_text.AsSpan(_lineStarts[line], offset - _lineStarts[line])));
    }
}
