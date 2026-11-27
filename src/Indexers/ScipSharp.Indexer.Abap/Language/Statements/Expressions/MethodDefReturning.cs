// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_def_returning.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodDefReturning : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // see MethodParam: the lexer gives "!VALUE" as one Identifier
        var value = Seq(AltPrio("VALUE", "!VALUE"),
                          Tok<Tokens.ParenLeft>(),
                          Expr<MethodParamName>(),
                          Tok<Tokens.ParenRightW>());

        return Seq("RETURNING", value, Expr<TypeParam>());
    }
}
