// Ported from abaplint packages/core/src/abap/2_statements/statements/scan.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Scan : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var tokens = Seq("TOKENS INTO", Expr<Target>());
        var word = Seq("WORD INTO", Expr<Target>());
        var line = Seq("LINE INTO", Expr<Target>());
        var statements = Seq("STATEMENTS INTO", Expr<Target>());
        var levels = Seq("LEVELS INTO", Expr<Target>());
        var structures = Seq("STRUCTURES INTO", Expr<Target>());
        var include = Seq("INCLUDE INTO", Expr<Target>());
        var offset = Seq("OFFSET INTO", Expr<Target>());
        var enh = Seq("ENHANCEMENTS INTO", Expr<Target>());
        var enhO = Seq("ENHANCEMENT OPTIONS INTO", Expr<Target>());
        var keywords = Seq("KEYWORDS FROM", Expr<Source>());
        var pragmas = Seq("WITH PRAGMAS", Expr<Source>());
        var overflow = Seq("OVERFLOW INTO", Expr<Target>());
        var message = Seq("MESSAGE INTO", Expr<Target>());
        var includeProgram = Seq("INCLUDE PROGRAM FROM", Expr<Source>());
        var frame = Seq("FRAME PROGRAM FROM", Expr<Source>());
        var program = Seq("PROGRAM FROM", Expr<Source>());
        var from = Seq("FROM", Expr<Source>());
        var to = Seq("TO", Expr<Source>());
        var replacing = Seq("REPLACING", Expr<Source>());
        var id = Seq("ID", Expr<Source>(), "TABLE", Expr<Source>());

        var ret = Seq("SCAN ABAP-SOURCE",
                        Expr<Source>(),
                        Per(tokens,
                            levels,
                            from,
                            to,
                            statements,
                            structures,
                            keywords,
                            word,
                            line,
                            offset,
                            overflow,
                            message,
                            includeProgram,
                            include,
                            frame,
                            enhO,
                            enh,
                            program,
                            replacing,
                            "WITH ANALYSIS",
                            "WITH COMMENTS",
                            "WITH TYPE-POOLS",
                            "WITH INCLUDES",
                            "WITHOUT TRMAC",
                            "WITH DECLARATIONS",
                            "WITH BLOCKS",
                            "PRESERVING IDENTIFIER ESCAPING",
                            "WITH LIST TOKENIZATION",
                            "WITH EXPLICIT ENHANCEMENTS",
                            "WITH IMPLICIT ENHANCEMENTS",
                            "WITH INACTIVE ENHANCEMENTS",
                            pragmas,
                            id));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
