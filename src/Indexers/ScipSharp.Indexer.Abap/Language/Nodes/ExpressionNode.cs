// Ported from abaplint packages/core/src/abap/nodes/expression_node.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Statements;

namespace ScipSharp.Indexer.Abap.Nodes;

public class ExpressionNode : AbstractNode<IExpressionOrTokenNode>, IExpressionOrTokenNode
{
    private readonly IStatementRunnable expression;
    private int tokenCount = 0;

    public ExpressionNode(IStatementRunnable expression)
    {
        this.expression = expression;
    }

    public override void AddChild(IExpressionOrTokenNode n)
    {
        throw new InvalidOperationException("ExpressionNode: addChild() must not be called, nodes are immutable after setChildren()");
    }

    public override IStatementRunnable Get() => expression;

    public ExpressionNode SetChildren(IEnumerable<IExpressionOrTokenNode> children)
    {
        SetChildrenInternal(children);

        foreach (var child in GetChildren())
        {
            tokenCount = tokenCount + child.CountTokens();
        }

        return this;
    }

    public int CountTokens() => tokenCount;

    public override AbstractToken GetFirstToken() => NodeWalk.FirstToken(this, "ExpressionNode");

    public string ConcatTokensWithLinebreaks()
    {
        var str = new System.Text.StringBuilder();
        AbstractToken? prev = null;
        foreach (var token in GetTokens())
        {
            if (token is Pragma)
            {
                continue;
            }
            if (str.Length == 0)
            {
                str.Append(token.GetStr());
            }
            else if (prev != null && prev.GetStr().Length + prev.GetCol() == token.GetCol()
                && prev.GetRow() == token.GetRow())
            {
                str.Append(token.GetStr());
            }
            else
            {
                if (prev != null && prev.GetRow() != token.GetRow())
                {
                    str.Append('\n').Append(token.GetStr());
                }
                else
                {
                    str.Append(' ').Append(token.GetStr());
                }
            }
            prev = token;
        }
        return str.ToString();
    }

    public string ConcatTokens()
    {
        var str = new System.Text.StringBuilder();
        AbstractToken? prev = null;
        foreach (var token in GetTokens())
        {
            if (token is Pragma)
            {
                continue;
            }
            if (str.Length == 0)
            {
                str.Append(token.GetStr());
            }
            else if (prev != null && prev.GetStr().Length + prev.GetCol() == token.GetCol()
                && prev.GetRow() == token.GetRow())
            {
                str.Append(token.GetStr());
            }
            else
            {
                str.Append(' ').Append(token.GetStr());
            }
            prev = token;
        }
        return str.ToString();
    }

    public string ConcatTokensWithoutStringsAndComments()
    {
        var str = new System.Text.StringBuilder();
        AbstractToken? prev = null;
        foreach (var token in GetTokens())
        {
            if (token is Comment
                || token is StringToken
                || token is StringTemplate
                || token is StringTemplateBegin
                || token is StringTemplateMiddle
                || token is StringTemplateEnd)
            {
                continue;
            }
            if (str.Length == 0)
            {
                str.Append(token.GetStr());
            }
            else if (prev != null && prev.GetStr().Length + prev.GetCol() == token.GetCol()
                && prev.GetRow() == token.GetRow())
            {
                str.Append(token.GetStr());
            }
            else
            {
                str.Append(' ').Append(token.GetStr());
            }
            prev = token;
        }
        return str.ToString();
    }

    // todo: delete this method?, its slow
    public List<AbstractToken> GetTokens()
    {
        var tokens = new List<AbstractToken>();
        NodeWalk.Tokens(GetChildren(), tokens);
        return tokens;
    }

    public override AbstractToken GetLastToken() => NodeWalk.LastToken(this, "ExpressionNode");

    public List<AbstractToken> GetAllTokens()
    {
        var ret = new List<AbstractToken>();
        NodeWalk.Tokens(GetChildren(), ret);
        return ret;
    }

    public List<AbstractToken> GetDirectTokens()
    {
        var ret = new List<AbstractToken>();

        foreach (var child in GetChildren())
        {
            if (child is TokenNode tn)
            {
                ret.Add(tn.Get());
            }
        }

        return ret;
    }

    public ExpressionNode? FindDirectExpression<T>() where T : IStatementRunnable => FindDirectExpression(typeof(T));

    public ExpressionNode? FindDirectExpression(Type type)
    {
        foreach (var child in GetChildren())
        {
            if (child is ExpressionNode en && NodeTypeCheck.Is(en.Get(), type))
            {
                return en;
            }
        }
        return null;
    }

    public ExpressionNode? FindExpressionAfterToken(string text)
    {
        var children = GetChildren();
        var upper = JsString.ToUpper(text);

        for (var i = 0; i < children.Count - 1; i++)
        {
            var c = children[i];
            var next = children[i + 1];
            if (c is TokenNode tn
                && JsString.ToUpper(tn.Get().GetStr()) == upper
                && next is ExpressionNode en)
            {
                return en;
            }
        }

        return null;
    }

    public List<ExpressionNode> FindDirectExpressions<T>() where T : IStatementRunnable => FindDirectExpressions(typeof(T));

    public List<ExpressionNode> FindDirectExpressions(Type type)
    {
        var ret = new List<ExpressionNode>();
        foreach (var child in GetChildren())
        {
            if (child is ExpressionNode en && NodeTypeCheck.Is(en.Get(), type))
            {
                ret.Add(en);
            }
        }
        return ret;
    }

    public List<ExpressionNode> FindDirectExpressionsMulti(params Type[] type)
    {
        var ret = new List<ExpressionNode>();
        foreach (var child in GetChildren())
        {
            if (child is ExpressionNode en)
            {
                foreach (var t in type)
                {
                    if (NodeTypeCheck.Is(en.Get(), t))
                    {
                        ret.Add(en);
                        break;
                    }
                }
            }
        }
        return ret;
    }

    public AbstractToken? FindDirectTokenByText(string text)
    {
        var search = JsString.ToUpper(text);
        foreach (var child in GetChildren())
        {
            if (child is TokenNode tn && JsString.ToUpper(tn.Get().GetStr()) == search)
            {
                return tn.Get();
            }
        }
        return null;
    }

    public List<ExpressionNode> FindAllExpressionsRecursive<T>() where T : IStatementRunnable => FindAllExpressionsRecursive(typeof(T));

    public List<ExpressionNode> FindAllExpressionsRecursive(Type type)
    {
        var ret = new List<ExpressionNode>();
        NodeWalk.Expressions(GetChildren(), [type], static _ => true, ret);
        return ret;
    }

    public List<ExpressionNode> FindAllExpressions<T>() where T : IStatementRunnable => FindAllExpressions(typeof(T));

    public List<ExpressionNode> FindAllExpressions(Type type)
    {
        var ret = new List<ExpressionNode>();
        NodeWalk.Expressions(GetChildren(), [type], static matches => matches == 0, ret);
        return ret;
    }

    public List<ExpressionNode> FindAllExpressionsMulti(Type[] type, bool recursive = false)
    {
        var ret = new List<ExpressionNode>();
        NodeWalk.Expressions(GetChildren(), type, matches => matches == 0 || recursive, ret);
        return ret;
    }

    public ExpressionNode? FindFirstExpression<T>() where T : IStatementRunnable => FindFirstExpression(typeof(T));

    public ExpressionNode? FindFirstExpression(Type type)
    {
        if (NodeTypeCheck.Is(Get(), type))
        {
            return this;
        }

        return NodeWalk.FirstExpression(GetChildren(), type);
    }

    public override string ToString() => "ExpressionNode " + expression.GetType().Name + " " + ConcatTokens();
}
