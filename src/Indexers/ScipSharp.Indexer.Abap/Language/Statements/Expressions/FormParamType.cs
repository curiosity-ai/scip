// Ported from abaplint packages/core/src/abap/2_statements/expressions/form_param_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FormParamType : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var def = Seq("DEFAULT", Alt(Expr<Constant>(), Expr<FieldChain>()));

        var table = Seq(Alt("STANDARD", "HASHED", "INDEX", "SORTED", "ANY"),
                          "TABLE");

        var tabseq = Seq(table, OptPrio(Seq("OF", Expr<TypeName>())));

        var ret = Seq(OptPrio(Alt("REF TO", "LINE OF")),
                        Expr<TypeName>(),
                        Opt(def));

        var like = Seq("LIKE", OptPrio(Alt("REF TO", "LINE OF")),
                         Expr<FieldChain>());

        return Alt(Seq("TYPE", AltPrio(tabseq, Ver(Release.v916, Seq("ANY", "STRUCTURE")), ret)), like);
    }
}
