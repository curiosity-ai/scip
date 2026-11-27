// Ported from abaplint packages/core/src/abap/2_statements/statements/method_def.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodDef : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var def = Ver(Release.v740sp08, Seq("DEFAULT", AltPrio("FAIL", "IGNORE")), also: AlsoIn.OpenABAP);

        var parameters = Seq(OptPrio(AltPrio("FINAL", def, Expr<Abstract>())),
                               OptPrio(Expr<MethodDefImporting>()),
                               OptPrio(Expr<MethodDefExporting>()),
                               OptPrio(Expr<MethodDefChanging>()),
                               OptPrio(Expr<MethodDefReturning>()),
                               OptPrio(AltPrio(Expr<MethodDefRaising>(), VerNotLang(LanguageVersion.KeyUser, Expr<MethodDefExceptions>()))));

        var testing = Seq(OptPrio(Expr<Abstract>()), "FOR TESTING", OptPrio(AltPrio(Expr<MethodDefRaising>(), VerNotLang(LanguageVersion.KeyUser, Expr<MethodDefExceptions>()))));

        var result = Seq("RESULT", Expr<MethodParamName>());
        var link = Seq("LINK", Expr<MethodParamName>());
        var full = Seq("FULL", Expr<MethodParamName>());

        var modify = Alt(
          Seq("FOR ACTION", Expr<TypeName>(), OptPrio(result)),
          Seq("FOR CREATE", Alt(Expr<TypeName>(), Expr<EntityAssociation>())),
          Seq("FOR DELETE", Expr<TypeName>()),
          Seq("FOR UPDATE", Expr<TypeName>()));

        var forRead = Seq("FOR READ", Alt(Expr<TypeName>(), Expr<EntityAssociation>()), OptPrio(full), result, OptPrio(link));
        var forfunction = Seq("FOR FUNCTION", Expr<TypeName>(), result);

        var entityEventReference = Seq("REFERENCE", Tok<Tokens.ParenLeft>(), Expr<MethodParamName>(), Tok<Tokens.ParenRightW>());
        var entityEventParameter = Seq(AltPrio(entityEventReference, Expr<MethodParamName>()), "FOR", Expr<TypeName>());
        var entityEvent = Seq(OptPrio("FINAL"),
                                "FOR ENTITY EVENT",
                                OptPrio("IMPORTING"),
                                Plus(entityEventParameter));

        var behavior = AltPrio(
          "DDL OBJECT OPTIONS CDS SESSION CLIENT REQUIRED",
          Seq("TABLE FUNCTION", Expr<NamespaceSimpleName>()),
          Seq("VALIDATE ON SAVE IMPORTING", Expr<MethodParamName>(), "FOR", Expr<TypeName>()),
          Seq("MODIFY", Opt("IMPORTING"), Plus(Seq(Expr<MethodParamName>(), modify))),
          Seq("PRECHECK IMPORTING", Expr<MethodParamName>(), modify),
          Seq("DETERMINATION", Expr<TypeName>(), "IMPORTING", Expr<MethodParamName>(), "FOR", Expr<TypeName>()),
          Seq("VALIDATION", Expr<TypeName>(), "IMPORTING", Expr<MethodParamName>(), "FOR", Expr<TypeName>()),
          Seq("NUMBERING IMPORTING", Expr<MethodParamName>(), modify),
          Seq("READ IMPORTING", Expr<MethodParamName>(), AltPrio(forRead, forfunction)),
          Seq("FEATURES IMPORTING", Expr<MethodParamName>(), "REQUEST", Expr<NamespaceSimpleName>(), "FOR", Expr<NamespaceSimpleName>(), result),
          Seq("BEHAVIOR IMPORTING", Expr<MethodParamName>(), "FOR CREATE", Expr<TypeName>(), Expr<MethodParamName>(), "FOR UPDATE", Expr<TypeName>(), Expr<MethodParamName>(), "FOR DELETE", Expr<TypeName>()),
          Seq("BEHAVIOR IMPORTING", Expr<MethodParamName>(), "FOR READ", Expr<TypeName>(), result),
          Seq("BEHAVIOR IMPORTING", Expr<MethodParamName>(), "FOR UPDATE", Expr<TypeName>()),
          Seq(Alt("BEHAVIOR", "LOCK"), "IMPORTING", Expr<MethodParamName>(), "FOR LOCK", Expr<TypeName>()),
          Seq("DETERMINE", Alt("ON MODIFY", "ON SAVE"), "IMPORTING", Expr<MethodParamName>(), "FOR", Expr<TypeName>()),
          Seq("GLOBAL AUTHORIZATION IMPORTING REQUEST", Expr<MethodParamName>(), "FOR", Expr<TypeName>(), result),
          Seq("GLOBAL FEATURES IMPORTING REQUEST", Expr<MethodParamName>(), "FOR", Expr<TypeName>(), result),
          Seq(Seq(Opt("INSTANCE"), "AUTHORIZATION IMPORTING"), Expr<MethodParamName>(), "REQUEST", Expr<MethodParamName>(), "FOR", Expr<TypeName>(), result),
          Seq("INSTANCE FEATURES IMPORTING", Expr<MethodParamName>(), "REQUEST", Expr<MethodParamName>(), "FOR", Expr<TypeName>(), result));

        // todo, this is only from version something
        // AMDP OPTIONS clause blocked in KeyUser
        var amdp = VerNotLang(LanguageVersion.KeyUser, Seq(
          "AMDP OPTIONS", Per("READ-ONLY", Seq("CDS SESSION CLIENT", Alt("CURRENT", "DEPENDENT"))),
          OptPrio(Expr<MethodDefImporting>()),
          OptPrio(Expr<MethodDefExporting>()),
          OptPrio(Expr<MethodDefRaising>())));

        // FOR DDL OBJECT / FOR TABLE FUNCTION / FOR SCALAR FUNCTION blocked in KeyUser
        var regular = Seq(AltPrio("CLASS-METHODS", "METHODS"),
                            Expr<MethodName>(),
                            Alt(Seq(OptPrio(Expr<Abstract>()), OptPrio(def), Expr<Expressions.EventHandler>()),
                                parameters,
                                testing,
                                VerNotLang(LanguageVersion.KeyUser, Seq("FOR", behavior)),
                                amdp,
                                "NOT AT END OF MODE",
                                OptPrio(Expr<Redefinition>())));

        // RAP event handlers are instance methods; CLASS-METHODS is not allowed.
        var ret = Alt(Seq("METHODS", Expr<MethodName>(), VerNotLang(LanguageVersion.KeyUser, entityEvent)), regular);

        return ret;
    }
}
