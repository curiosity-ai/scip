// Ported from abaplint packages/core/src/abap/2_statements/statements/add_corresponding.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class AddCorresponding : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("ADD-CORRESPONDING",
                        Expr<Source>(),
                        "TO",
                        Expr<Target>());

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
