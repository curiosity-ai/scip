// Ported from abaplint packages/core/src/abap/2_statements/expressions/type_structure.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeStructure : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var entity = Alt(Expr<TypeName>(), Expr<EntityAssociation>());

        var derivedTypes = DerivedTypes.DerivedTypesAlt(
          Ver(Release.v779, Seq("FUNCTION REQUEST", entity), also: AlsoIn.OpenABAP),
          Ver(Release.v779, Seq("ACTION REQUEST", entity), also: AlsoIn.OpenABAP),
          Seq("ACTION IMPORT", Expr<Source>()),
          Ver(Release.v781, Seq("GLOBAL AUTHORIZATION REQUEST", entity), also: AlsoIn.OpenABAP),
          Ver(Release.v781, Seq("GLOBAL AUTHORIZATION RESULT", entity), also: AlsoIn.OpenABAP),
          Ver(Release.v781, Seq("GLOBAL FEATURES REQUEST", entity), also: AlsoIn.OpenABAP),
          Ver(Release.v781, Seq("GLOBAL FEATURES RESULT", entity), also: AlsoIn.OpenABAP),
          Ver(Release.v780, Seq("AUTHORIZATION REQUEST", entity), also: AlsoIn.OpenABAP),
          Ver(Release.v776, Seq("FEATURES REQUEST", entity), also: AlsoIn.OpenABAP),
          Seq("PERMISSIONS REQUEST", Expr<NamespaceSimpleName>()),
          Ver(Release.v780, Seq("PERMISSIONS RESULT", entity), also: AlsoIn.OpenABAP),
          Seq("READ LINK", Expr<EntityAssociation>()),
          Seq("HIERARCHY", Expr<NamespaceSimpleName>()),
          Seq("EVENT", Expr<EventName>()));

        var structure = Ver(Release.v774, Seq("STRUCTURE FOR", derivedTypes), also: AlsoIn.OpenABAP);

        var response = Ver(Release.v776, Seq("RESPONSE FOR", AltPrio(
          Seq("FAILED EARLY", Expr<NamespaceSimpleName>()),
          Seq("FAILED LATE", Expr<NamespaceSimpleName>()),
          Seq("FAILED", Expr<NamespaceSimpleName>()),
          Seq("MAPPED EARLY", Expr<NamespaceSimpleName>()),
          Seq("MAPPED LATE", Expr<NamespaceSimpleName>()),
          Seq("MAPPED", Expr<NamespaceSimpleName>()),
          Seq("REPORTED EARLY", Expr<NamespaceSimpleName>()),
          Seq("REPORTED LATE", Expr<NamespaceSimpleName>()),
          Seq("REPORTED", Expr<NamespaceSimpleName>()))), also: AlsoIn.OpenABAP);

        var request = Ver(Release.v778, Seq("REQUEST FOR", Alt("CHANGE", "DELETE"), Expr<NamespaceSimpleName>()), also: AlsoIn.OpenABAP);

        return Seq("TYPE", AltPrio(structure, response, request), OptPrio("VALUE IS INITIAL"));
    }
}
