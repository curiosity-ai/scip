// Ported from abaplint packages/core/src/abap/2_statements/statements/delete_textpool.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DeleteTextpool : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var language = Seq("LANGUAGE", Expr<Source>());
        var state = Seq("STATE", Expr<Source>());

        var ret = Seq("DELETE TEXTPOOL",
                        Expr<Source>(),
                        Opt(language),
                        Opt(state));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
