// Ported from abaplint packages/core/src/abap/2_statements/statements/read_dataset.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReadDataset : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("READ DATASET",
                        Expr<Source>(),
                        "INTO",
                        Expr<Target>(),
                        Opt(Per(Seq("MAXIMUM LENGTH", Expr<Source>()),
                                Seq("ACTUAL LENGTH", Expr<Target>()),
                                Seq("LENGTH", Expr<Target>()))));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
