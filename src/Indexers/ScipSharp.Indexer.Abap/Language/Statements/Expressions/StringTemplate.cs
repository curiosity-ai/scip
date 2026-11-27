// Ported from abaplint packages/core/src/abap/2_statements/expressions/string_template.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class StringTemplate : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var nest = Seq(Tok<Tokens.StringTemplateBegin>(),
                         Expr<StringTemplateSource>(),
                         StarPrio(Seq(Tok<Tokens.StringTemplateMiddle>(), Expr<StringTemplateSource>())),
                         Tok<Tokens.StringTemplateEnd>());

        return Ver(Release.v702, AltPrio(nest, Tok<Tokens.StringTemplate>()));
    }
}
