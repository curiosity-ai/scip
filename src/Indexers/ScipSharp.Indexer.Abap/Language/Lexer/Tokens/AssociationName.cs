// Ported from abaplint packages/core/src/abap/1_lexer/tokens/association_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer.Tokens;

public sealed class AssociationName(Position start, string str) : AbstractToken(start, str)
{
}
