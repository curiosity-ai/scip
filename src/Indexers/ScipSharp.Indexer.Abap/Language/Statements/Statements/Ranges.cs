// Ported from abaplint packages/core/src/abap/2_statements/statements/ranges.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Ranges : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var occurs = Seq("OCCURS", Expr<Source>());

        var ret = Seq("RANGES",
                        Expr<DefinitionName>(),
                        "FOR",
                        Expr<SimpleFieldChain2>(),
                        OptPrio(occurs),
                        OptPrio(Expr<FieldLength>()));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
