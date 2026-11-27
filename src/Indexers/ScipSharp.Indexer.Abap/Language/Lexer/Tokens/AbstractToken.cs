// Ported from abaplint packages/core/src/abap/1_lexer/tokens/abstract_token.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer.Tokens;

public abstract class AbstractToken
{
    private readonly Position start;
    private readonly string str;
    private readonly string strUpper;

    protected AbstractToken(Position start, string str)
    {
        this.start = start;
        this.str = str;
        strUpper = JsString.ToUpper(str);
    }

    public string GetStr() => str;

    public string GetUpperStr() => strUpper;

    public int GetRow() => start.GetRow();

    public int GetCol() => start.GetCol();

    public Position GetStart() => start;

    public Position GetEnd() => new Position(start.GetRow(), start.GetCol() + str.Length);

    /// <summary>For debugging, mirrors the TS "debug.description".</summary>
    public override string ToString() => $"{GetType().Name} {str}";
}
