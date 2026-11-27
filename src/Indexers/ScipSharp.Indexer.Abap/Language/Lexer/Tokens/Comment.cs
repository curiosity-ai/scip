// Ported from abaplint packages/core/src/abap/1_lexer/tokens/comment.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer.Tokens;

public sealed class Comment(Position start, string str) : AbstractToken(start, str)
{
}
