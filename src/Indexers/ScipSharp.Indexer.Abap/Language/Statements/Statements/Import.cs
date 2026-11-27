// Ported from abaplint packages/core/src/abap/2_statements/statements/import.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Import : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var dto = Seq("TO", Expr<Target>());
        var client = Seq("CLIENT", Expr<Source>());
        var id = Seq("ID", Expr<Source>());
        var @using = Seq("USING", Expr<Source>());

        var cluster = Seq(Expr<NamespaceSimpleName>(),
                            Tok<Tokens.ParenLeft>(),
                            Regex(@"^[\w$%\^]{2}$"),
                            Tok<Tokens.ParenRightW>());

        var buffer = Seq("DATA BUFFER", Expr<Source>());
        var memory = VerNotLang(LanguageVersion.KeyUser, Seq("MEMORY", Opt(Seq("ID", Expr<Source>()))));
        var table = Seq("INTERNAL TABLE", Expr<Source>());
        var shared = VerNotLang(LanguageVersion.KeyUser, Seq(Alt("SHARED MEMORY", "SHARED BUFFER"), cluster, Per(dto, client, id)));
        var database = VerNotLang(LanguageVersion.KeyUser, Seq("DATABASE", cluster, Per(dto, client, id, @using)));
        var logfile = VerNotLang(LanguageVersion.KeyUser, Seq("LOGFILE ID", Expr<Source>()));

        var source = Alt(buffer, memory, database, table, shared, logfile);

        var to = Plus(Seq(Expr<ComponentChainSimple>(), Alt("TO", "INTO"), Expr<Target>()));

        var toeq = Plus(Seq(Alt(Expr<ComponentChainSimple>(), Expr<Expressions.FieldSymbol>()), "=", Expr<Target>()));

        var target = Alt(toeq,
                           to,
                           Expr<Dynamic>(),
                           Plus(Expr<Target>()));

        var options = Per("ACCEPTING PADDING",
                            "IGNORING CONVERSION ERRORS",
                            "IN CHAR-TO-HEX MODE",
                            "IGNORING STRUCTURE BOUNDARIES",
                            "ACCEPTING TRUNCATION",
                            Seq("REPLACEMENT CHARACTER", Expr<Source>()),
                            Seq("CODE PAGE INTO", Expr<Source>()),
                            Seq("ENDIAN INTO", Expr<Source>()));

        var ret = Seq("IMPORT", target, "FROM", source, Opt(options));

        return ret;
    }
}
