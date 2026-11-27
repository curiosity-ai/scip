// Ported from abaplint packages/core/src/abap/2_statements/statements/type_enum.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeEnum : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        // it is also possible to define without Value, this is covered by normal type
        var ret = Ver(Release.v751, Seq("TYPES", Expr<NamespaceSimpleName>(), Expr<Value>()), also: AlsoIn.OpenABAP);

        return ret;
    }
}
