// Ported from abaplint packages/core/src/abap/2_statements/statements/move.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Move : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var mov = VerNotLang(LanguageVersion.Cloud, "MOVE");

        var move = Seq(mov,
                         AltPrio(
                           Seq("EXACT", Expr<Source>(), "TO", Expr<Target>()),
                           Seq(Expr<Source>(), AltPrio("?TO", "TO"), Expr<Target>())),
                         Opt(Seq("PERCENTAGE", Expr<Source>(), Opt(Alt("LEFT", "RIGHT")))));

        var calcAssign = Ver(Release.v754,
                               Alt(Seq(Tok<Tokens.WPlus>(), "="),
                                   Seq(Tok<Tokens.WDash>(), "="),
                                   "/=",
                                   "*=",
                                   "&&="), also: AlsoIn.OpenABAP);

        var chained = Seq("=", Star(Seq(Expr<Target>(), "=")));

        var equals = AltPrio(AltPrio(chained, "?="), calcAssign);

        // todo, move "?=" to CAST?
        var eq = Seq(Expr<Target>(), equals, Expr<Source>());

        return Alt(move, eq);
    }
}
