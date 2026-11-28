// Ported from abaplint packages/core/src/abap/5_syntax/expressions/method_parameters.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class IListItemT
{
    public string Name { get; set; }
    public ExpressionNode Target { get; set; }
    public AbstractType? TargetType { get; set; }
}

public class IListItemS
{
    public string Name { get; set; }
    public ExpressionNode Source { get; set; }
    public AbstractType? SourceType { get; set; }
}

public class MethodParameters
{

    internal static string NormalizeParameterName(string name)
    {
        return JsUtils.ToUpper(name.StartsWith('!') ? name.Substring(1) : name); // TS replace(/^!/, "")
    }

    private HashSet<string>? requiredParameters = null;

    public void RunSyntax(INode node, SyntaxInput input, object method)
    {
        if (!((node.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodParameters)))
        {
            throw new AssertError("MethodParameters, unexpected input");
        }

        var children = node.GetChildren().ToList();
        if (method is VoidType)
        {
            this.requiredParameters = new HashSet<string>();
        }
        else
        {
            this.requiredParameters = new HashSet<string>(((IMethodDefinition)method).GetParameters().GetRequiredParameters().Select(i => JsUtils.ToUpper(i.GetName())).ToList());
        }

        while (children.Count > 0)
        {
            var name = JsUtils.ToUpper(JsUtils.Shift(children)?.GetFirstToken().GetStr());
            switch (name)
            {
                case "EXPORTING":
                    this.CheckExporting(JsUtils.Shift(children)!, input, method, false);
                    break;
                case "IMPORTING":
                    this.CheckImporting(JsUtils.Shift(children)!, input, method);
                    break;
                case "CHANGING":
                    this.CheckChanging(JsUtils.Shift(children)!, input, method);
                    break;
                case "RECEIVING":
                    this.CheckReceiving(JsUtils.Shift(children)!, input, method);
                    break;
                case "EXCEPTIONS":
                    {
                        // todo, old style exceptions
                        var node2 = ((ExpressionNode)(JsUtils.Shift(children)!));
                        var exceptions = node2.FindFirstExpression<ParameterException>();
                        foreach (var s in exceptions?.FindAllExpressions<SimpleFieldChain>() ?? new List<ExpressionNode>())
                        {
                            FieldChain.RunSyntax(s, input, ReferenceType.DataReadReference);
                        }
                    }
                    break;
                default:
                    throw new AssertError("MethodParameters, unexpected token, " + JsUtils.Str(name));
            }
        }

        this.ReportErrors(node, input);
    }

    ///////////////////////
    private void CheckReceiving(INode node, SyntaxInput input, object method)
    {

        AbstractType? type = method is VoidType ? ((VoidType)method) : ((IMethodDefinition)method).GetParameters().GetReturning()?.GetType();
        if (type == null)
        {
            var message = "Method does not have a returning parameter";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }
        else if (!((node is ExpressionNode)))
        {
            var message = "checkReceiving, not an expression node";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var target = ((ExpressionNode)node).FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        var inline = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
        if (inline != null)
        {
            InlineData.RunSyntax(inline, input, type);
        }
        else if (target != null)
        {
            var targetType = Target.RunSyntax(target, input);
            if (targetType != null && new TypeUtils(input.Scope).IsAssignable(type, targetType) == false)
            {
                var message = "Method returning value not type compatible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, ((ExpressionNode)node).GetFirstToken(), message));
                return;
            }
        }
    }

    private void CheckImporting(INode node, SyntaxInput input, object method)
    {
        foreach (var item in this.ParameterListT(node, input))
        {
            AbstractType? parameterType = null;
            if (method is VoidType)
            {
                parameterType = ((VoidType)method);
            }
            else
            {
                var parameter = ((IMethodDefinition)method).GetParameters().GetExporting().FirstOrDefault(p => JsUtils.ToUpper(p.GetName()) == item.Name);
                if (parameter == null)
                {
                    var message = "Method exporting parameter \"" + item.Name + "\" does not exist";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
                parameterType = parameter.GetType();
            }

            var inline = item.Target.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                InlineData.RunSyntax(inline, input, parameterType);
            }
            else if (item.TargetType == null)
            {
                var message = "Could not determine target type";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            else if (item.TargetType != null && new TypeUtils(input.Scope).IsAssignableStrict(parameterType, item.TargetType) == false)
            {
                var message = "Method parameter type not compatible, " + item.Name;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }
    }

    private void CheckChanging(INode node, SyntaxInput input, object method)
    {
        foreach (var item in this.ParameterListT(node, input))
        {
            if (item.Target.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>() != null)
            {
                var message = "CHANGING cannot be inlined";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }

            AbstractType? parameterType = null;
            if (method is VoidType)
            {
                parameterType = ((VoidType)method);
            }
            else
            {
                var parameter = ((IMethodDefinition)method).GetParameters().GetChanging().FirstOrDefault(p => JsUtils.ToUpper(p.GetName()) == item.Name);
                if (parameter == null)
                {
                    var message = "Method changing parameter \"" + item.Name + "\" does not exist";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
                parameterType = parameter.GetType();
            }

            if (item.TargetType != null && new TypeUtils(input.Scope).IsAssignableStrict(parameterType, item.TargetType) == false)
            {
                var message = "Method parameter type not compatible, " + item.Name;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }

            this.requiredParameters?.Remove(item.Name);
        }
    }

    public void CheckExporting(INode node, SyntaxInput input, object method, bool errors = true)
    {

        var items = this.ParameterListS(node, input, method);
        if (method is VoidType)
        {
            return;
        }

        var allImporting = ((IMethodDefinition)method).GetParameters().GetImporting();
        if (this.requiredParameters == null)
        {
            this.requiredParameters = new HashSet<string>(((IMethodDefinition)method).GetParameters().GetRequiredParameters().Select(i => JsUtils.ToUpper(i.GetName())).ToList());
        }

        foreach (var item in items)
        {
            var parameter = allImporting.FirstOrDefault(p => JsUtils.ToUpper(p.GetName()) == item.Name);
            if (parameter == null)
            {
                var message = "Method importing parameter \"" + item.Name + "\" does not exist";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                continue;
            }
            else if (CheckTextSymbolHelper.CheckTextSymbol(item.Source, parameter.GetType(), input))
            {
                return;
            }
            else if (new TypeUtils(input.Scope).IsAssignableStrict(item.SourceType, parameter.GetType(), item.Source) == false)
            {
                var message = "Method parameter type not compatible, " + item.Name;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            this.requiredParameters.Remove(item.Name);
        }

        if (errors == true)
        {
            this.ReportErrors(node, input);
        }
    }

    private void ReportErrors(INode node, SyntaxInput input)
    {
        foreach (var r in this.requiredParameters ?? new HashSet<string>())
        {
            var message = $"method parameter \"{r}\" must be supplied";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
        }
    }

    private List<IListItemS> ParameterListS(INode? node, SyntaxInput input, object method)
    {

        if (node == null)
        {
            return new List<IListItemS>();
        }
        else if (!((node.Get() is ParameterListS)))
        {
            throw new AssertError("parameterListS, unexpected node");
        }

        List<IListItemS> ret = new List<IListItemS>();
        var supplied = new HashSet<string>();

        foreach (var c in node.GetChildren())
        {
            if (!((c.Get() is ParameterS)) || !((c is ExpressionNode)))
            {
                throw new AssertError("parameterListS, unexpected node, child");
            }

            var nameNode = ((ExpressionNode)c).FindDirectExpression<ParameterName>();
            string? name = nameNode == null ? (string?)null : NormalizeParameterName(nameNode.GetFirstToken().GetStr());
            if (name == null || nameNode == null)
            {
                throw new AssertError("parameterListS, no name determined");
            }
            if (supplied.Contains(name))
            {
                var message = $"Method parameter \"{name}\" is supplied more than once";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameNode.GetFirstToken(), message));
            }
            supplied.Add(name);

            var source = ((ExpressionNode)c).FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
            if (source == null)
            {
                throw new AssertError("parameterListS, no source found");
            }

            AbstractType? targetType = null;
            if (!((method is VoidType)))
            {
                foreach (var i in ((IMethodDefinition)method).GetParameters().GetImporting())
                {
                    if (JsUtils.ToUpper(i.GetName()) == name)
                    {
                        targetType = i.GetType();
                        break;
                    }
                }
            }
            else
            {
                targetType = ((VoidType)method);
            }

            AbstractType? sourceType = Source.RunSyntax(source, input, targetType);

            CheckOffsetLengthHelper.CheckOffsetLength(source, sourceType, method, input);

            if (sourceType == null)
            {
                if (method is VoidType)
                {
                    sourceType = ((VoidType)method);
                }
                else
                {
                    var message = "No source type determined for parameter " + name + " input";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    sourceType = VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }

            ret.Add(new IListItemS { Name = name, Source = source, SourceType = sourceType });
        }

        return ret;
    }

    private List<IListItemT> ParameterListT(INode? node, SyntaxInput input)
    {

        if (node == null)
        {
            return new List<IListItemT>();
        }
        else if (!((node.Get() is ParameterListT)))
        {
            throw new AssertError("parameterListT, unexpected node");
        }

        List<IListItemT> ret = new List<IListItemT>();
        var supplied = new HashSet<string>();

        foreach (var c in node.GetChildren())
        {
            if (!((c.Get() is ParameterT)) || !((c is ExpressionNode)))
            {
                throw new AssertError("parameterListT, unexpected node, child");
            }

            var nameNode = ((ExpressionNode)c).FindDirectExpression<ParameterName>();
            string? name = nameNode == null ? (string?)null : NormalizeParameterName(nameNode.GetFirstToken().GetStr());
            if (name == null || nameNode == null)
            {
                throw new AssertError("parameterListT, no name determined");
            }
            if (supplied.Contains(name))
            {
                var message = $"Method parameter \"{name}\" is supplied more than once";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameNode.GetFirstToken(), message));
            }
            supplied.Add(name);

            var target = ((ExpressionNode)c).FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
            if (target == null)
            {
                throw new AssertError("parameterListT, no target found");
            }

            var targetType = Target.RunSyntax(target, input);

            ret.Add(new IListItemT { Name = name, Target = target, TargetType = targetType });
        }

        return ret;
    }
}
