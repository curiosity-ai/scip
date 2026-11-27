// Ported from abaplint packages/core/src/abap/2_statements/expressions/database_table.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DatabaseTable : Expression
{
    private readonly bool noDynamic;

    public DatabaseTable() : this(false)
    {
    }

    public DatabaseTable(bool noDynamic)
    {
        this.noDynamic = noDynamic;
    }

    public override IStatementRunnable GetRunnable()
    {
        if (this.noDynamic)
        {
            return AltPrio(VerNotLang(LanguageVersion.KeyUser, Expr<Dynamic>()), Regex(@"^\*?(\/\w+\/)?\w+$"));
        }
        return AltPrio(Expr<Dynamic>(), Regex(@"^\*?(\/\w+\/)?\w+$"));
    }
}
