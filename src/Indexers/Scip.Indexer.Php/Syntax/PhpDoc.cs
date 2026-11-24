using System.Text;
using System.Text.RegularExpressions;

namespace Scip.Indexer.Php.Syntax;

// A tiny subset of phpstan/phpdoc-parser: just enough to read @var / @return types and
// @property[-read|-write] / @method tags with their source positions.

internal abstract record DocType;

internal sealed record DocIdent(string Name) : DocType;

internal sealed record DocNullable(DocType Type) : DocType;

internal sealed record DocUnion(List<DocType> Types) : DocType;

internal sealed record DocIntersection(List<DocType> Types) : DocType;

internal sealed record DocArray(DocType Type) : DocType;

internal sealed record DocThis : DocType;

internal sealed record DocShapeItem(string? Key, DocType Type);

internal sealed record DocShape(List<DocShapeItem> Items) : DocType;

internal sealed record DocConditional(DocType If, DocType Else) : DocType;

/// <summary>Generics, callables, literals, ...: parsed (to skip them) but not interpreted, as upstream.</summary>
internal sealed record DocOther : DocType;

internal sealed record DocPropertyTag(DocType Type, string Name, int NameStart, int NameEnd);

internal sealed record DocMethodTag(DocType? ReturnType, string Name, int NameStart, int NameEnd);

internal sealed class DocTags
{
    public static readonly DocTags Empty = new();

    public DocType? VarType;
    public DocType? ReturnType;
    public List<DocPropertyTag> Properties { get; } = [];
    public List<DocMethodTag> Methods { get; } = [];
}

internal static partial class PhpDocParser
{
    [GeneratedRegex(@"@(?:(?:phpstan|psalm)-)?(var|return|property-read|property-write|property|method)(?![\w-])")]
    private static partial Regex TagRegex();

    public static DocTags Parse(DocComment doc)
    {
        var tags = new DocTags();
        var text = Clean(doc.Text);
        foreach (Match m in TagRegex().Matches(text))
        {
            // A tag must be the first thing on its line (after the comment decoration).
            var lineStart = text.LastIndexOf('\n', m.Index) + 1;
            if (text[lineStart..m.Index].Trim().Length != 0)
            {
                continue;
            }

            var p = new Cursor(text, m.Index + m.Length);
            try
            {
                switch (m.Groups[1].Value)
                {
                    case "var":
                        tags.VarType ??= p.ParseType();
                        break;
                    case "return":
                        tags.ReturnType ??= p.ParseType();
                        break;
                    case "method":
                        tags.Methods.Add(p.ParseMethod(doc.Start));
                        break;
                    default:
                    {
                        var type = p.ParseType();
                        p.SkipSpaces(newlines: false);
                        var (name, s, e) = p.ReadVariable();
                        tags.Properties.Add(new DocPropertyTag(type, name, doc.Start + s, doc.Start + e));
                        break;
                    }
                }
            }
            catch (FormatException)
            {
                // Invalid tag value: ignored, like phpdoc-parser's InvalidTagValueNode.
            }
        }

        return tags;
    }

    /// <summary>Blanks out comment decoration while keeping every offset stable.</summary>
    private static string Clean(string text)
    {
        var sb = new StringBuilder(text);
        var lineStart = true;
        for (var i = 0; i < sb.Length; i++)
        {
            var c = sb[i];
            if (c == '\n')
            {
                lineStart = true;
                continue;
            }

            if (lineStart && (c == ' ' || c == '\t' || c == '\r'))
            {
                continue;
            }

            if (lineStart && c == '/' && i + 2 < sb.Length && sb[i + 1] == '*' && sb[i + 2] == '*')
            {
                sb[i] = sb[i + 1] = sb[i + 2] = ' ';
                i += 2;
                continue;
            }

            if (lineStart && c == '*' && !(i + 1 < sb.Length && sb[i + 1] == '/'))
            {
                sb[i] = ' ';
                continue;
            }

            lineStart = false;
        }

        if (sb.Length >= 2 && sb[^1] == '/' && sb[^2] == '*')
        {
            sb[^1] = sb[^2] = ' ';
        }

        return sb.ToString();
    }

    private sealed class Cursor(string s, int pos)
    {
        private int _p = pos;

        private char Cur => _p < s.Length ? s[_p] : '\0';

        public void SkipSpaces(bool newlines = true)
        {
            while (_p < s.Length && (s[_p] is ' ' or '\t' or '\r' || (newlines && s[_p] == '\n')))
            {
                _p++;
            }
        }

        private static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c is '_' or '\\' or '-' or >= '\x80';

        private string ReadIdent()
        {
            var st = _p;
            while (_p < s.Length && IsIdent(s[_p]))
            {
                _p++;
            }

            return s[st.._p];
        }

        public (string Name, int Start, int End) ReadVariable()
        {
            if (Cur != '$')
            {
                throw new FormatException();
            }

            var st = _p;
            _p++;
            while (_p < s.Length && (char.IsLetterOrDigit(s[_p]) || s[_p] is '_' or >= '\x80'))
            {
                _p++;
            }

            if (_p - st < 2)
            {
                throw new FormatException();
            }

            return (s[st.._p], st, _p);
        }

        public DocMethodTag ParseMethod(int docStart)
        {
            SkipSpaces(newlines: false);
            if (string.Compare(s, _p, "static", 0, 6, StringComparison.Ordinal) == 0 && _p + 6 < s.Length && s[_p + 6] is ' ' or '\t')
            {
                _p += 6;
                SkipSpaces(newlines: false);
            }

            DocType? ret = null;
            // `name(` without a return type, otherwise `type name(`.
            var save = _p;
            var first = ReadIdent();
            if (first == "" || Cur != '(')
            {
                _p = save;
                ret = ParseType();
                SkipSpaces(newlines: false);
            }
            else
            {
                _p = save;
            }

            var st = _p;
            var name = ReadIdent();
            if (name == "" || Cur != '(')
            {
                throw new FormatException();
            }

            return new DocMethodTag(ret, name, docStart + st, docStart + _p);
        }

        public DocType ParseType()
        {
            SkipSpaces();
            var first = ParseIntersection();
            if (PeekAfterSpaces() == '|')
            {
                var u = new DocUnion([first]);
                while (PeekAfterSpaces() == '|')
                {
                    SkipSpaces();
                    _p++;
                    SkipSpaces();
                    u.Types.Add(ParseIntersection());
                }

                return u;
            }

            return first;
        }

        private char PeekAfterSpaces()
        {
            var save = _p;
            SkipSpaces();
            var c = Cur;
            if (c != '|' && c != '&')
            {
                _p = save;
                return '\0';
            }

            _p = save;
            return c;
        }

        private DocType ParseIntersection()
        {
            var first = ParseAtom();
            if (PeekAfterSpaces() == '&' && !(_p + 1 < s.Length && s[_p + 1] == '$'))
            {
                var u = new DocIntersection([first]);
                while (PeekAfterSpaces() == '&')
                {
                    SkipSpaces();
                    _p++;
                    SkipSpaces();
                    u.Types.Add(ParseAtom());
                }

                return u;
            }

            return first;
        }

        private DocType ParseAtom()
        {
            SkipSpaces();
            DocType t;
            var c = Cur;
            if (c == '?')
            {
                _p++;
                return new DocNullable(ParseAtom());
            }

            if (c == '(')
            {
                _p++;
                t = ParseType();
                SkipSpaces();
                if (Cur == 'i' && string.Compare(s, _p, "is", 0, 2, StringComparison.Ordinal) == 0)
                {
                    // (T is X ? A : B)
                    _p += 2;
                    SkipSpaces();
                    if (string.Compare(s, _p, "not", 0, 3, StringComparison.Ordinal) == 0)
                    {
                        _p += 3;
                    }

                    ParseType();
                    SkipSpaces();
                    Expect('?');
                    var a = ParseType();
                    SkipSpaces();
                    Expect(':');
                    var b = ParseType();
                    SkipSpaces();
                    Expect(')');
                    t = new DocConditional(a, b);
                }
                else
                {
                    Expect(')');
                }
            }
            else if (c == '$')
            {
                var (name, _, _) = ReadVariable();
                if (name == "$this")
                {
                    t = new DocThis();
                }
                else
                {
                    SkipSpaces();
                    // $param is T ? A : B
                    if (string.Compare(s, _p, "is", 0, 2, StringComparison.Ordinal) != 0)
                    {
                        throw new FormatException();
                    }

                    _p += 2;
                    SkipSpaces();
                    if (string.Compare(s, _p, "not", 0, 3, StringComparison.Ordinal) == 0)
                    {
                        _p += 3;
                    }

                    ParseType();
                    SkipSpaces();
                    Expect('?');
                    var a = ParseType();
                    SkipSpaces();
                    Expect(':');
                    var b = ParseType();
                    t = new DocConditional(a, b);
                }
            }
            else if (c is '\'' or '"')
            {
                _p++;
                while (_p < s.Length && s[_p] != c)
                {
                    _p += s[_p] == '\\' ? 2 : 1;
                }

                _p++;
                t = new DocOther();
            }
            else if (char.IsDigit(c) || (c == '-' && _p + 1 < s.Length && char.IsDigit(s[_p + 1])))
            {
                _p++;
                while (_p < s.Length && (char.IsLetterOrDigit(s[_p]) || s[_p] is '.' or '_'))
                {
                    _p++;
                }

                t = new DocOther();
            }
            else
            {
                var name = ReadIdent();
                if (name == "")
                {
                    throw new FormatException();
                }

                if (Cur == '<')
                {
                    SkipBalanced('<', '>');
                    t = new DocOther();
                }
                else if (Cur == '{' && name is "array" or "list" or "object" or "non-empty-array" or "non-empty-list")
                {
                    t = ParseShape();
                }
                else if (Cur == '(' && name is "callable" or "Closure" or "\\Closure" or "pure-callable" or "pure-Closure")
                {
                    SkipBalanced('(', ')');
                    var save = _p;
                    SkipSpaces(newlines: false);
                    if (Cur == ':')
                    {
                        _p++;
                        ParseAtom();
                    }
                    else
                    {
                        _p = save;
                    }

                    t = new DocOther();
                }
                else if (Cur == ':' && _p + 1 < s.Length && s[_p + 1] == ':')
                {
                    _p += 2;
                    ReadIdent();
                    if (Cur == '*')
                    {
                        _p++;
                    }

                    t = new DocOther();
                }
                else
                {
                    t = new DocIdent(name);
                }
            }

            while (true)
            {
                var save = _p;
                SkipSpaces(newlines: false);
                if (Cur == '[' && _p + 1 < s.Length && s[_p + 1] == ']')
                {
                    _p += 2;
                    t = new DocArray(t);
                    continue;
                }

                _p = save;
                break;
            }

            return t;
        }

        private void Expect(char c)
        {
            if (Cur != c)
            {
                throw new FormatException();
            }

            _p++;
        }

        private void SkipBalanced(char open, char close)
        {
            var depth = 0;
            while (_p < s.Length)
            {
                if (s[_p] == open)
                {
                    depth++;
                }
                else if (s[_p] == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        _p++;
                        return;
                    }
                }

                _p++;
            }

            throw new FormatException();
        }

        private DocShape ParseShape()
        {
            Expect('{');
            var items = new List<DocShapeItem>();
            while (true)
            {
                SkipSpaces();
                if (Cur == '}')
                {
                    _p++;
                    break;
                }

                if (Cur == '.' && string.Compare(s, _p, "...", 0, 3, StringComparison.Ordinal) == 0)
                {
                    _p += 3;
                    SkipSpaces();
                    continue;
                }

                string? key = null;
                var save = _p;
                var keyText = ReadKey();
                if (keyText != null)
                {
                    var afterKey = _p;
                    if (Cur == '?')
                    {
                        _p++;
                    }

                    if (Cur == ':' && !(_p + 1 < s.Length && s[_p + 1] == ':'))
                    {
                        _p++;
                        key = keyText;
                    }
                    else
                    {
                        _p = afterKey;
                        _p = save;
                    }
                }
                else
                {
                    _p = save;
                }

                var type = ParseType();
                items.Add(new DocShapeItem(key, type));
                SkipSpaces();
                if (Cur == ',')
                {
                    _p++;
                    continue;
                }

                SkipSpaces();
                Expect('}');
                break;
            }

            return new DocShape(items);
        }

        private string? ReadKey()
        {
            if (Cur is '\'' or '"')
            {
                var q = Cur;
                var st = ++_p;
                while (_p < s.Length && s[_p] != q)
                {
                    _p++;
                }

                var k = s[st.._p];
                _p++;
                return k;
            }

            if (char.IsLetterOrDigit(Cur) || Cur == '_')
            {
                var st = _p;
                while (_p < s.Length && (char.IsLetterOrDigit(s[_p]) || s[_p] == '_'))
                {
                    _p++;
                }

                return s[st.._p];
            }

            return null;
        }
    }
}
