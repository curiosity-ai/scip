// Ported from abaplint packages/core/src/abap/2_statements/statements/fieldsymbol.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FieldSymbol : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var stru = Seq("STRUCTURE", Expr<TypeName>(), "DEFAULT", Expr<Expressions.Field>());

        var anyStructure = Ver(Release.v916, Seq("TYPE", "ANY", "STRUCTURE"));

        return Seq("FIELD-SYMBOLS",
                   Expr<Expressions.FieldSymbol>(),
                   Opt(Alt(anyStructure, Expr<Expressions.Type>(), Expr<TypeTable>(), stru)));
    }
}
