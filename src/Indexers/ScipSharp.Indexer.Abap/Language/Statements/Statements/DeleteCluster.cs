// Ported from abaplint packages/core/src/abap/2_statements/statements/delete_cluster.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DeleteCluster : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var client = Seq("CLIENT", Expr<Source>());

        var ret = Seq("DELETE FROM DATABASE",
                        Expr<NamespaceSimpleName>(),
                        Tok<Tokens.ParenLeft>(),
                        Expr<SimpleName>(),
                        Tok<Tokens.ParenRightW>(),
                        Opt(client),
                        "ID",
                        Expr<Source>());

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
