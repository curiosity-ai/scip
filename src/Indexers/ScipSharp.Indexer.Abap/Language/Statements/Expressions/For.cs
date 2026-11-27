// Ported from abaplint packages/core/src/abap/2_statements/expressions/for.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class For : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var where = Seq("WHERE (", Expr<ComponentCond>(), ")");
        var from = Seq("FROM", Expr<Source>());
        var to = Seq("TO", Expr<Source>());
        var inn = Seq(Expr<InlineLoopDefinition>(), OptPrio(from), OptPrio(to), OptPrio(where));
        var then = Seq("THEN", Expr<Source>());
        var whil = Seq(AltPrio("UNTIL", "WHILE"), Expr<Cond>());
        var itera = Seq(Expr<InlineFieldDefinition>(), Opt(then), whil);

        var groupBy = Seq("GROUP BY",
                            Alt(Expr<FieldChain>(), Seq("(", Plus(Expr<LoopGroupByComponent>()), ")")),
                            Opt(Seq(Alt("ASCENDING", "DESCENDING"), Opt("AS TEXT"))),
                            Opt("WITHOUT MEMBERS"));

        var t = Alt(Expr<TargetField>(), Expr<TargetFieldSymbol>());
        var groups = Ver(Release.v740sp08, Seq("GROUPS", t, "OF", t, "IN", Expr<Source>(), OptPrio(groupBy)), also: AlsoIn.OpenABAP);

        var f = Seq("FOR", Alt(itera, inn, groups), OptPrio(Expr<Let>()));

        return Ver(Release.v740sp05, f, also: AlsoIn.OpenABAP);
    }
}
