// Ported from abaplint packages/core/src/abap/2_statements/statements/export.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// todo, cloud, split?
public class Export : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var from = Seq(AltPrio("FROM", "="), Expr<Source>());
        var client = Seq("CLIENT", Expr<Source>());
        var id = Seq("ID", Expr<Source>());
        var @using = Seq("USING", Expr<Source>());

        var cluster = Seq(Expr<NamespaceSimpleName>(),
                            Tok<Tokens.ParenLeft>(),
                            Regex(@"^[\w$%\^~]{2}$"),
                            Tok<Tokens.ParenRightW>());

        var buffer = Seq("DATA BUFFER", Expr<Target>());
        var memory = VerNotLang(LanguageVersion.KeyUser, Seq("MEMORY", Opt(Seq("ID", Expr<Source>()))));
        var table = Seq("INTERNAL TABLE", Expr<Target>());
        var shared = VerNotLang(LanguageVersion.KeyUser, Seq(Alt("SHARED MEMORY", "SHARED BUFFER"), cluster, Per(from, client, id)));
        var database = VerNotLang(LanguageVersion.KeyUser, Seq("DATABASE", cluster, Per(from, client, id, @using)));

        var target = Alt(buffer, memory, database, table, shared);

        var left = Alt(Expr<FieldSub>(), Expr<Expressions.FieldSymbol>());

        var source = Alt(Plus(AltPrio(Seq(left, from), left)),
                           Expr<Dynamic>(), Expr<Expressions.Constant>());

        var compression = Seq("COMPRESSION", Alt("ON", "OFF"));
        var hint = Seq("CODE PAGE HINT", Expr<Source>());

        return Seq("EXPORT", source, "TO", target, Opt(compression), Opt(hint));
    }
}
