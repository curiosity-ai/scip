// Ported from abaplint packages/core/src/abap/2_statements/statements/modify_entities.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ModifyEntities : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var withh = Seq("WITH", Expr<Source>());
        var fieldsWith = Seq("FIELDS (", Plus(Expr<SimpleName>()), ")", withh);
        var by = Seq("BY", Expr<EMLEntityPath>());
        var relating = Seq("RELATING TO", Expr<NamespaceSimpleName>(), "BY", Expr<NamespaceSimpleName>());

        var execute = Seq("EXECUTE", Expr<NamespaceSimpleName>(), "FROM", Expr<Source>());
        var create = Seq("CREATE", Opt(by), "FROM", Expr<Source>(), Opt(relating));
        var updateFrom = Seq("UPDATE FROM", Expr<Source>(), Opt(relating));
        var deleteFrom = Seq("DELETE FROM", Expr<Source>());
        var updateFields = Seq("UPDATE", fieldsWith);
        var updateSetFields = Seq("UPDATE SET FIELDS WITH", Expr<Source>());

        var operation = Alt(
          updateSetFields,
          Seq("CREATE SET FIELDS WITH", Expr<Source>()),
          updateFields,
          deleteFrom,
          updateFrom,
          create,
          execute,
          Seq("CREATE", Opt(by), OptPrio("AUTO FILL CID"), AltPrio(withh, fieldsWith)));

        var failed = Seq("FAILED", Expr<Target>());
        var result = Seq("RESULT", Expr<Target>());
        var mapped = Seq("MAPPED", Expr<Target>());
        var reported = Seq("REPORTED", Expr<Target>());

        var end = OptPrio(Per(failed,
                                result,
                                mapped,
                                reported));

        var entities = Seq(OptPrio("AUGMENTING"), "ENTITIES OF", Expr<NamespaceSimpleName>(),
                             OptPrio(AltPrio("IN LOCAL MODE", Seq(Opt("FORWARDING"), "PRIVILEGED"))),
                             PlusPrio(Seq("ENTITY", Expr<NamespaceSimpleName>(), Plus(operation))));

        var dynamic = Seq("ENTITIES",
                            OptPrio(AltPrio("IN LOCAL MODE", Seq(Opt("FORWARDING"), "PRIVILEGED"))),
                            "OPERATIONS", Expr<Source>());

        var create2 = Seq("CREATE", fieldsWith, Opt(Seq("CREATE BY", Expr<EMLEntityPath>(), fieldsWith)));
        var create3 = Seq("CREATE BY", Expr<EMLEntityPath>(), fieldsWith);
        var create4 = Seq("CREATE FROM", Expr<Source>(), Plus(Seq("CREATE BY", Expr<EMLEntityPath>(), "FROM", Expr<Source>())));

        var entity = Seq("ENTITY",
                           Opt("IN LOCAL MODE"),
                           Alt(Expr<NamespaceSimpleName>(), Expr<EntityAssociation>()),
                           Alt(execute, create, updateFields, deleteFrom, updateSetFields, updateFrom, create2, create3, create4));

        return Ver(Release.v754, Seq("MODIFY", Alt(entities, dynamic, entity), end), also: AlsoIn.OpenABAP);
    }
}
