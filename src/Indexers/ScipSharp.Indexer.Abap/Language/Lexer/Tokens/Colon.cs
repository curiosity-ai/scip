// Ported from abaplint packages/core/src/abap/1_lexer/tokens/colon.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer.Tokens;

public sealed class Colon(Position start, string str) : AbstractToken(start, str)
{
}
