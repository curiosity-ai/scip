// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_param.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodParam : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // The escape is part of the token: the lexer gives "!VALUE" as a single
        // Identifier, so a literal "VALUE" cannot match it and the whole METHODS
        // statement falls back to Unknown.
        var @ref = Seq(AltPrio("REFERENCE", "!REFERENCE"),
                        Tok<Tokens.ParenLeft>(),
                        Expr<MethodParamName>(),
                        Tok<Tokens.ParenRightW>());

        var value = Seq(AltPrio("VALUE", "!VALUE"),
                          Tok<Tokens.ParenLeft>(),
                          Expr<MethodParamName>(),
                          Tok<Tokens.ParenRightW>());

        var fieldsOrValue = Seq(AltPrio(value, @ref, Expr<MethodParamName>()),
                                  Expr<TypeParam>());

        return fieldsOrValue;
    }
}
