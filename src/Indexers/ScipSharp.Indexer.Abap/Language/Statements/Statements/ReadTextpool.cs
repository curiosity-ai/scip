// Ported from abaplint packages/core/src/abap/2_statements/statements/read_textpool.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReadTextpool : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var language = Seq("LANGUAGE", Expr<Source>());
        var into = Seq("INTO", Expr<SimpleTarget>());
        var state = Seq("STATE", Expr<Source>());

        var ret = Seq("READ TEXTPOOL",
                        Expr<Source>(),
                        Per(into, language, state));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
