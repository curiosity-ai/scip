// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_def_changing.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodDefChanging : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var anyStructureParam = Ver(Release.v916, Seq(Expr<MethodParamName>(), "TYPE", "ANY", "STRUCTURE", OptPrio("OPTIONAL")));

        return Seq("CHANGING", Plus(AltPrio(anyStructureParam, Expr<MethodParamOptional>())));
    }
}
