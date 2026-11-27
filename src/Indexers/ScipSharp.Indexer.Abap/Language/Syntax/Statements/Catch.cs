// Ported from abaplint packages/core/src/abap/5_syntax/statements/catch.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Catch : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        var inlineTarget = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>()?.FindFirstExpression<TargetField>()?.GetFirstToken();
        if (inlineTarget != null && inlineTarget.GetStr().Length > 30)
        {
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, inlineTarget, "Inline exception name longer than 30 characters"));
        }

        var names = new HashSet<string>();
        foreach (var c in node.FindDirectExpressions<ClassName>())
        {
            var token = c.GetFirstToken();
            var className = JsUtils.ToUpper(token.GetStr());
            var found = input.Scope.ExistsObject(className);
            if ((found?.id) != null)
            {
                input.Scope.AddReference(token, found.Value.id, ReferenceType.ObjectOrientedReference, input.Filename);
            }
            else if (input.Scope.GetDDIC().InErrorNamespace(className) == false)
            {
                IReferenceExtras extra = new IReferenceExtras { OoName = className, OoType = "Void" };
                input.Scope.AddReference(token, null, ReferenceType.ObjectOrientedVoidReference, input.Filename, extra);
            }
            else
            {
                var message = "CATCH, unknown class " + className;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, token, message));
            }

            if (names.Contains(className))
            {
                var message = "Duplicate class name in CATCH: " + className;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            names.Add(className);
        }

        if ((target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>()) != null)
        {
            var token = target.FindFirstExpression<TargetField>()?.GetFirstToken();
            if (token != null)
            {
                var classNames = names.ToList();
                var unknownClass = classNames.FirstOrDefault(name => input.Scope.FindClassDefinition(name) == null);
                IClassDefinition? commonSuperclass = unknownClass == null ? this.FindCommonSuperclass(classNames, input) : (IClassDefinition?)null;
                AbstractType type = commonSuperclass != null ? new ObjectReferenceType(((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)commonSuperclass)) : VoidType.Get(JsUtils.Or(unknownClass, string.Join(" ", classNames)));
                var identifier = new TypedIdentifier(token, input.Filename, type, new List<string> { IdentifierMeta.InlineDefinition });
                input.Scope.AddIdentifier(identifier);
                input.Scope.AddReference(token, identifier, ReferenceType.DataWriteReference, input.Filename);
            }
        }
        else if (target != null)
        {
            var targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
            if (targetType is ObjectReferenceType)
            {
                foreach (var c in node.FindDirectExpressions<ClassName>())
                {
                    var token = c.GetFirstToken();
                    var className = JsUtils.ToUpper(token.GetStr());
                    var found = input.Scope.ExistsObject(className);
                    if ((found?.id) != null)
                    {
                        var catchType = new ObjectReferenceType(found.Value.id);
                        if (new TypeUtils(input.Scope).IsAssignableStrict(catchType, ((ObjectReferenceType)targetType)) == false)
                        {
                            var message = "CATCH target not compatible with " + className;
                            input.Issues.Add(SyntaxInput.SyntaxIssue(input, token, message));
                            return;
                        }
                    }
                }
            }
        }
    }

    private IClassDefinition? FindCommonSuperclass(List<string> classNames, SyntaxInput input)
    {
        List<List<IClassDefinition>> lineages = new List<List<IClassDefinition>>();
        foreach (var className in classNames)
        {
            List<IClassDefinition> lineage = new List<IClassDefinition>();
            IClassDefinition? current = input.Scope.FindClassDefinition(className);
            var visited = new HashSet<string>();
            while (current != null && visited.Contains(JsUtils.ToUpper(current.GetName())) == false)
            {
                lineage.Add(current);
                visited.Add(JsUtils.ToUpper(current.GetName()));
                current = input.Scope.FindClassDefinition(current.GetSuperClass());
            }
            lineages.Add(lineage);
        }

        return lineages.ElementAtOrDefault(0)?.FirstOrDefault(candidate => lineages.All(lineage => lineage.Any(item => JsUtils.ToUpper(item.GetName()) == JsUtils.ToUpper(candidate.GetName()))));
    }
}
