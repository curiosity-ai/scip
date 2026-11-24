using System.Text;

namespace Scip.Indexer.TypeScript.Syntax;

public enum T { Eof, Ident, Private, Number, String, TemplateNoSub, TemplateHead, TemplateMiddle, TemplateTail, Regex, Punct }

public sealed class SyntaxException(string message, int position) : Exception(message)
{
    public int Position { get; } = position;
}

/// <summary>On-demand lexer. The parser drives re-scans for regexes and template continuations.</summary>
public sealed class Scanner(string text)
{
    public readonly string Text = text;

    public int Pos;          // scan position (end of current token)
    public T Kind;
    public int TokenStart;
    public int TokenEnd;
    public string TokenText = "";   // identifier name / punctuator / decoded string value
    public bool NewlineBefore;
    public string? JsDoc;           // last /** */ comment in the trivia before this token
    public int PrevEnd;             // end of the previous token

    public readonly record struct State(int Pos, T Kind, int TokenStart, int TokenEnd, string TokenText, bool NewlineBefore, string? JsDoc, int PrevEnd);

    public State Save() => new(Pos, Kind, TokenStart, TokenEnd, TokenText, NewlineBefore, JsDoc, PrevEnd);

    public void Restore(State s)
    {
        (Pos, Kind, TokenStart, TokenEnd, TokenText, NewlineBefore, JsDoc, PrevEnd) = s;
    }

    private static bool IsIdStart(char c) => char.IsLetter(c) || c == '$' || c == '_' || (c > 127 && char.IsLetterOrDigit(c));

    private static bool IsIdPart(char c) => char.IsLetterOrDigit(c) || c == '$' || c == '_' || c == '‌' || c == '‍';

    public void Next()
    {
        PrevEnd = TokenEnd;
        NewlineBefore = false;
        JsDoc = null;
        SkipTrivia();
        TokenStart = Pos;
        if (Pos >= Text.Length)
        {
            Kind = T.Eof;
            TokenText = "";
            TokenEnd = Pos;
            return;
        }

        var c = Text[Pos];
        if (IsIdStart(c) || c == '\\')
        {
            var s = Pos;
            Pos++;
            while (Pos < Text.Length && (IsIdPart(Text[Pos]) || Text[Pos] == '\\')) { Pos++; }
            Kind = T.Ident;
            TokenText = Text[s..Pos];
        }
        else if (char.IsDigit(c) || (c == '.' && Pos + 1 < Text.Length && char.IsDigit(Text[Pos + 1])))
        {
            ScanNumber();
        }
        else if (c is '"' or '\'')
        {
            ScanString(c);
        }
        else if (c == '`')
        {
            Pos++;
            ScanTemplate(head: true);
        }
        else if (c == '#' && Pos + 1 < Text.Length && IsIdStart(Text[Pos + 1]))
        {
            var s = Pos;
            Pos++;
            while (Pos < Text.Length && IsIdPart(Text[Pos])) { Pos++; }
            Kind = T.Private;
            TokenText = Text[s..Pos];
        }
        else
        {
            ScanPunct();
        }

        TokenEnd = Pos;
    }

    private void SkipTrivia()
    {
        while (Pos < Text.Length)
        {
            var c = Text[Pos];
            if (c is '\n' or '\r' or '\u2028' or '\u2029') { NewlineBefore = true; Pos++; }
            else if (char.IsWhiteSpace(c) || c == '\uFEFF') { Pos++; }
            else if (c == '/' && Pos + 1 < Text.Length && Text[Pos + 1] == '/')
            {
                while (Pos < Text.Length && Text[Pos] is not ('\n' or '\r' or '\u2028' or '\u2029')) { Pos++; }
            }
            else if (c == '/' && Pos + 1 < Text.Length && Text[Pos + 1] == '*')
            {
                var s = Pos;
                var end = Text.IndexOf("*/", Pos + 2, StringComparison.Ordinal);
                Pos = end < 0 ? Text.Length : end + 2;
                if (Text[s..Pos].Contains('\n')) { NewlineBefore = true; }
                if (Pos - s > 4 && Text[s + 2] == '*') { JsDoc = Text[s..Pos]; }
            }
            else if (c == '#' && Pos == 0 && Pos + 1 < Text.Length && Text[1] == '!')
            {
                while (Pos < Text.Length && Text[Pos] != '\n') { Pos++; }
            }
            else
            {
                break;
            }
        }
    }

    private void ScanNumber()
    {
        var s = Pos;
        if (Text[Pos] == '0' && Pos + 1 < Text.Length && Text[Pos + 1] is 'x' or 'X' or 'b' or 'B' or 'o' or 'O')
        {
            Pos += 2;
            while (Pos < Text.Length && (char.IsAsciiHexDigit(Text[Pos]) || Text[Pos] == '_')) { Pos++; }
        }
        else
        {
            while (Pos < Text.Length && (char.IsDigit(Text[Pos]) || Text[Pos] == '_')) { Pos++; }
            if (Pos < Text.Length && Text[Pos] == '.')
            {
                Pos++;
                while (Pos < Text.Length && (char.IsDigit(Text[Pos]) || Text[Pos] == '_')) { Pos++; }
            }

            if (Pos < Text.Length && Text[Pos] is 'e' or 'E')
            {
                var save = Pos;
                Pos++;
                if (Pos < Text.Length && Text[Pos] is '+' or '-') { Pos++; }
                if (Pos < Text.Length && char.IsDigit(Text[Pos]))
                {
                    while (Pos < Text.Length && char.IsDigit(Text[Pos])) { Pos++; }
                }
                else
                {
                    Pos = save;
                }
            }
        }

        if (Pos < Text.Length && Text[Pos] == 'n') { Pos++; }
        Kind = T.Number;
        TokenText = Text[s..Pos];
    }

    private void ScanString(char quote)
    {
        Pos++;
        var sb = new StringBuilder();
        while (Pos < Text.Length && Text[Pos] != quote)
        {
            var c = Text[Pos];
            if (c == '\\' && Pos + 1 < Text.Length)
            {
                Pos++;
                AppendEscape(sb);
            }
            else if (c is '\n' or '\r')
            {
                break; // unterminated
            }
            else
            {
                sb.Append(c);
                Pos++;
            }
        }

        if (Pos < Text.Length && Text[Pos] == quote) { Pos++; }
        Kind = T.String;
        TokenText = sb.ToString();
    }

    private void AppendEscape(StringBuilder sb)
    {
        var e = Text[Pos++];
        switch (e)
        {
            case 'n': sb.Append('\n'); break;
            case 't': sb.Append('\t'); break;
            case 'r': sb.Append('\r'); break;
            case 'b': sb.Append('\b'); break;
            case 'f': sb.Append('\f'); break;
            case 'v': sb.Append('\v'); break;
            case '0': sb.Append('\0'); break;
            case '\r': if (Pos < Text.Length && Text[Pos] == '\n') { Pos++; } break;
            case '\n': break;
            case 'x' when Pos + 2 <= Text.Length && int.TryParse(Text.AsSpan(Pos, 2), System.Globalization.NumberStyles.HexNumber, null, out var xv):
                sb.Append((char)xv); Pos += 2; break;
            case 'u':
                if (Pos < Text.Length && Text[Pos] == '{')
                {
                    var close = Text.IndexOf('}', Pos);
                    if (close > 0 && int.TryParse(Text.AsSpan(Pos + 1, close - Pos - 1), System.Globalization.NumberStyles.HexNumber, null, out var cp) && cp <= 0x10FFFF)
                    {
                        sb.Append(char.ConvertFromUtf32(cp));
                        Pos = close + 1;
                        break;
                    }
                }
                else if (Pos + 4 <= Text.Length && int.TryParse(Text.AsSpan(Pos, 4), System.Globalization.NumberStyles.HexNumber, null, out var uv))
                {
                    sb.Append((char)uv);
                    Pos += 4;
                    break;
                }

                sb.Append('u');
                break;
            default: sb.Append(e); break;
        }
    }

    /// <summary>Scans template characters after a backtick or closing brace.</summary>
    private void ScanTemplate(bool head)
    {
        var sb = new StringBuilder();
        while (true)
        {
            if (Pos >= Text.Length) { Kind = head ? T.TemplateNoSub : T.TemplateTail; break; }
            var c = Text[Pos];
            if (c == '`') { Pos++; Kind = head ? T.TemplateNoSub : T.TemplateTail; break; }
            if (c == '$' && Pos + 1 < Text.Length && Text[Pos + 1] == '{') { Pos += 2; Kind = head ? T.TemplateHead : T.TemplateMiddle; break; }
            if (c == '\\' && Pos + 1 < Text.Length) { Pos++; AppendEscape(sb); continue; }
            sb.Append(c);
            Pos++;
        }

        TokenText = sb.ToString();
    }

    /// <summary>Called by the parser when the current token is <c>}</c> that closes a template substitution.</summary>
    public void RescanTemplateContinuation()
    {
        Pos = TokenStart + 1;
        ScanTemplate(head: false);
        TokenEnd = Pos;
    }

    /// <summary>Called when the current token is <c>/</c> or <c>/=</c> in expression position.</summary>
    public void RescanRegex()
    {
        var p = TokenStart + 1;
        var inClass = false;
        while (p < Text.Length)
        {
            var c = Text[p];
            if (c is '\n' or '\r') { break; }
            if (c == '\\') { p += 2; continue; }
            if (c == '[') { inClass = true; }
            else if (c == ']') { inClass = false; }
            else if (c == '/' && !inClass) { p++; break; }
            p++;
        }

        while (p < Text.Length && IsIdPart(Text[p])) { p++; }
        Pos = p;
        TokenEnd = p;
        Kind = T.Regex;
        TokenText = Text[TokenStart..p];
    }

    private static readonly string[] Puncts =
    [
        "...", "=>", "===", "!==", "==", "!=", "<<=", "<=", "<<", "&&=", "||=", "??=", "&&", "||", "??", "?.", "**=", "**", "*=", "+=", "-=",
        "/=", "%=", "&=", "|=", "^=", "++", "--",
    ];

    private void ScanPunct()
    {
        var rest = Text.AsSpan(Pos);
        foreach (var p in Puncts)
        {
            if (rest.StartsWith(p, StringComparison.Ordinal))
            {
                if (p == "?." && rest.Length > 2 && char.IsDigit(rest[2])) { continue; }
                Pos += p.Length;
                Kind = T.Punct;
                TokenText = p;
                return;
            }
        }

        Kind = T.Punct;
        TokenText = Text[Pos].ToString();
        Pos++;
    }

    /// <summary>Splits a multi-character punctuator (<c>&lt;&lt;</c>) so that only its first character is the current token.</summary>
    public void SplitFirstChar()
    {
        Pos = TokenStart + 1;
        TokenEnd = Pos;
        TokenText = Text[TokenStart].ToString();
    }

    /// <summary>JSX attribute strings have no escapes and may span lines: rescans the current string token that way.</summary>
    public void RescanJsxString()
    {
        var quote = Text[TokenStart];
        if (quote is not ('"' or '\'')) { return; }
        var end = Text.IndexOf(quote, TokenStart + 1);
        if (end < 0) { return; }
        Pos = end + 1;
        TokenEnd = Pos;
        TokenText = Text.Substring(TokenStart + 1, end - TokenStart - 1);
    }

    /// <summary>Error recovery: skips the template literal whose head is the current token without parsing its substitutions.</summary>
    public void SkipRawTemplate()
    {
        Pos = EndOfTemplate(TokenStart + 1);
        TokenEnd = Pos;
        Kind = T.String;
        Next();
    }

    private int EndOfTemplate(int i)
    {
        while (i < Text.Length)
        {
            var c = Text[i];
            if (c == '\\') { i += 2; continue; }
            if (c == '`') { return i + 1; }
            if (c == '$' && i + 1 < Text.Length && Text[i + 1] == '{') { i = EndOfBraces(i + 2); continue; }
            i++;
        }

        return Text.Length;
    }

    private int EndOfBraces(int i)
    {
        var depth = 1;
        while (i < Text.Length)
        {
            var c = Text[i];
            switch (c)
            {
                case '`': i = EndOfTemplate(i + 1); continue;
                case '"' or '\'':
                    i++;
                    while (i < Text.Length && Text[i] != c && Text[i] != '\n') { i += Text[i] == '\\' ? 2 : 1; }
                    i++;
                    continue;
                case '{': depth++; break;
                case '}': if (--depth == 0) { return i + 1; } break;
            }

            i++;
        }

        return Text.Length;
    }

    /// <summary>Raw lookahead: characters following the current token, used for merging <c>&gt;</c> operators.</summary>
    public string RawAfterToken(int count) => TokenEnd + count <= Text.Length ? Text.Substring(TokenEnd, count) : Text[TokenEnd..];

    /// <summary>Consumes extra raw characters after a <c>&gt;</c> token (to form <c>&gt;&gt;</c>, <c>&gt;=</c> ...).</summary>
    public void ExtendToken(int extra)
    {
        Pos += extra;
        TokenEnd = Pos;
        TokenText = Text[TokenStart..Pos];
    }
}
