// Ported from abaplint packages/core/src/abap/1_lexer/tokens/wstatic_arrow.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer.Tokens;

public sealed class WStaticArrow(Position start, string str) : AbstractToken(start, str)
{
    public static string Railroad() => " =>";
}
