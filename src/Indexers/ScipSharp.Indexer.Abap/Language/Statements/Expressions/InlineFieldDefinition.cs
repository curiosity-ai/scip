// Ported from abaplint packages/core/src/abap/2_statements/expressions/inline_field_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InlineFieldDefinition : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var field = AltPrio(Expr<Field>(), Expr<FieldSymbol>());
        return AltPrio(Seq(field, "=", Expr<Source>()),
                       Seq(Expr<Field>(), "TYPE", Expr<TypeName>()));
    }
}
