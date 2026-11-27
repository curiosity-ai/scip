// Ported from abaplint packages/core/src/abap/2_statements/statements/call_dialog.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallDialog : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var from = Seq(Expr<FieldSub>(), OptPrio(Seq("FROM", Expr<Source>())));
        var exporting = Seq("EXPORTING", Plus(from));

        var to = Seq(Expr<Expressions.Field>(), OptPrio(Seq("TO", Expr<Expressions.Field>())));
        var importing = Seq("IMPORTING", Plus(to));

        var ret = Seq("CALL DIALOG",
                        Alt(Expr<Expressions.Constant>(), Expr<FieldSub>()),
                        Opt(exporting),
                        Opt(importing));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
