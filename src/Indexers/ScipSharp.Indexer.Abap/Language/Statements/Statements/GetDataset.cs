// Ported from abaplint packages/core/src/abap/2_statements/statements/get_dataset.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GetDataset : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var position = Seq("POSITION", Expr<Target>());
        var attr = Seq("ATTRIBUTES", Expr<Target>());

        var ret = Seq("GET DATASET",
                        Expr<Target>(),
                        Opt(Per(position, attr)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
