// Ported from abaplint packages/core/src/abap/nodes/statement_node.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Statements;


namespace ScipSharp.Indexer.Abap.Nodes;

using Comment = ScipSharp.Indexer.Abap.Lexer.Tokens.Comment;
using Type = System.Type;

public class StatementNode : AbstractNode<IExpressionOrTokenNode>, IStructureOrStatementNode
{
    private static readonly AbstractToken[] EMPTY_PRAGMAS = [];

    private readonly IStatement statement;
    private readonly AbstractToken? colon;
    private readonly IReadOnlyList<AbstractToken> pragmas;

    public StatementNode(IStatement statement, AbstractToken? colon = null, IReadOnlyList<AbstractToken>? pragmas = null)
    {
        this.statement = statement;
        this.colon = colon;
        this.pragmas = pragmas ?? EMPTY_PRAGMAS;
    }

    public override IStatement Get() => statement;

    public AbstractToken? GetColon() => colon;

    public IReadOnlyList<AbstractToken> GetPragmas() => pragmas;

    public StatementNode SetChildren(IEnumerable<IExpressionOrTokenNode> children)
    {
        var list = new List<IExpressionOrTokenNode>(children);
        if (list.Count == 0)
        {
            throw new InvalidOperationException("statement: zero children");
        }

        SetChildrenInternal(list);

        return this;
    }

    public Position GetStart() => GetFirstToken().GetStart();

    public Position GetEnd() => GetLastToken().GetEnd();

    public List<AbstractToken> GetTokens()
    {
        var tokens = new List<AbstractToken>();
        foreach (var c in GetChildren())
        {
            ToTokens(c, tokens);
        }
        return tokens;
    }

    public bool IncludesToken(AbstractToken search)
    {
        foreach (var t in GetTokens())
        {
            if (t.GetStart().Equals(search.GetStart()))
            {
                return true;
            }
        }
        return false;
    }

    public List<TokenNode> GetTokenNodes()
    {
        var tokens = new List<TokenNode>();
        foreach (var c in GetChildren())
        {
            ToTokenNodes(c, tokens);
        }
        return tokens;
    }

    public string ConcatTokens()
    {
        var str = "";
        AbstractToken? prev = null;
        foreach (var token in GetTokens())
        {
            if (token is Pragma)
            {
                continue;
            }
            if (str == "")
            {
                str = token.GetStr();
            }
            else if (prev != null && prev.GetStr().Length + prev.GetCol() == token.GetCol()
                && prev.GetRow() == token.GetRow())
            {
                str = str + token.GetStr();
            }
            else
            {
                str = str + " " + token.GetStr();
            }
            prev = token;
        }
        return str;
    }

    public string ConcatTokensVirtual()
    {
        var str = "";
        AbstractToken? prev = null;
        foreach (var token in GetTokens())
        {
            if (token is Pragma)
            {
                continue;
            }
            var vprev = prev?.GetStart() as VirtualPosition;
            var vtoke = token.GetStart() as VirtualPosition;
            if (str == "")
            {
                str = token.GetStr();
            }
            else if (prev != null && vprev != null && vtoke != null
                && prev.GetStr().Length + vprev.vcol == vtoke.vcol
                && vprev.vrow == vtoke.vrow)
            {
                str = str + token.GetStr();
            }
            else
            {
                str = str + " " + token.GetStr();
            }
            prev = token;
        }
        return str;
    }

    public string ConcatTokensWithoutStringsAndComments()
    {
        var str = "";
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
            if (str == "")
            {
                str = token.GetStr();
            }
            else if (prev != null && prev.GetStr().Length + prev.GetCol() == token.GetCol()
                && prev.GetRow() == token.GetRow())
            {
                str = str + token.GetStr();
            }
            else
            {
                str = str + " " + token.GetStr();
            }
            prev = token;
        }
        return str;
    }

    public string GetTerminator() => GetLastToken().GetStr();

    public override AbstractToken GetFirstToken()
    {
        foreach (var child in GetChildren())
        {
            return child.GetFirstToken();
        }
        throw new InvalidOperationException("StatementNode, getFirstToken, no children, " + Get().GetType().Name);
    }

    public override AbstractToken GetLastToken()
    {
        var child = GetLastChild();

        if (child != null)
        {
            return child.GetLastToken();
        }

        throw new InvalidOperationException("StatementNode, getLastToken, no children");
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
            if (child is ExpressionNode en && type.Any(t => NodeTypeCheck.Is(en.Get(), t)))
            {
                ret.Add(en);
            }
        }
        return ret;
    }

    public AbstractToken? FindDirectTokenByText(string text)
    {
        var upper = JsString.ToUpper(text);
        foreach (var child in GetChildren())
        {
            if (child is TokenNode tn && JsString.ToUpper(tn.Get().GetStr()) == upper)
            {
                return tn.Get();
            }
        }
        return null;
    }

    public ExpressionNode? FindFirstExpression<T>() where T : IStatementRunnable => FindFirstExpression(typeof(T));

    public ExpressionNode? FindFirstExpression(Type type)
    {
        foreach (var child in GetChildren())
        {
            if (child is not ExpressionNode en)
            {
                continue;
            }
            else if (NodeTypeCheck.Is(en.Get(), type))
            {
                return en;
            }
            else
            {
                var res = en.FindFirstExpression(type);
                if (res != null)
                {
                    return res;
                }
            }
        }
        return null;
    }

    public List<ExpressionNode> FindAllExpressions<T>() where T : IStatementRunnable => FindAllExpressions(typeof(T));

    public List<ExpressionNode> FindAllExpressions(Type type)
    {
        var ret = new List<ExpressionNode>();
        foreach (var child in GetChildren())
        {
            if (child is not ExpressionNode en)
            {
                continue;
            }
            else if (NodeTypeCheck.Is(en.Get(), type))
            {
                ret.Add(en);
            }
            else
            {
                ret.AddRange(en.FindAllExpressions(type));
            }
        }
        return ret;
    }

    public List<ExpressionNode> FindAllExpressionsRecursive<T>() where T : IStatementRunnable => FindAllExpressionsRecursive(typeof(T));

    public List<ExpressionNode> FindAllExpressionsRecursive(Type type)
    {
        var ret = new List<ExpressionNode>();
        foreach (var child in GetChildren())
        {
            if (child is not ExpressionNode en)
            {
                continue;
            }
            else if (NodeTypeCheck.Is(en.Get(), type))
            {
                ret.Add(en);
            }
            ret.AddRange(en.FindAllExpressionsRecursive(type));
        }
        return ret;
    }

    public List<ExpressionNode> FindAllExpressionsMulti(Type[] type, bool recursive = false)
    {
        var ret = new List<ExpressionNode>();
        foreach (var child in GetChildren())
        {
            if (child is not ExpressionNode en)
            {
                continue;
            }
            var before = ret.Count;
            foreach (var t in type)
            {
                if (NodeTypeCheck.Is(en.Get(), t))
                {
                    ret.Add(en);
                }
            }
            if (before == ret.Count || recursive)
            {
                ret.AddRange(en.FindAllExpressionsMulti(type, recursive));
            }
        }
        return ret;
    }

    /// <summary>
    /// Returns the Position of the first token if the sequence is found, otherwise null.
    /// Strings and Comments are ignored in this search.
    /// </summary>
    public Position? FindTokenSequencePosition(string first, string second)
    {
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
            if (prev != null && JsString.ToUpper(token.GetStr()) == second && JsString.ToUpper(prev.GetStr()) == JsString.ToUpper(first))
            {
                return prev.GetStart();
            }
            else
            {
                prev = token;
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

    public List<ExpressionNode> FindExpressionsAfterToken(string text)
    {
        var children = GetChildren();
        var ret = new List<ExpressionNode>();
        var upper = JsString.ToUpper(text);

        for (var i = 0; i < children.Count - 1; i++)
        {
            var c = children[i];
            var next = children[i + 1];
            if (c is TokenNode tn
                && JsString.ToUpper(tn.Get().GetStr()) == upper
                && next is ExpressionNode en)
            {
                ret.Add(en);
            }
        }

        return ret;
    }

    ////////////////////////////////

    private static void ToTokens(INode b, List<AbstractToken> tokens)
    {
        if (b is TokenNode tn)
        {
            tokens.Add(tn.Get());
            return;
        }

        foreach (var c in b.GetChildren())
        {
            if (c is TokenNode ctn)
            {
                tokens.Add(ctn.Get());
            }
            else
            {
                ToTokens(c, tokens);
            }
        }
    }

    private static void ToTokenNodes(INode b, List<TokenNode> tokens)
    {
        if (b is TokenNode tn)
        {
            tokens.Add(tn);
            return;
        }

        foreach (var c in b.GetChildren())
        {
            if (c is TokenNode ctn)
            {
                tokens.Add(ctn);
            }
            else
            {
                ToTokenNodes(c, tokens);
            }
        }
    }

    public override string ToString() => "StatementNode " + statement.GetType().Name + " " + ConcatTokens();
}
