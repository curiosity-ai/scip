// Ported from abaplint packages/core/src/abap/2_statements/statements/assign_local_copy.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class AssignLocalCopy : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var init = Seq("INITIAL", Alt(Expr<Source>(), Expr<Dynamic>()));
        var iline = Seq("INITIAL LINE OF", Alt(Expr<Source>(), Expr<Dynamic>()));
        var main = Seq("MAIN TABLE FIELD", Expr<Dynamic>());

        var ret = Seq("ASSIGN LOCAL COPY OF",
                        Alt(init, iline, main, Expr<Source>()),
                        "TO",
                        Expr<TargetFieldSymbol>());

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
