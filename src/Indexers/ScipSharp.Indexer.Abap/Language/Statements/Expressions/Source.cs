// Ported from abaplint packages/core/src/abap/2_statements/expressions/source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// todo, COND and SWITCH are quite similar?
// this class is used quite often, so its nice to have the differentiating tokens part of it
public class Source : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var comp = Seq(Tok<Tokens.Dash>(), Expr<ComponentChain>());
        var attr = Seq(Expr<Arrow>(), Expr<AttributeChain>());
        var deref = OptPrio(Ver(Release.v756, Expr<Dereference>()));

        // below v756 "->*" directly after a functional method call is a syntax error, CAST can be dereferenced
        var dynChain = Star(Seq(AltPrio(DynamicAccess.DynAttr(), DynamicAccess.DynComp()), OptPrio(Expr<Dereference>())));
        var castDeref = Seq(Expr<Cast>(), Expr<Dereference>());

        var method = Seq(Expr<MethodCallChain>(), OptPrio(AltPrio(attr, comp)), deref, dynChain);

        var rparen = Tok<Tokens.WParenRightW>();
        var rparenNoSpace = AltPrio(Tok<Tokens.ParenRightW>(), Tok<Tokens.WParenRightW>());
        var lparenNoSpace = AltPrio(Tok<Tokens.ParenLeft>(), Tok<Tokens.ParenLeftW>());

        // paren used for eg. "( 2 + 1 ) * 4"
        var paren = Seq(Tok<Tokens.WParenLeftW>(),
                          Expr<Source>(),
                          rparen);

        var after = Seq(AltPrio("&", "&&", Expr<ArithOperator>()), Expr<Source>());

        var @bool = Seq(AltPrio(Ver(Release.v702, Regex(@"^BOOLC$", "i"), also: AlsoIn.OpenABAP),
                                 Ver(Release.v740sp08, Regex(@"^XSDBOOL$", "i"), also: AlsoIn.OpenABAP)),
                         Tok<Tokens.ParenLeftW>(),
                         Expr<Cond>(),
                         ")");

        var prefix = AltPrio(Tok<Tokens.WPlus>(), "BIT-NOT");

        var old = Seq(OptPrio(prefix), AltPrio(Expr<Constant>(),
                                                 Expr<StringTemplate>(),
                                                 Expr<TextElement>(),
                                                 @bool,
                                                 castDeref,
                                                 method,
                                                 Seq(Expr<FieldChain>(), deref),
                                                 paren),
                        OptPrio(after));

        var corr = Ver(Release.v740sp05, Seq("CORRESPONDING",
                                               Expr<TypeNameOrInfer>(),
                                               Tok<Tokens.ParenLeftW>(),
                                               Expr<CorrespondingBody>(),
                                               rparen,
                                               OptPrio(after)), also: AlsoIn.OpenABAP);

        var conv = Ver(Release.v740sp02, Seq("CONV",
                                               Expr<TypeNameOrInfer>(),
                                               lparenNoSpace,
                                               Expr<ConvBody>(),
                                               rparenNoSpace,
                                               OptPrio(after)), also: AlsoIn.OpenABAP);

        var swit = Ver(Release.v740sp02, Seq("SWITCH",
                                               Expr<TypeNameOrInfer>(),
                                               Tok<Tokens.ParenLeftW>(),
                                               Expr<SwitchBody>(),
                                               rparenNoSpace,
                                               OptPrio(after)), also: AlsoIn.OpenABAP);

        var value = Ver(Release.v740sp02, Seq("VALUE",
                                                Expr<TypeNameOrInfer>(),
                                                Tok<Tokens.ParenLeftW>(),
                                                Expr<ValueBody>(),
                                                rparenNoSpace,
                                                OptPrio(after)), also: AlsoIn.OpenABAP);

        var cond = Ver(Release.v740sp02, Seq("COND",
                                               Expr<TypeNameOrInfer>(),
                                               Tok<Tokens.ParenLeftW>(),
                                               Expr<CondBody>(),
                                               rparenNoSpace,
                                               OptPrio(after)), also: AlsoIn.OpenABAP);

        var reff = Ver(Release.v740sp02, Seq("REF",
                                               Expr<TypeNameOrInfer>(),
                                               Tok<Tokens.ParenLeftW>(),
                                               Expr<Source>(),
                                               OptPrio("OPTIONAL"),
                                               rparen), also: AlsoIn.OpenABAP);

        var exact = Ver(Release.v740sp02, Seq("EXACT",
                                                Expr<TypeNameOrInfer>(),
                                                Tok<Tokens.ParenLeftW>(),
                                                Expr<Source>(),
                                                rparen,
                                                OptPrio(after)), also: AlsoIn.OpenABAP);

        var filter = Ver(Release.v740sp08,
                           Seq("FILTER",
                               Expr<TypeNameOrInfer>(),
                               Tok<Tokens.ParenLeftW>(),
                               Expr<FilterBody>(),
                               rparen), also: AlsoIn.OpenABAP);

        var reduce = Ver(Release.v740sp08,
                           Seq("REDUCE",
                               Expr<TypeNameOrInfer>(),
                               Tok<Tokens.ParenLeftW>(),
                               Expr<ReduceBody>(),
                               rparen,
                               OptPrio(after)), also: AlsoIn.OpenABAP);

        var prefix1 = AltPrio(Tok<Tokens.WDashW>(), Tok<Tokens.WPlusW>());

        var ret = Seq(StarPrio(prefix1),
                        AltPrio(filter, reff, corr, conv, value, cond, exact, swit, reduce, old));

        return ret;
    }
}
