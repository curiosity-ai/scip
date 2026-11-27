// Ported from abaplint packages/core/src/abap/1_lexer/lexer_stream.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer;

public sealed class LexerStream
{
    private const int NL = 10;   // "\n"
    private const int EOF = -1;  // no character (start/end of file)

    private readonly string raw;
    private int offset = -1;
    private int row;
    private int col;

    public LexerStream(string raw)
    {
        this.raw = raw;
        row = 0;
        col = 0;
    }

    public bool Advance()
    {
        if (CurrentChar() == NL)
        {
            col = 1;
            row = row + 1;
        }

        if (offset == raw.Length)
        {
            col = col - 1;
            return false;
        }

        col = col + 1;

        offset = offset + 1;
        return true;
    }

    public int GetCol() => col;

    public int GetRow() => row;

    // the *Char() accessors return character codes, EOF (-1) when out of range

    public int PrevChar()
    {
        var o = offset - 1;
        if (o < 0) return EOF;
        return raw[o];
    }

    public int PrevPrevChar()
    {
        var o = offset - 2;
        if (o < 0) return EOF;
        return raw[o];
    }

    /// <summary>Number of consecutive occurrences of the character directly before the current one.</summary>
    public int CountPrevious(int code)
    {
        var count = 0;
        for (var o = offset - 1; o >= 0 && raw[o] == code; o--)
        {
            count++;
        }
        return count;
    }

    public int CurrentChar()
    {
        if (offset < 0)
        {
            return NL; // simulate newline at start of file to handle star(*) comments
        }
        else if (offset >= raw.Length)
        {
            return EOF;
        }
        return raw[offset];
    }

    public int NextChar()
    {
        var o = offset + 1;
        if (o >= raw.Length) return EOF;
        return raw[o];
    }

    public int NextNextChar()
    {
        var o = offset + 2;
        if (o >= raw.Length) return EOF;
        return raw[o];
    }

    public int CharCodeAt(int o)
    {
        if (o < 0 || o >= raw.Length) return EOF;
        return raw[o];
    }

    public string GetRaw() => raw;

    public int GetOffset() => offset;
}
