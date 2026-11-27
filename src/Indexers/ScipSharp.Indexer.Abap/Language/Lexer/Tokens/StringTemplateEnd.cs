// Ported from abaplint packages/core/src/abap/1_lexer/tokens/string_template_end.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Lexer.Tokens;

public sealed class StringTemplateEnd(Position start, string str) : AbstractToken(start, str)
{
}
