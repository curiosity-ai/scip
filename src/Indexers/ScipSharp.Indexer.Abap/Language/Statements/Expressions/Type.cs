// Ported from abaplint packages/core/src/abap/2_statements/expressions/type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Type : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var indicatorSpec = AltPrio(Ver(Release.v784, "AS BITFIELD"), Seq("TYPE", Expr<TypeName>()));
        var indicators = Seq("WITH INDICATORS", Expr<ComponentName>(), OptPrio(indicatorSpec));

        var typeType = Seq(Expr<TypeName>(), OptPrio(Expr<Default>()));

        var like = AltPrio(Seq("LINE OF", Expr<FieldChain>()),
                             Seq("REF TO", Expr<FieldChain>()),
                             Expr<FieldChain>());

        var type = AltPrio(Seq("LINE OF", typeType),
                             Seq("REF TO", typeType),
                             Seq(typeType, OptPrio(Expr<LOBHandle>())));

        var ret = Seq(AltPrio(Seq("LIKE", like), Seq("TYPE", type)), OptPrio(Ver(Release.v781, indicators)));

        return ret;
    }
}
