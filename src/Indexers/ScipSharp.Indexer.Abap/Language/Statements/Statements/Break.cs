// Ported from abaplint packages/core/src/abap/2_statements/statements/break.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Break : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var next = Str("AT NEXT APPLICATION STATEMENT");

        // note BREAK is a special macro that wraps the parameter in a char
        var ret = AltPrio(Seq("BREAK-POINT", OptPrio(AltPrio(next, Expr<Source>()))),
                            Seq("BREAK", StarPrio(Regex(@".*"))));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
