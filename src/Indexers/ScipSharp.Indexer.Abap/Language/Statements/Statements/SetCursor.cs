// Ported from abaplint packages/core/src/abap/2_statements/statements/set_cursor.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SetCursor : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var line = Seq("LINE", Expr<Source>());
        var offset = Seq("OFFSET", Expr<Source>());
        var field = Seq("FIELD", Expr<Source>());
        var pos = Seq(Expr<Source>(), Expr<Source>());
        var ret = Seq("SET CURSOR", AltPrio(Per(field, offset, line), pos));
        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
