// Ported from abaplint packages/core/src/abap/2_statements/statements/insert_field_group.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InsertFieldGroup : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var into = Seq("INTO", Expr<Target>());

        var src = Alt(Ver(Release.v740sp02, Plus(Expr<Source>())), Plus(Expr<SimpleSource1>()));

        var ret = Seq("INSERT", src, into);

        return ret;
    }
}
