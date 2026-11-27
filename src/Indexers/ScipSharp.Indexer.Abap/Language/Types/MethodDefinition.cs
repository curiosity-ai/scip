// Ported from abaplint packages/core/src/abap/types/method_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Syntax;

namespace ScipSharp.Indexer.Abap.Types;

public class MethodDefinition : global::ScipSharp.Indexer.Abap.FileInformation.Identifier, IMethodDefinition
{
    private readonly Visibility visibility;
    private readonly MethodParameters parameters;
    private readonly bool redefinition;
    private readonly bool eventHandler;
    private readonly string? eventName;
    private readonly string? eventClass;
    private readonly bool @abstract;
    private readonly bool @static;
    private readonly List<string> raising;
    private readonly List<string> exceptions;
    private readonly string className;

    // todo: final flag
    // TS checks the node and finds the name before calling super()
    private static AbstractToken NameToken(StatementNode node)
    {
        if (!((node.Get() is MethodDef)))
        {
            throw new Exception("MethodDefinition, expected MethodDef as part of input node");
        }

        var found = node.FindDirectExpression<MethodName>();
        if (found == null)
        {
            throw new Exception("MethodDefinition, expected MethodDef as part of input node");
        }
        return found.GetFirstToken();
    }

    public MethodDefinition(StatementNode node, Visibility visibility, SyntaxInput input)
        : base(NameToken(node), input.Filename)
    {
        var found = node.FindDirectExpression<MethodName>()!;

        this.className = input.Scope.GetName();

        this.redefinition = false;
        if ((node.FindDirectExpression<Redefinition>()) != null)
        {
            this.redefinition = true;

            var name = found.GetFirstToken().GetStr();
            if (name.Contains("~", StringComparison.Ordinal))
            {
                var idef = input.Scope.FindInterfaceDefinition(name.Split("~").ToList().ElementAtOrDefault(0));
                if (idef != null)
                {
                    input.Scope.AddReference(found.GetFirstToken(), ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)idef), ReferenceType.ObjectOrientedReference, input.Filename);
                }
            }
        }

        this.eventHandler = false;
        var handler = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.EventHandler>();
        if (handler != null)
        {
            this.eventHandler = true;
            this.eventName = JsUtils.ToUpper(handler.FindDirectExpression<EventName>()?.ConcatTokens());
            this.eventClass = JsUtils.ToUpper(handler.FindDirectExpression<ClassName>()?.ConcatTokens());
        }

        this.@abstract = false;
        if ((node.FindDirectExpression<Abstract>()) != null)
        {
            this.@abstract = true;
        }

        this.@static = false;
        // checks for "CLASS-METHODS"
        if (JsUtils.ToUpper(node.GetFirstToken().GetStr()).StartsWith("CLASS", StringComparison.Ordinal))
        {
            this.@static = true;
        }

        this.raising = new List<string>();
        this.CheckRaisingParameter(node, input);
        foreach (var r in node.FindDirectExpression<MethodDefRaising>()?.FindAllExpressions<ClassName>() ?? new List<ExpressionNode>())
        {
            var token = r.GetFirstToken();
            var name = token.GetStr();
            this.raising.Add(name);
            var clas = input.Scope.FindClassDefinition(name);
            if (clas != null)
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)clas), ReferenceType.ObjectOrientedReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "CLAS" });
            }
            else if (input.Scope.GetDDIC().InErrorNamespace(name) == false)
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)clas), ReferenceType.ObjectOrientedVoidReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "CLAS" });
            }
            else
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)clas), ReferenceType.ObjectOrientedUnknownReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "CLAS" });
            }
        }

        this.exceptions = new List<string>();
        foreach (var r in node.FindDirectExpression<MethodDefExceptions>()?.FindAllExpressions<NamespaceSimpleName>() ?? new List<ExpressionNode>())
        {
            var token = r.GetFirstToken();
            var name = token.GetStr();
            this.exceptions.Add(name);
        }

        this.visibility = visibility;
        this.parameters = new MethodParameters(node, input, this.@abstract);
    }

    // A system reads a parameter named RAISING as the RAISING addition, measured on 758:
    // "IMPORTING a TYPE i raising TYPE i" fails with 'Type "TYPE" is unknown'
    private void CheckRaisingParameter(StatementNode node, SyntaxInput input)
    {
        var raising = node.FindDirectExpression<MethodDefRaising>();
        var first = raising?.GetChildren().ElementAtOrDefault(1);
        if (raising == null || first == null || !((first.Get() is ClassName)) || new List<string> { "TYPE", "LIKE" }.Contains(JsUtils.ToUpper(first.GetFirstToken().GetStr())) == false)
        {
            return;
        }
        if (node.FindDirectExpression<MethodDefImporting>() == null && node.FindDirectExpression<MethodDefExporting>() == null && node.FindDirectExpression<MethodDefChanging>() == null)
        {
            return;
        }
        var token = raising.GetFirstToken();
        var message = "Parameter \"" + token.GetStr() + "\" is read as the RAISING addition, rename it or escape it as !" + token.GetStr();
        input.Issues.Add(SyntaxInput.SyntaxIssue(input, token, message));
    }

    public Visibility GetVisibility()
    {
        return this.visibility;
    }

    public string GetClassName()
    {
        return this.className;
    }

    public bool IsRedefinition()
    {
        return this.redefinition;
    }

    public bool IsAbstract()
    {
        return this.@abstract;
    }

    public bool IsStatic()
    {
        return this.@static;
    }

    public bool IsEventHandler()
    {
        return this.eventHandler;
    }

    public string? GetEventName()
    {
        return this.eventName;
    }

    public string? GetEventClass()
    {
        return this.eventClass;
    }

    public MethodParameters GetParameters()
    {
        return this.parameters;
    }

    public List<string> GetRaising()
    {
        return this.raising;
    }

    public List<string> GetExceptions()
    {
        return this.exceptions;
    }

    IMethodParameters IMethodDefinition.GetParameters() => GetParameters();
}
