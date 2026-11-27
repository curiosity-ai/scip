// Ported from abaplint packages/core/src/abap/1_lexer/tokens/dashw.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer.Tokens;

public sealed class DashW(Position start, string str) : AbstractToken(start, str)
{
    public static string Railroad() => "- ";
}
