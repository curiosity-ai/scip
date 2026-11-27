// Ported from abaplint packages/core/src/abap/2_statements/statements/import_dynpro.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ImportDynpro : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("IMPORT DYNPRO",
                        Expr<Target>(),
                        Expr<Target>(),
                        Expr<Target>(),
                        Expr<Target>(),
                        "ID",
                        Expr<Source>());

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
