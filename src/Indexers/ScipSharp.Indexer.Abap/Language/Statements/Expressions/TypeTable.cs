// Ported from abaplint packages/core/src/abap/2_statements/expressions/type_table.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeTable : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        string header = "WITH HEADER LINE";
        var initial = Seq("INITIAL SIZE", Expr<Constant>());

        var generic = Seq(Opt(Alt("STANDARD", "HASHED", "INDEX", "SORTED", "ANY")), "TABLE");

        var normal1 = Seq(Opt(Alt("STANDARD", "HASHED", "INDEX", "SORTED", "ANY")),
                            "TABLE OF",
                            OptPrio("REF TO"),
                            Expr<TypeName>());

        var likeType = Seq(Opt(Alt("STANDARD", "HASHED", "INDEX", "SORTED", "ANY")),
                             "TABLE OF",
                             OptPrio("REF TO"),
                             Opt(Expr<FieldChain>()),
                             Opt(Per(header, initial, PlusPrio(Expr<TypeTableKey>()))));

        var rangeType = Seq("RANGE OF", Expr<TypeName>(), OptPrio(header), OptPrio(initial), OptPrio("VALUE IS INITIAL"));
        var rangeLike = Seq("RANGE OF", Expr<FieldChain>(), OptPrio(header), OptPrio(initial), OptPrio("VALUE IS INITIAL"));

        // a maximum of 15 secondary table keys can be defined
        // "WITH" is not allowed as a field name in keys
        var typetable = Alt(generic, Seq(normal1,
                                           Alt(Opt(Per(header, initial, PlusPrio(Expr<TypeTableKey>()))),
                                               Seq(Plus(Expr<TypeTableKey>()), OptPrio(initial))), OptPrio("VALUE IS INITIAL")));

        var occurs = Seq("OCCURS", AltPrio(Expr<Integer>(), Expr<FieldChain>()));

        var entity = Alt(Expr<TypeName>(), Expr<EntityAssociation>());

        var derivedTypes = DerivedTypes.DerivedTypesAlt(
          Ver(Release.v773, Seq("ACTION IMPORT", entity), also: AlsoIn.OpenABAP),
          Ver(Release.v774, Seq("READ LINK", entity), also: AlsoIn.OpenABAP),
          Ver(Release.v787, Seq("EVENT", entity), also: AlsoIn.OpenABAP));

        var derived = Ver(Release.v770, Seq("TABLE FOR", derivedTypes, OptPrio("VALUE IS INITIAL")), also: AlsoIn.OpenABAP);

        var oldType = Seq(Opt("REF TO"), Expr<TypeName>(), Alt(Seq(occurs, Opt(header)), header));
        var oldLike = Seq(Opt("REF TO"), Expr<FieldChain>(), Alt(Seq(occurs, Opt(header)), header));

        var typeLine = Seq("LINE OF", Expr<TypeName>(), occurs, header);

        var ret = AltPrio(
          Seq(occurs, Opt(header)),
          Seq("LIKE", Alt(oldLike, likeType, rangeLike, typeLine)),
          Seq("TYPE", Alt(oldType, typetable, rangeType, typeLine, derived)));

        return ret;
    }
}
