// Ported from abaplint packages/core/src/abap/1_lexer/lexer_buffer.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer;

/// <summary>Tracks the [start, end) offset range of the current token in the raw input.</summary>
public sealed class LexerBuffer
{
    private readonly string raw;
    private int start;
    private int end; // exclusive
    private bool empty;

    public LexerBuffer(string raw)
    {
        this.raw = raw;
        start = 0;
        end = 0;
        empty = true;
    }

    public void Add(int offset)
    {
        if (empty)
        {
            start = offset < 0 ? 0 : offset;
            empty = false;
        }
        end = offset + 1;
    }

    /// <summary>JS substring semantics: arguments are clamped to [0, length].</summary>
    public string Get()
    {
        var s = Math.Clamp(start, 0, raw.Length);
        var e = Math.Clamp(end, 0, raw.Length);
        if (s > e) (s, e) = (e, s);
        return raw.Substring(s, e - s);
    }

    public int Length() => end - start;

    public void Clear()
    {
        start = 0;
        end = 0;
        empty = true;
    }

    public bool CountIsEven(int ch)
    {
        var count = 0;
        for (var i = start; i < end; i += 1)
        {
            if (i >= 0 && i < raw.Length && raw[i] == ch)
            {
                count += 1;
            }
        }
        return count % 2 == 0;
    }
}
