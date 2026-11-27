// Ported from abaplint packages/core/src/abap/5_syntax/_statement_syntax.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax;

public interface StatementSyntax
{
    void RunSyntax(StatementNode node, SyntaxInput input);
}
