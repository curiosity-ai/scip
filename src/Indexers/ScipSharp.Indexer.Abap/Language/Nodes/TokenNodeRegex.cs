// Ported from abaplint packages/core/src/abap/nodes/token_node_regex.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.Nodes;

public class TokenNodeRegex(AbstractToken token) : TokenNode(token)
{
}
