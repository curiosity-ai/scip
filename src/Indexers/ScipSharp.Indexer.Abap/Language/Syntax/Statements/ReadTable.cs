// Ported from abaplint packages/core/src/abap/5_syntax/statements/read_table.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class ReadTable : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var concat = JsUtils.ToUpper(node.ConcatTokens());
        var sources = node.FindDirectExpressionsMulti(typeof(global::ScipSharp.Indexer.Abap.Statements.Expressions.Source), typeof(SimpleSource2));

        var firstSource = sources.ElementAtOrDefault(0);
        AbstractType? sourceType = firstSource != null ? global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(firstSource, input) : (AbstractType?)null;

        if (sourceType == null)
        {
            var message = "No source type determined, read table";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }
        else if (sourceType is UnknownType)
        {
            // do nothing, ok
        }
        else if (!((sourceType is TableType)) && !((sourceType is VoidType)))
        {
            var message = "Read table, not a table type";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        AbstractType rowType = sourceType;
        if (rowType is TableType)
        {
            rowType = ((TableType)rowType).GetRowType();
        }

        var components = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCompareSimple>();
        if (components != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.ComponentCompareSimple.RunSyntax(components, input, rowType);
            var keyMessage = this.CheckKeyOperands(components, input);
            if (keyMessage != null)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), keyMessage));
                return;
            }
        }

        var indexSource = node.FindExpressionAfterToken("INDEX");
        if (indexSource != null)
        {
            var indexType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(indexSource, input);
            if (new TypeUtils(input.Scope).IsAssignable(indexType, IntegerType.Get()) == false)
            {
                var message = "READ TABLE, INDEX must be simple, got " + JsUtils.Str(indexType?.GetType().Name);
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            else if (sourceType is TableType && ((TableType)sourceType).GetAccessType() == TableAccessType.hashed)
            {
                var message = "INDEX on hashed table not possible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        var fromSource = node.FindExpressionAfterToken("FROM");
        if (fromSource != null)
        {
            var fromType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(fromSource, input);
            if (new TypeUtils(input.Scope).IsAssignable(fromType, rowType) == false)
            {
                var message = "READ TABLE, FROM must be compatible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, fromSource.GetFirstToken(), message));
                return;
            }
        }

        var afterKey = node.FindExpressionAfterToken("KEY");
        foreach (var s in sources)
        {
            if (s == firstSource || s == indexSource || s == fromSource)
            {
                continue;
            }
            var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
            if (s == afterKey)
            {
                if (type is StringType || (type is TableType && ((TableType)type).IsWithHeader() == false) || type is ObjectReferenceType)
                {
                    var message = "Key cannot be string or table or reference";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, s.GetFirstToken(), message));
                    return;
                }
            }
        }

        var target = node.FindDirectExpression<ReadTableTarget>();
        if (target != null)
        {
            if (concat.Contains(" REFERENCE INTO ", StringComparison.Ordinal))
            {
                rowType = new DataReference(rowType);
            }

            var inline = target.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            var fst = target.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FSTarget>();
            var t = target.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, rowType);
            }
            else if (fst != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.FSTarget.RunSyntax(fst, input, rowType);
            }
            else if (t != null)
            {
                var targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
                if (new TypeUtils(input.Scope).IsAssignable(rowType, targetType) == false)
                {
                    var message = "Incompatible types";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
        }

        if (target == null && concat.Contains(" TRANSPORTING NO FIELDS ", StringComparison.Ordinal) == false)
        {
            // if sourceType is void, assume its with header
            if (sourceType is TableType && ((TableType)sourceType).IsWithHeader() == false)
            {
                var message = "READ TABLE, define INTO or TRANSPORTING NO FIELDS";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        var transporting = node.FindDirectExpression<TransportingFields>();
        if (transporting != null && !((rowType is VoidType)) && !((rowType is UnknownType)) && !((rowType is AnyType)))
        {
            if (!((rowType is StructureType)))
            {
                var message = "READ TABLE, source not structured";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            foreach (var t in transporting?.FindDirectExpressions<FieldSub>() ?? new List<ExpressionNode>())
            {
                var field = t.ConcatTokens();
                if (field.Contains("-", StringComparison.Ordinal))
                {
                    // todo
                    continue;
                }
                if (((StructureType)rowType).GetComponentByName(field) == null)
                {
                    var message = "READ TABLE, field " + field + " not found in source";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
        }
    }

    // before 7.40 a WITH [TABLE] KEY operand is not an expression position, a built-in function from 7.02 on
    // is read as a functional method call there, "method TO_UPPER is unknown" on a system, while
    // functional methods and the older built-in functions like lines( ) are accepted
    private string? CheckKeyOperands(ExpressionNode node, SyntaxInput input)
    {
        if (input.Scope.GetOpenABAP() || Versions.ReleaseAtLeast(input.Scope.GetRelease(), Release.v740sp02))
        {
            return null;
        }
        foreach (var operand in node.FindDirectExpressionsMulti(typeof(global::ScipSharp.Indexer.Abap.Statements.Expressions.Source), typeof(SimpleSource4)))
        {
            var chain = operand.GetChildren();
            if (chain.Count != 1 || !((chain.ElementAtOrDefault(0) is ExpressionNode)) || !((chain.ElementAtOrDefault(0).Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodCallChain)))
            {
                continue;
            }
            var calls = chain.ElementAtOrDefault(0).GetChildren();
            if (calls.Count != 1 || !((calls.ElementAtOrDefault(0) is ExpressionNode)) || !((calls.ElementAtOrDefault(0).Get() is MethodCall)))
            {
                continue;
            }
            var name = calls.ElementAtOrDefault(0).FindDirectExpression<MethodName>()?.ConcatTokens();
            if (name == null || BuiltIn.methods.GetValueOrDefault(JsUtils.ToUpper(name))?.Release == null)
            {
                continue;
            }
            // a method of the same name hides the built-in function
            var enclosing = input.Scope.FindClassDefinition(input.Scope.GetEnclosingClassName());
            if (enclosing != null && new ObjectOriented(input.Scope).SearchMethodName(((IInterfaceDefinition?)enclosing), name).method != null)
            {
                continue;
            }
            return $"Built-in function \"{name}\" cannot be a READ TABLE key operand before 7.40";
        }
        return null;
    }
}
