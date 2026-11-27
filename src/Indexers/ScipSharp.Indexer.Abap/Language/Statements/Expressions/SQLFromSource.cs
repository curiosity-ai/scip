// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_from_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLFromSource : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var tab = Ver(Release.v752, Seq(Tok<Tokens.WAt>(), Expr<FieldChain>()), also: AlsoIn.OpenABAP);
        var aas = Seq("AS", Expr<SQLAsName>());
        var privileged = Ver(Release.v752, Seq("WITH", Expr<SQLPrivilegedAccess>()));
        var providedBy = OptPrio(Ver(Release.v796, Expr<SQLProvidedBy>()));
        var exposeClient = OptPrio(Ver(Release.v913, Expr<SQLExposeClientAs>()));
        var pathForEntity = OptPrio(VerNotLang(LanguageVersion.KeyUser, new SQLPathForEntity()));

        return Seq(AltPrio(new SQLHierarchyAggregate(),
                           new SQLHierarchyAccessor(),
                           new SQLHierarchySource(),
                           Seq(Expr<WithName>(), pathForEntity),
                           Seq(new DatabaseTable(true), OptPrio(Expr<SQLCDSParameters>()), pathForEntity),
                           tab),
                   providedBy,
                   OptPrio(privileged),
                   OptPrio(aas),
                   exposeClient);
    }
}
