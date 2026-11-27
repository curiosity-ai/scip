// Ported from abaplint packages/core/src/abap/2_statements/statements/interface_def.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InterfaceDef : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var val = Seq(Expr<AttributeName>(), "=", Expr<Source>());

        var dataValues = Seq("DATA VALUES", Plus(val));

        var options = Alt(Seq(Expr<AbstractMethods>(), Opt(Expr<FinalMethods>())),
                            Expr<FinalMethods>(),
                            "ALL METHODS ABSTRACT",
                            "ALL METHODS FINAL",
                            Ver(Release.v740sp02, "PARTIALLY IMPLEMENTED", also: AlsoIn.OpenABAP));

        return Seq("INTERFACES",
                   Expr<InterfaceName>(),
                   Opt(options),
                   Opt(dataValues));
    }
}
