// Ported from abaplint packages/core/src/abap/2_statements/statements/insert_textpool.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InsertTextpool : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var state = Seq("STATE", Expr<Source>());
        var language = Seq("LANGUAGE", Expr<Source>());

        var ret = Seq("INSERT TEXTPOOL",
                        Expr<Source>(),
                        "FROM",
                        Expr<Source>(),
                        Opt(language),
                        Opt(state));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
