// Ported from abaplint packages/core/src/abap/2_statements/statements/generate_dynpro.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GenerateDynpro : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var line = Seq("LINE", Expr<Target>());
        var word = Seq("WORD", Expr<Target>());

        var ret = Seq("GENERATE DYNPRO",
                        Expr<Source>(),
                        Expr<Source>(),
                        Expr<Source>(),
                        Expr<Source>(),
                        "ID",
                        Expr<Source>(),
                        "MESSAGE",
                        Expr<Target>(),
                        Per(line, word));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
