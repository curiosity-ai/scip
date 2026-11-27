// Ported from abaplint packages/core/src/abap/2_statements/expressions/type_simple_ref.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeSimpleRef : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var tableKind = Seq(Alt("ANY", "STANDARD", "HASHED", "SORTED", "INDEX"), "TABLE");

        return AltPrio(
          Seq("LINE", "OF", Expr<TypeName>()),
          Seq("REF", "TO", Expr<TypeName>()),
          tableKind,
          Seq("ANY", "STRUCTURE"),
          Expr<TypeName>());
    }
}
