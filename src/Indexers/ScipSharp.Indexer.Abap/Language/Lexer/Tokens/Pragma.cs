// Ported from abaplint packages/core/src/abap/1_lexer/tokens/pragma.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer.Tokens;

public sealed class Pragma(Position start, string str) : AbstractToken(start, str)
{
}
