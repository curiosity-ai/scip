using System.Text;

namespace Scip.Indexer.Dart;

internal enum TokenKind { Identifier, Punctuation, String, Number }

/// <summary>A lexical token. <see cref="Offset"/> and <see cref="Length"/> are UTF-16 code unit based.</summary>
internal readonly record struct Token(TokenKind Kind, string Text, int Offset, int Length, int DocIndex = -1)
{
    public int End => Offset + Length;

    public bool Is(string text) => Kind is TokenKind.Identifier or TokenKind.Punctuation && Text == text;
}

/// <summary>
/// A small, tolerant Dart lexer. It understands comments (collecting <c>///</c> doc comments), raw/triple-quoted
/// strings and string interpolation (interpolated expressions are lexed into the same token stream so
/// identifiers inside them can be indexed). It does not validate anything.
/// </summary>
internal sealed class DartLexer(string source)
{
    private int _pos;
    private readonly List<Token> _tokens = [];
    private readonly List<string> _docs = [];
    private readonly StringBuilder _pendingDoc = new();

    public static (List<Token> Tokens, IReadOnlyList<string> Docs) Lex(string source)
    {
        var lexer = new DartLexer(source);
        lexer.LexUntil(stopAtCloseBrace: false);
        return (lexer._tokens, lexer._docs);
    }

    private void Add(TokenKind kind, int start, int end)
    {
        var doc = -1;
        if (_pendingDoc.Length > 0)
        {
            _docs.Add(_pendingDoc.ToString().TrimEnd('\n'));
            doc = _docs.Count - 1;
            _pendingDoc.Clear();
        }

        _tokens.Add(new Token(kind, source.Substring(start, end - start), start, end - start, doc));
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '$';

    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';

    private char Peek(int n) => _pos + n < source.Length ? source[_pos + n] : '\0';

    private void LexUntil(bool stopAtCloseBrace)
    {
        var depth = 0;
        while (_pos < source.Length)
        {
            var c = source[_pos];
            if (char.IsWhiteSpace(c))
            {
                _pos++;
            }
            else if (c == '/' && Peek(1) == '/')
            {
                var start = _pos;
                while (_pos < source.Length && source[_pos] != '\n')
                {
                    _pos++;
                }

                var line = source.AsSpan(start, _pos - start);
                if (line.StartsWith("///") && !line.StartsWith("////"))
                {
                    // upstream strips `^\s*///\s*`
                    _pendingDoc.Append(line[3..].TrimEnd('\r').TrimStart()).Append('\n');
                }
            }
            else if (c == '/' && Peek(1) == '*')
            {
                var nesting = 0;
                do
                {
                    if (source[_pos] == '/' && Peek(1) == '*')
                    {
                        nesting++;
                        _pos += 2;
                    }
                    else if (source[_pos] == '*' && Peek(1) == '/')
                    {
                        nesting--;
                        _pos += 2;
                    }
                    else
                    {
                        _pos++;
                    }
                }
                while (nesting > 0 && _pos < source.Length);
            }
            else if (c == '#' && _pos == 0 && Peek(1) == '!')
            {
                while (_pos < source.Length && source[_pos] != '\n')
                {
                    _pos++;
                }
            }
            else if (c is '\'' or '"' || (c == 'r' && Peek(1) is '\'' or '"'))
            {
                LexString();
            }
            else if (IsIdentStart(c))
            {
                var start = _pos;
                while (_pos < source.Length && IsIdentPart(source[_pos]))
                {
                    _pos++;
                }

                Add(TokenKind.Identifier, start, _pos);
            }
            else if (char.IsDigit(c))
            {
                var start = _pos;
                while (_pos < source.Length && (char.IsLetterOrDigit(source[_pos]) || source[_pos] == '_' ||
                    (source[_pos] == '.' && _pos + 1 < source.Length && char.IsDigit(source[_pos + 1]))))
                {
                    _pos++;
                }

                Add(TokenKind.Number, start, _pos);
            }
            else
            {
                if (stopAtCloseBrace)
                {
                    if (c == '{')
                    {
                        depth++;
                    }
                    else if (c == '}')
                    {
                        if (depth == 0)
                        {
                            _pos++;
                            return;
                        }

                        depth--;
                    }
                }

                var len = 1;
                if ((c == '=' && Peek(1) == '>') || (c == '?' && Peek(1) == '.') || (c == '.' && Peek(1) == '.'))
                {
                    len = 2;
                }

                Add(TokenKind.Punctuation, _pos, _pos + len);
                _pos += len;
            }
        }
    }

    private void LexString()
    {
        var start = _pos;
        var raw = false;
        if (source[_pos] == 'r')
        {
            raw = true;
            _pos++;
        }

        var quote = source[_pos];
        var triple = Peek(1) == quote && Peek(2) == quote;
        _pos += triple ? 3 : 1;
        var interpolated = new List<Token>();
        while (_pos < source.Length)
        {
            var c = source[_pos];
            if (!raw && c == '\\')
            {
                _pos += 2;
            }
            else if (c == quote && (!triple || (Peek(1) == quote && Peek(2) == quote)))
            {
                _pos += triple ? 3 : 1;
                break;
            }
            else if (!triple && c == '\n')
            {
                break; // unterminated
            }
            else if (!raw && c == '$' && Peek(1) == '{')
            {
                _pos += 2;
                var before = _tokens.Count;
                LexUntil(stopAtCloseBrace: true);
                interpolated.AddRange(_tokens.Skip(before));
                _tokens.RemoveRange(before, _tokens.Count - before);
            }
            else if (!raw && c == '$' && IsIdentStart(Peek(1)) && Peek(1) != '$')
            {
                var s = _pos + 1;
                var e = s;
                while (e < source.Length && IsIdentPart(source[e]) && source[e] != '$')
                {
                    e++;
                }

                interpolated.Add(new Token(TokenKind.Identifier, source[s..e], s, e - s));
                _pos = e;
            }
            else
            {
                _pos++;
            }
        }

        var end = Math.Min(_pos, source.Length);
        var stringToken = new Token(TokenKind.String, source[start..end], start, end - start);
        _tokens.Add(stringToken);
        _tokens.AddRange(interpolated);
    }
}
