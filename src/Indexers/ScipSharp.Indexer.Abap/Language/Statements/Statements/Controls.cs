// Ported from abaplint packages/core/src/abap/2_statements/statements/controls.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Controls : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var tableview = Seq("TABLEVIEW USING SCREEN", Expr<Source>());
        var type = Seq("TYPE", Alt(tableview, "TABSTRIP"));
        var ret = Seq("CONTROLS", Expr<NamespaceSimpleName>(), type);

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
