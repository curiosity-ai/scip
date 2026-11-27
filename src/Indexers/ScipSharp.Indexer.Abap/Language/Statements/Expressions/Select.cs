// Ported from abaplint packages/core/src/abap/2_statements/expressions/select.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Select : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var into = AltPrio(Expr<SQLIntoTable>(), Expr<SQLIntoStructure>(), Expr<SQLIntoList>());

        var standalone = AltPrio(
          Seq("SINGLE", OptPrio(VerNotLang(LanguageVersion.KeyUser, "FOR UPDATE")), SelectCore.BuildSelectSingleCore(true)),
          SelectCore.BuildSelectCore(true));

        var unionTail = Ver(Release.v750, PlusPrio(Expr<SQLSetOp>()), also: AlsoIn.OpenABAP);
        var chained = Seq(SelectCore.BuildSelectCore(false, false), unionTail, OptPrio(Expr<SQLOrderBy>()), OptPrio(into));

        return Seq("SELECT", AltPrio(chained, standalone));
    }
}
