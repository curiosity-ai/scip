// Ported from abaplint packages/core/src/abap/5_syntax/expressions/type_table.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class TypeTable
{
    public static TypedIdentifier? RunSyntax(INode node, SyntaxInput input, string? qualifiedNamePrefix = null)
    {
        // todo, input is currently the statement, but should be the expression?
        ExpressionNode? nameExpr = node.FindFirstExpression<DefinitionName>();
        if (nameExpr == null)
        {
            nameExpr = node.FindFirstExpression<NamespaceSimpleName>();
        }
        if (nameExpr == null)
        {
            return null;
        }
        var name = nameExpr.GetFirstToken();

        string qualifiedName = JsUtils.Or(qualifiedNamePrefix, "");
        if (JsUtils.ToUpper(node.GetFirstToken().GetStr()) == "TYPES")
        {
            qualifiedName = qualifiedName + name.GetStr();
            if (input.Scope.GetType() == ScopeType.ClassDefinition || input.Scope.GetType() == ScopeType.Interface)
            {
                qualifiedName = input.Scope.GetName() + "=>" + qualifiedName;
            }
        }

        AbstractType? type = new BasicTypes(input).ParseTable(node, qualifiedName);
        if (type == null)
        {
            return new TypedIdentifier(name, input.Filename, new UnknownType("TableType, fallback"));
        }

        foreach (var tt in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.TypeTableKey>())
        {
            var error = TypeTableKey.RunSyntax(tt, type);
            if (error != null)
            {
                type = error;
            }
        }

        return new TypedIdentifier(name, input.Filename, type);
    }
}
