// Ported from abaplint packages/core/src/abap/2_statements/statements/set_titlebar.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SetTitlebar : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var wit = Seq("WITH", Plus(Expr<Source>()));

        var program = Seq("OF PROGRAM", Expr<Source>());

        var ret = Seq("SET TITLEBAR", Expr<Source>(), Opt(program), Opt(wit));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
