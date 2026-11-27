// Ported from abaplint packages/core/src/abap/2_statements/statements/editor_call.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class EditorCall : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var title = Seq("TITLE", Expr<SimpleSource3>());

        var options = Per("DISPLAY-MODE", title);

        var ret = Seq("EDITOR-CALL FOR",
                        OptPrio("REPORT"),
                        Expr<Source>(),
                        OptPrio(options));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
