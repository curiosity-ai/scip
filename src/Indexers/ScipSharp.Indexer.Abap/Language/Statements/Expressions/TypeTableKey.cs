// Ported from abaplint packages/core/src/abap/2_statements/expressions/type_table_key.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeTableKey : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var uniqueness = Alt("NON-UNIQUE", "UNIQUE");
        string defaultKey = "DEFAULT KEY";
        var emptyKey = Ver(Release.v740sp02, "EMPTY KEY", also: AlsoIn.OpenABAP);

        var components = PlusPrio(Seq(StopBefore1("WITH", "INITIAL", "WITHOUT"), StopBefore("READ", "-"), Expr<FieldSub>()));

        var further = Seq(Alt("WITHOUT", "WITH"), "FURTHER SECONDARY KEYS");

        var alias = Seq("ALIAS", Expr<Field>());

        var key = Seq("WITH",
                        OptPrio(uniqueness),
                        AltPrio(defaultKey, emptyKey,
                                Seq(Opt(Alt("SORTED", "HASHED")),
                                    "KEY",
                                    AltPrio(Seq(Expr<Field>(), Opt(alias), "COMPONENTS", components),
                                            components))),
                        OptPrio(further),
                        OptPrio("READ-ONLY"));

        return key;
    }
}
