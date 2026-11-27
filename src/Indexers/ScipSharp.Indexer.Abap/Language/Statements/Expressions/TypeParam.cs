// Ported from abaplint packages/core/src/abap/2_statements/expressions/type_param.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeParam : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var table = Seq(AltPrio("STANDARD", "HASHED", "INDEX", "SORTED", "ANY"),
                          "TABLE");

        var foo = Seq(OptPrio(Seq(table, "OF")), OptPrio("REF TO"));

        string typeLine = "LINE OF";

        var ret = Seq(Alt(foo, typeLine),
                        Expr<TypeNameOrInfer>(),
                        Opt(Expr<Default>()));

        var like = Seq("LIKE", Opt("LINE OF"), Expr<FieldChain>(), OptPrio(Expr<Default>()));

        var entity = Alt(Expr<TypeName>(), Expr<EntityAssociation>());
        var rapTable = Ver(Release.v770,
                             Seq("TYPE", "TABLE FOR", DerivedTypes.DerivedTypesAlt(
                               Ver(Release.v773, Seq("ACTION IMPORT", entity), also: AlsoIn.OpenABAP),
                               Ver(Release.v774, Seq("READ LINK", entity), also: AlsoIn.OpenABAP),
                               Ver(Release.v787, Seq("EVENT", entity), also: AlsoIn.OpenABAP)), OptPrio("VALUE IS INITIAL")), also: AlsoIn.OpenABAP);

        // Method parameters also allow the RAP table and response type forms.
        return AltPrio(rapTable, Expr<TypeStructure>(), Seq("TYPE", Alt(table, ret)), like);
    }
}
