// Ported from abaplint packages/core/src/abap/2_statements/statements/read_entities.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReadEntities : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var from = Seq("FROM", Expr<Source>());
        var fields = Seq("FIELDS", Tok<Tokens.WParenLeftW>(), Plus(Expr<SimpleName>()), Tok<Tokens.WParenRightW>(), "WITH", Expr<Source>());
        var all = Seq("ALL FIELDS WITH", Expr<Source>());
        var result = Seq("RESULT", Expr<Target>());
        var failed = Seq("FAILED", Expr<Target>());
        var reported = Seq("REPORTED", Expr<Target>());

        var execute = Seq("EXECUTE", Expr<SimpleName>(), from);

        var foo = Seq(Opt(Seq("BY", Expr<EMLEntityPath>())),
                        Alt(fields, from, all, execute),
                        OptPrio(result));

        var entity = Seq("ENTITY", Expr<NamespaceSimpleName>(),
                           Plus(foo));

        var s = Seq("ENTITIES OF", Expr<NamespaceSimpleName>(),
                      Opt("IN LOCAL MODE"),
                      Plus(entity),
                      OptPrio(Seq("LINK", Expr<Target>())),
                      OptPrio(Per(failed, reported)));

        var byall = Seq("BY", Expr<EMLEntityPath>(), all);
        var by = Seq("BY", Expr<EMLEntityPath>(), fields);
        var sub = Seq(Alt(all, fields, from, by, byall), result);
        var single = Seq("ENTITY", Opt("IN LOCAL MODE"), Alt(Expr<NamespaceSimpleName>(), Expr<EntityAssociation>()), Plus(sub), OptPrio(failed), OptPrio(reported));
        return Ver(Release.v754, Seq("READ", Alt(s, single)), also: AlsoIn.OpenABAP);
    }
}
