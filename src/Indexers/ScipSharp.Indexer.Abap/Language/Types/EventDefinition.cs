// Ported from abaplint packages/core/src/abap/types/event_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Syntax;

namespace ScipSharp.Indexer.Abap.Types;

using AbstractToken = ScipSharp.Indexer.Abap.Lexer.Tokens.AbstractToken;

public class EventDefinition : Identifier, IEventDefinition
{
    private readonly List<TypedIdentifier> parameters;
    private readonly bool is_static;

    public EventDefinition(StatementNode node, Visibility _visibility, SyntaxInput input)
        : base(NameToken(node), input.Filename)
    {

        this.parameters = new List<TypedIdentifier>();
        this.Parse(node, input);

        this.is_static = JsUtils.ToUpper(node.GetFirstToken().GetStr()) == "CLASS";
    }

    // TS checks the node and finds the name before calling super()
    private static AbstractToken NameToken(StatementNode node)
    {
        if (!((node.Get() is Events)))
        {
            throw new Exception("MethodDefinition, expected MethodDef as part of input node");
        }
        var found = node.FindFirstExpression<EventName>();
        if (found == null)
        {
            throw new Exception("MethodDefinition, expected MethodDef as part of input node");
        }
        return found.GetFirstToken();
    }

    public List<TypedIdentifier> GetParameters()
    {
        return this.parameters;
    }

    public bool IsStatic()
    {
        return this.is_static;
    }

    ///////////////
    private void Parse(StatementNode node, SyntaxInput input)
    {
        foreach (var e in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodParam>())
        {
            this.parameters.Add(global::ScipSharp.Indexer.Abap.Syntax.Expressions.MethodParam.RunSyntax(e, input, new List<string>()));
        }
    }
}
