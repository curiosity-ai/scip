// Ported from abaplint packages/core/src/abap/1_lexer/lexer.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Lexer;

public sealed class Lexer
{
    private const int ModeNormal = 1;
    private const int ModePing = 2;
    private const int ModeStr = 3;
    private const int ModeTemplate = 4;
    private const int ModeComment = 5;
    private const int ModePragma = 6;

    // character codes, used for integer comparisons in the hot loop
    private const int EOF = -1;             // no character (start/end of file)
    private const int CH_TAB = 9;           // "\t"
    private const int CH_NL = 10;           // "\n"
    private const int CH_SPACE = 32;        // " "
    private const int CH_DQUOTE = 34;       // "\""
    private const int CH_HASH = 35;         // "#"
    private const int CH_QUOTE = 39;        // "'"
    private const int CH_STAR = 42;         // "*"
    private const int CH_DASH = 45;         // "-"
    private const int CH_COMMA = 44;        // ","
    private const int CH_DOT = 46;          // "."
    private const int CH_COLON = 58;        // ":"
    private const int CH_EQUALS = 61;       // "="
    private const int CH_GT = 62;           // ">"
    private const int CH_AT = 64;           // "@"
    private const int CH_BACKSLASH = 92;    // "\\"
    private const int CH_BACKTICK = 96;     // "`"
    private const int CH_LBRACE = 123;      // "{"
    private const int CH_PIPE = 124;        // "|"
    private const int CH_RBRACE = 125;      // "}"

    // characters that terminate the current token
    private static readonly HashSet<int> SPLITS =
    [
        CH_SPACE, CH_COLON, CH_DOT, CH_COMMA, CH_DASH, 43 /* + */, 40 /* ( */,
        41 /* ) */, 91 /* [ */, 93 /* ] */, CH_BACKSLASH, CH_TAB, CH_NL,
    ];

    // single characters that are emitted as their own token
    private static readonly HashSet<int> BUFS =
    [
        CH_DOT, CH_COMMA, CH_COLON, 40 /* ( */, 41 /* ) */, 91 /* [ */,
        93 /* ] */, 43 /* + */, CH_AT,
    ];

    // characters treated as separators after ping literals and string templates.
    private static readonly HashSet<int> AFTER_LITERAL =
    [
        EOF, CH_SPACE, CH_TAB, CH_NL, CH_DOT, CH_COMMA, CH_COLON, CH_DQUOTE,
        40 /* ( */, 41 /* ) */, 91 /* [ */, 93 /* ] */,
    ];

    private Position? @virtual;
    private List<AbstractToken> tokens = [];
    private int m;
    private LexerStream stream = null!;
    private LexerBuffer buffer = null!;

    public IABAPLexerResult Run(IFile file, Position? @virtual = null)
    {
        this.@virtual = @virtual;
        tokens = [];
        m = ModeNormal;

        Process(file.GetRaw());
        return new IABAPLexerResult(file, tokens);
    }

    private void Add()
    {
        var s = JsString.Trim(buffer.Get());
        if (s.Length > 0)
        {
            var col = stream.GetCol();
            var row = stream.GetRow();

            var whiteBefore = false;
            var beforeOffset = stream.GetOffset() - s.Length;
            if (beforeOffset >= 0)
            {
                var prev = stream.CharCodeAt(beforeOffset);
                if (prev == CH_SPACE || prev == CH_NL || prev == CH_TAB || prev == CH_COLON)
                {
                    whiteBefore = true;
                }
            }

            var whiteAfter = false;
            var next = stream.NextChar();
            if (next == CH_SPACE || next == CH_NL || next == CH_TAB || next == CH_COLON
                || next == CH_COMMA || next == CH_DOT || next == EOF || next == CH_DQUOTE)
            {
                whiteAfter = true;
            }

            var pos = new Position(row, col - s.Length);
            if (@virtual != null)
            {
                pos = new VirtualPosition(@virtual, pos.GetRow(), pos.GetCol());
            }

            AbstractToken? tok = null;
            if (m == ModeComment)
            {
                tok = new Comment(pos, s);
            }
            else if (m == ModePing || m == ModeStr)
            {
                tok = new StringToken(pos, s);
            }
            else if (m == ModeTemplate)
            {
                var first = s[0];
                var last = s[s.Length - 1];
                if (first == '|' && last == '|')
                {
                    tok = new StringTemplate(pos, s);
                }
                else if (first == '|' && last == '{' && whiteAfter)
                {
                    tok = new StringTemplateBegin(pos, s);
                }
                else if (first == '}' && last == '|' && whiteBefore)
                {
                    tok = new StringTemplateEnd(pos, s);
                }
                else if (first == '}' && last == '{' && whiteAfter && whiteBefore)
                {
                    tok = new StringTemplateMiddle(pos, s);
                }
                else
                {
                    tok = new Identifier(pos, s);
                }
            }
            else if (s.Length > 2 && s.StartsWith("##", StringComparison.Ordinal))
            {
                tok = new Pragma(pos, s);
            }
            else if (s.Length == 1)
            {
                if (s == "." || s == ",")
                {
                    tok = new Punctuation(pos, s);
                }
                else if (s == "[")
                {
                    if (whiteBefore && whiteAfter) tok = new WBracketLeftW(pos, s);
                    else if (whiteBefore) tok = new WBracketLeft(pos, s);
                    else if (whiteAfter) tok = new BracketLeftW(pos, s);
                    else tok = new BracketLeft(pos, s);
                }
                else if (s == "(")
                {
                    if (whiteBefore && whiteAfter) tok = new WParenLeftW(pos, s);
                    else if (whiteBefore) tok = new WParenLeft(pos, s);
                    else if (whiteAfter) tok = new ParenLeftW(pos, s);
                    else tok = new ParenLeft(pos, s);
                }
                else if (s == "]")
                {
                    if (whiteBefore && whiteAfter) tok = new WBracketRightW(pos, s);
                    else if (whiteBefore) tok = new WBracketRight(pos, s);
                    else if (whiteAfter) tok = new BracketRightW(pos, s);
                    else tok = new BracketRight(pos, s);
                }
                else if (s == ")")
                {
                    if (whiteBefore && whiteAfter) tok = new WParenRightW(pos, s);
                    else if (whiteBefore) tok = new WParenRight(pos, s);
                    else if (whiteAfter) tok = new ParenRightW(pos, s);
                    else tok = new ParenRight(pos, s);
                }
                else if (s == "-")
                {
                    if (whiteBefore && whiteAfter) tok = new WDashW(pos, s);
                    else if (whiteBefore) tok = new WDash(pos, s);
                    else if (whiteAfter) tok = new DashW(pos, s);
                    else tok = new Dash(pos, s);
                }
                else if (s == "+")
                {
                    if (whiteBefore && whiteAfter) tok = new WPlusW(pos, s);
                    else if (whiteBefore) tok = new WPlus(pos, s);
                    else if (whiteAfter) tok = new PlusW(pos, s);
                    else tok = new Plus(pos, s);
                }
                else if (s == "@")
                {
                    if (whiteBefore && whiteAfter) tok = new WAtW(pos, s);
                    else if (whiteBefore) tok = new WAt(pos, s);
                    else if (whiteAfter) tok = new AtW(pos, s);
                    else tok = new At(pos, s);
                }
            }
            else if (s.Length == 2)
            {
                if (s == "->")
                {
                    if (whiteBefore && whiteAfter) tok = new WInstanceArrowW(pos, s);
                    else if (whiteBefore) tok = new WInstanceArrow(pos, s);
                    else if (whiteAfter) tok = new InstanceArrowW(pos, s);
                    else tok = new InstanceArrow(pos, s);
                }
                else if (s == "=>")
                {
                    if (whiteBefore && whiteAfter) tok = new WStaticArrowW(pos, s);
                    else if (whiteBefore) tok = new WStaticArrow(pos, s);
                    else if (whiteAfter) tok = new StaticArrowW(pos, s);
                    else tok = new StaticArrow(pos, s);
                }
            }

            if (tok == null && m == ModeNormal && s[0] == '\\')
            {
                var adj = stream.NextChar() == EOF ? 1 : 0;
                var prevOffset = stream.GetOffset() - s.Length - adj;
                var prevChar = stream.CharCodeAt(prevOffset);
                var whiteBeforeBackslash = prevChar == CH_SPACE || prevChar == CH_NL || prevChar == CH_TAB || prevChar == CH_COLON;
                if (!whiteBeforeBackslash)
                {
                    tok = new AssociationName(pos, s);
                }
            }

            tok ??= new Identifier(pos, s);
            tokens.Add(tok);
        }
        buffer.Clear();
    }

    private void Process(string raw)
    {
        var stream = new LexerStream(raw.Replace("\r", ""));
        this.stream = stream;
        buffer = new LexerBuffer(stream.GetRaw());

        for (;;)
        {
            var current = stream.CurrentChar();
            buffer.Add(stream.GetOffset());
            var ahead = stream.NextChar();
            var aahead = stream.NextNextChar();

            if (m == ModeNormal)
            {
                if (SPLITS.Contains(ahead))
                {
                    Add();
                }
                else if (ahead == CH_QUOTE)
                {
                    // start string
                    Add();
                    m = ModeStr;
                }
                else if (ahead == CH_PIPE || ahead == CH_RBRACE)
                {
                    // start template
                    Add();
                    m = ModeTemplate;
                }
                else if (ahead == CH_BACKTICK)
                {
                    // start ping
                    Add();
                    m = ModePing;
                }
                else if (ahead == CH_HASH && aahead == CH_HASH)
                {
                    // start pragma
                    Add();
                    m = ModePragma;
                }
                else if (ahead == CH_DQUOTE
                    || (ahead == CH_STAR && current == CH_NL))
                {
                    // start comment
                    Add();
                    m = ModeComment;
                }
                else if (ahead == CH_AT && JsString.Trim(buffer.Get()).Length == 0)
                {
                    Add();
                }
                else if ((ahead == CH_DASH || ahead == CH_EQUALS) && aahead == CH_GT)
                {
                    Add();
                }
                else if (current == CH_GT
                    && ahead != CH_SPACE
                    && (stream.PrevChar() == CH_DASH || stream.PrevChar() == CH_EQUALS))
                {
                    // arrows
                    Add();
                }
                else if (buffer.Length() == 1
                    && (BUFS.Contains(current)
                    || (current == CH_DASH && ahead != CH_GT)))
                {
                    Add();
                }
            }
            else if (m == ModePragma && (ahead == CH_COMMA || ahead == CH_COLON
                || ahead == CH_DOT || ahead == CH_SPACE || ahead == CH_NL))
            {
                // end of pragma
                Add();
                m = ModeNormal;
            }
            else if (m == ModePing
                && buffer.Length() > 1
                && current == CH_BACKTICK
                && !(ahead == CH_BACKTICK && aahead == CH_BACKTICK)
                && ahead != CH_BACKTICK
                && buffer.CountIsEven(CH_BACKTICK))
            {
                // end of ping
                if (AFTER_LITERAL.Contains(ahead))
                {
                    Add();
                }
                // else: no separator after the literal, keep the buffer so the following
                // characters are glued onto the token, the parser will report the error
                m = ahead == CH_DQUOTE ? ModeComment : ModeNormal;
            }
            else if (m == ModeTemplate
                && buffer.Length() > 1
                && (current == CH_PIPE || current == CH_LBRACE)
                && stream.CountPrevious(CH_BACKSLASH) % 2 == 0)
            {
                // end of template
                if (current == CH_LBRACE || AFTER_LITERAL.Contains(ahead))
                {
                    Add();
                }
                m = ModeNormal;
            }
            else if (m == ModeTemplate
                && ahead == CH_RBRACE
                && current != CH_BACKSLASH)
            {
                Add();
            }
            else if (m == ModeStr
                && current == CH_QUOTE
                && buffer.Length() > 1
                && !(ahead == CH_QUOTE && aahead == CH_QUOTE)
                && ahead != CH_QUOTE
                && buffer.CountIsEven(CH_QUOTE))
            {
                // end of string
                Add();
                m = ahead == CH_DQUOTE ? ModeComment : ModeNormal;
            }
            else if (ahead == CH_NL && m != ModeTemplate)
            {
                Add();
                m = ModeNormal;
            }
            else if (m == ModeTemplate && current == CH_NL)
            {
                Add();
            }

            if (!stream.Advance())
            {
                break;
            }
        }

        Add();
    }
}
