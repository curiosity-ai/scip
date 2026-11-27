// Ported from abaplint packages/core/src/abap/2_statements/statements/at.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class At : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var field = Alt(Seq(Expr<FieldSub>(), Opt(Expr<FieldOffset>()), Opt(Expr<FieldLength>())),
                          Expr<Dynamic>(),
                          Expr<SourceFieldSymbol>());

        var atNew = Seq("NEW", field);
        var atEnd = Seq("END OF", field);
        var group = Regex(@"^[%\w]+$");

        var ret = Seq("AT", AltPrio(atNew, atEnd, group));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
