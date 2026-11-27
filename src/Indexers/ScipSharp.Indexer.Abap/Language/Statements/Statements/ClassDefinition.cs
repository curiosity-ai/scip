// Ported from abaplint packages/core/src/abap/2_statements/statements/class_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ClassDefinition : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var create = Seq("CREATE", AltPrio("PUBLIC", "PROTECTED", "PRIVATE"));

        var level = AltPrio("CRITICAL", "HARMLESS", "DANGEROUS");
        var risk = Seq("RISK LEVEL", level);

        var time = AltPrio("LONG", "MEDIUM", "SHORT");
        var duration = Seq("DURATION", time);

        var blah = Per(Expr<ClassGlobal>(),
                         Expr<ClassFinal>(),
                         "ABSTRACT",
                         Seq("INHERITING FROM", Expr<SuperClassName>()),
                         create,
                         "FOR TESTING",
                         risk,
                         "SHARED MEMORY ENABLED",
                         duration,
                         Ver(Release.v754, Seq("FOR BEHAVIOR OF", Expr<BehaviorDefinitionName>()), also: AlsoIn.OpenABAP),
                         Ver(Release.v758, Seq("FOR EVENTS OF", Expr<BehaviorDefinitionName>())),
                         Expr<ClassFriends>());

        var def = Seq("DEFINITION", OptPrio(blah));

        return Seq("CLASS", Expr<ClassName>(), def);
    }
}
