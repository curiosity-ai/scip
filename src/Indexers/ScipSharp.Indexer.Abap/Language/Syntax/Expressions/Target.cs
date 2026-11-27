// Ported from abaplint packages/core/src/abap/5_syntax/expressions/target.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class Target
{
    public static AbstractType? RunSyntax(ExpressionNode node, SyntaxInput input)
    {

        var concat = node.ConcatTokens();
        if (concat.Contains("-", StringComparison.Ordinal))
        {
            // workaround for names with dashes
            var found = input.Scope.FindVariable(concat);
            if (found != null)
            {
                input.Scope.AddReference(node.GetFirstToken(), found, ReferenceType.DataWriteReference, input.Filename);
                return found.GetType();
            }
        }

        var children = node.GetChildren();
        var first = children.ElementAtOrDefault(0);
        if (first == null || !((first is ExpressionNode)))
        {
            return null;
        }

        AbstractType? context = Target.FindTop(((ExpressionNode)first), input);
        if (context == null)
        {
            var message = $"\"{((ExpressionNode)first).GetFirstToken().GetStr()}\" not found, Target";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        int currentIndex = 1;
        while (currentIndex <= children.Count)
        {
            var current = children.ElementAtOrDefault(currentIndex);
            if (current == null)
            {
                break;
            }
            currentIndex++;

            if (current.Get() is Dash)
            {
                if (context is UnknownType)
                {
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                else if (context is TableType && ((TableType)context).IsWithHeader() && (((TableType)context).GetRowType()) is UnknownType)
                {
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                else if (context is TableType && ((TableType)context).IsWithHeader() && (((TableType)context).GetRowType()) is VoidType)
                {
                    return ((TableType)context).GetRowType();
                }
                else if (!((context is StructureType)) && !((context is TableType && ((TableType)context).IsWithHeader() && (((TableType)context).GetRowType()) is StructureType)) && !((context is VoidType)))
                {
                    var message = "Not a structure, target, " + JsUtils.Str(context?.GetType().Name) + ", " + current.ConcatTokens();
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
            else if (current.Get() is InstanceArrow)
            {
                if (!((context is ObjectReferenceType)) && !((context is DataReference)) && !((context is VoidType)))
                {
                    var message = "Not an object reference, target";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
            else if (current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dereference)
            {
                if (!((context is DataReference)) && !((context is VoidType)))
                {
                    var message = "Not an object reference, target";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }

                if (!((context is VoidType)))
                {
                    context = ((DataReference)context).GetType();
                }
                if (context?.IsGeneric() == true && !(Versions.ReleaseAtLeast(input.Scope.GetRelease(), Release.v756)) && input.Scope.GetLanguageVersion() != LanguageVersion.Cloud && !input.Scope.GetOpenABAP())
                {
                    throw new Exception("A generic reference cannot be dereferenced");
                }
            }
            else if (current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentName)
            {
                context = ComponentName.RunSyntax(context, current, input);
            }
            else if (current.Get() is TableBody)
            {
                if (!((context is TableType)) && !((context is VoidType)) && !((context is UnknownType)) && !((context is UnknownType)))
                {
                    var message = $"Not an internal table, \"{node.ConcatTokens()}\"";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                if (context is TableType && ((TableType)context).IsWithHeader())
                {
                    context = new TableType(((TableType)context).GetRowType(), new ITableOptions { KeyType = ((TableType)context).GetOptions().KeyType, PrimaryKey = ((TableType)context).GetOptions().PrimaryKey, Secondary = ((TableType)context).GetOptions().Secondary, WithHeader = false });
                }
            }
            else if (current is ExpressionNode && (((ExpressionNode)current).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.TableExpression)
            {
                if (!((context is TableType)) && !((context is VoidType)) && !((context is UnknownType)))
                {
                    var message = "Table expression, expected table";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                TableExpression.RunSyntax(((ExpressionNode)current), input, context);
                if (!((context is VoidType)) && !((context is UnknownType)))
                {
                    context = ((TableType)context).GetRowType();
                }
            }
            else if (current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.AttributeName)
            {
                string type = children.Count == 0 ? ReferenceType.DataWriteReference : ReferenceType.DataReadReference;
                context = AttributeName.RunSyntax(context, current, input, type);
            }
        }

        // the substring is checked, but the type of the whole field is returned
        AbstractType? substring = context;
        var offset = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldOffset>();
        if (offset != null)
        {
            if (context is XStringType || context is StringType)
            {
                var message = "xstring/string offset/length in writer position not possible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            var value = FieldOffset.RunSyntax(offset, input);
            if (!(Substring.IsFieldSymbolItself(children.Cast<INode>().ToList(), JsUtils.IndexOf(children, offset))))
            {
                substring = Substring.SubstringType(substring, value, null, offset, input);
            }
        }

        var length = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldLength>();
        if (length != null)
        {
            if (context is XStringType || context is StringType)
            {
                var message = "xstring/string offset/length in writer position not possible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            var value = FieldLength.RunSyntax(length, input);
            if (!(Substring.IsFieldSymbolItself(children.Cast<INode>().ToList(), JsUtils.IndexOf(children, length))))
            {
                substring = Substring.SubstringType(substring, null, value, length, input);
            }
        }
        if (substring is VoidType && !((context is VoidType)))
        {
            return ((VoidType)substring);
        }

        return context;
    }

    /////////////////////////////////
    private static AbstractType? FindTop(INode? node, SyntaxInput input)
    {
        if (node == null)
        {
            return null;
        }

        var token = node.GetFirstToken();
        var name = token.GetStr();

        if (node.Get() is TargetField || node.Get() is TargetFieldSymbol)
        {
            var found = input.Scope.FindVariable(name);
            if (found != null)
            {
                input.Scope.AddReference(token, found, ReferenceType.DataWriteReference, input.Filename);
            }
            if (name.Contains("~", StringComparison.Ordinal))
            {
                var idef = input.Scope.FindInterfaceDefinition(name.Split("~").ToList().ElementAtOrDefault(0));
                if (idef != null)
                {
                    input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)idef), ReferenceType.ObjectOrientedReference, input.Filename);
                }
            }
            return found?.GetType();
        }
        else if (node.Get() is ClassName)
        {
            var found = input.Scope.FindObjectDefinition(name);
            if (found != null)
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)found), ReferenceType.ObjectOrientedReference, input.Filename);
                return new ObjectReferenceType(((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)found));
            }
            else if (input.Scope.GetDDIC().InErrorNamespace(name) == false)
            {
                input.Scope.AddReference(token, null, ReferenceType.ObjectOrientedVoidReference, input.Filename, new IReferenceExtras { OoName = name, OoType = "CLAS" });
                return VoidType.Get(name);
            }
            else
            {
                return new UnknownType(name + " unknown, Target");
            }
        }
        else if (node.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Cast && node is ExpressionNode)
        {
            var ret = Cast.RunSyntax(((ExpressionNode)node), input, null);
            if (ret is UnknownType)
            {
                var message = "CAST, uknown type";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, ((ExpressionNode)node).GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            return ret;
        }

        return new UnknownType("unknown target type");
    }
}
