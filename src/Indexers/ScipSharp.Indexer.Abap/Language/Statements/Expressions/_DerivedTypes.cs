// Ported from abaplint packages/core/src/abap/2_statements/expressions/_derived_types.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public static class DerivedTypes
{

    public static IStatementRunnable[] CommonDerivedTypes()
    {
        var entity = Alt(Expr<TypeName>(), Expr<EntityAssociation>());

        // more-specific keyword phrases (e.g. "FAILED LATE") must precede less-specific ones ("FAILED")
        return [
            Ver(Release.v774, Seq("FAILED LATE", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v774, Seq("MAPPED LATE", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v774, Seq("REPORTED LATE", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v777, Seq("FAILED EARLY", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v777, Seq("MAPPED EARLY", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v777, Seq("REPORTED EARLY", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v776, Seq("FAILED", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v776, Seq("MAPPED", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v776, Seq("REPORTED", entity), also: AlsoIn.OpenABAP),

            Ver(Release.v773, Seq("FUNCTION IMPORT", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v773, Seq("FUNCTION RESULT", entity), also: AlsoIn.OpenABAP),

            Ver(Release.v773, Seq("ACTION RESULT", entity), also: AlsoIn.OpenABAP),

            Ver(Release.v781, Seq("INSTANCE AUTHORIZATION KEY", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v781, Seq("INSTANCE AUTHORIZATION REQUEST", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v781, Seq("INSTANCE AUTHORIZATION RESULT", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v781, Seq("INSTANCE FEATURES KEY", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v781, Seq("INSTANCE FEATURES REQUEST", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v781, Seq("INSTANCE FEATURES RESULT", entity), also: AlsoIn.OpenABAP),

            Ver(Release.v780, Seq("AUTHORIZATION RESULT", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v781, Seq("AUTHORIZATION KEY", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v776, Seq("FEATURES RESULT", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v776, Seq("FEATURES KEY", entity), also: AlsoIn.OpenABAP),

            Ver(Release.v780, Seq("PERMISSIONS KEY", entity), also: AlsoIn.OpenABAP),

            Seq("READ IMPORT", entity),
            Seq("READ RESULT", entity),
            Ver(Release.v915, Seq("READ CHANGES", entity), also: AlsoIn.OpenABAP),

            Seq("CREATE", entity),
            Seq("DELETE", entity),
            Seq("UPDATE", entity),
            Ver(Release.v777, Seq("LOCK", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v775, Seq("KEY OF", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v776, Seq("DETERMINATION", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v776, Seq("VALIDATION", entity), also: AlsoIn.OpenABAP),
            Ver(Release.v778, Seq("CHANGE", entity), also: AlsoIn.OpenABAP),
        ];
    }

    public static IStatementRunnable DerivedTypesAlt(params IStatementRunnable[] extra)
    {
        IStatementRunnable[] all = [.. extra, .. CommonDerivedTypes()];
        var first = all[0];
        var second = all[1];
        var rest = all.Skip(2).ToArray();
        return AltPrioOf([first, second, .. rest]);
    }
}
