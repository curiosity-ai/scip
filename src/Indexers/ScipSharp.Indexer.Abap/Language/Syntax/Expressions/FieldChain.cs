// Ported from abaplint packages/core/src/abap/5_syntax/expressions/field_chain.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class FieldChain
{

    public static AbstractType? RunSyntax(ExpressionNode node, SyntaxInput input, object? refType = null, bool allowGenericDeference = false)
    {

        if ((node.GetFirstChild()?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.SourceField && (node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentName>()) != null)
        {
            // workaround for names with dashes, eg. "sy-repid"
            var concat = JsUtils.RegexReplace(node.ConcatTokens(), @" ", "", RegexOptions.ECMAScript);
            var offset = JsUtils.Or(JsUtils.RegexReplace(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldOffset>()?.ConcatTokens(), @" ", "", RegexOptions.ECMAScript), "");
            var length = JsUtils.Or(JsUtils.RegexReplace(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldLength>()?.ConcatTokens(), @" ", "", RegexOptions.ECMAScript), "");
            var found = input.Scope.FindVariable(JsUtils.ReplaceFirst(JsUtils.ReplaceFirst(concat, offset, ""), length, ""));
            if (found != null)
            {
                if (refType != null)
                {
                    input.Scope.AddReference(node.GetFirstToken(), found, refType, input.Filename);
                }
                // this is not completely correct, but will work, dashes in names is a mess anyhow
                return found.GetType();
            }
        }

        AbstractType? context = null;
        var children = node.GetChildren();
        context = FieldChain.FindTop(children.ElementAtOrDefault(0), input, refType);

        string prev = children.ElementAtOrDefault(0).ConcatTokens();
        for (var i = 1; i < children.Count; i++)
        {
            var current = children[i];
            if (current == null)
            {
                break;
            }

            if (current.Get() is DashW)
            {
                var message = "Ending with dash";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, current.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            else if (current.Get() is Dash)
            {
                if (context is UnknownType)
                {
                    /*
                    const message = "Not a structure, type unknown, FieldChain";
                    input.issues.push(syntaxIssue(input, current.getFirstToken(), message));
                     */
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                else if (!((context is StructureType)) && !((context is TableType && ((TableType)context).IsWithHeader())) && !((context is VoidType)))
                {
                    if (context is TableType && ((TableType)context).IsWithHeader() == false)
                    {
                        string contextName = "";
                        for (var j = 0; j < i; j++)
                        {
                            contextName += children.ElementAtOrDefault(j).ConcatTokens();
                        }
                        if (input.Scope.IsAllowHeaderUse(contextName))
                        {
                            // FOR ALL ENTRIES workaround
                            context = ((TableType)context).GetRowType();
                            if (!((context is StructureType)) && !((context is VoidType)))
                            {
                                context = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "TABLE_LINE", Type = context } });
                            }
                        }
                        else
                        {
                            var message = "Table without header, cannot access fields, " + contextName;
                            input.Issues.Add(SyntaxInput.SyntaxIssue(input, current.GetFirstToken(), message));
                            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                        }
                    }
                    else
                    {
                        var message = JsUtils.ToUpper(prev) + " is not structured (" + JsUtils.Str(context?.GetType().Name) + ")";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, current.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                }
            }
            else if (current.Get() is InstanceArrow)
            {
                if (!((context is ObjectReferenceType)) && !((context is DataReference)) && !((context is VoidType)))
                {
                    var message = "Not an object reference, field chain";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, current.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
            else if (current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dereference)
            {
                context = Dereference.RunSyntax(current, context, input);
                if (allowGenericDeference == false && context?.IsGeneric() == true && !(Versions.ReleaseAtLeast(input.Scope.GetRelease(), Release.v756)) && input.Scope.GetLanguageVersion() != LanguageVersion.Cloud && !input.Scope.GetOpenABAP())
                {
                    throw new Exception("A generic reference cannot be dereferenced");
                }
            }
            else if (current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentName)
            {
                if (context is TableType && ((TableType)context).IsWithHeader())
                {
                    context = ((TableType)context).GetRowType();
                }
                context = ComponentName.RunSyntax(context, current, input);
            }
            else if (current is ExpressionNode && (((ExpressionNode)current).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.TableExpression)
            {
                if (!((context is TableType)) && !((context is VoidType)) && !((context is UnknownType)))
                {
                    var message = "Table expression, expected table";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, ((ExpressionNode)current).GetFirstToken(), message));
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
                context = AttributeName.RunSyntax(context, current, input, refType);
            }
            else if (current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldOffset && current is ExpressionNode)
            {
                var offset = FieldOffset.RunSyntax(((ExpressionNode)current), input);
                if (!(Substring.IsFieldSymbolItself(children.Cast<INode>().ToList(), i)))
                {
                    context = Substring.SubstringType(context, offset, null, ((ExpressionNode)current), input);
                }
            }
            else if (current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldLength && current is ExpressionNode)
            {
                var length = FieldLength.RunSyntax(((ExpressionNode)current), input);
                if (!(Substring.IsFieldSymbolItself(children.Cast<INode>().ToList(), i)))
                {
                    context = Substring.SubstringType(context, null, length, ((ExpressionNode)current), input);
                }
            }

            prev = current.ConcatTokens();
        }

        return context;
    }

    ////////////////
    private static AbstractType? FindTop(INode? node, SyntaxInput input, object? type)
    {

        if (node == null)
        {
            return null;
        }

        if (node is ExpressionNode && (((ExpressionNode)node).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.SourceFieldSymbol)
        {
            return SourceFieldSymbol.RunSyntax(((ExpressionNode)node), input);
        }
        else if (node is ExpressionNode && (((ExpressionNode)node).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.SourceField)
        {
            return SourceField.RunSyntax(((ExpressionNode)node), input, type);
        }
        else if (node is ExpressionNode && (((ExpressionNode)node).Get()) is Field)
        {
            return SourceField.RunSyntax(((ExpressionNode)node), input, type);
        }
        else if (node.Get() is ClassName)
        {
            var classTok = node.GetFirstToken();
            var classNam = classTok.GetStr();
            if (JsUtils.ToUpper(classNam) == "OBJECT")
            {
                return new GenericObjectReferenceType();
            }
            var found = input.Scope.ExistsObject(classNam);
            if ((found?.id) != null)
            {
                input.Scope.AddReference(classTok, found.Value.id, ReferenceType.ObjectOrientedReference, input.Filename);
                return new ObjectReferenceType(found.Value.id);
            }
            else if (input.Scope.GetDDIC().InErrorNamespace(classNam) == false)
            {
                input.Scope.AddReference(classTok, null, ReferenceType.ObjectOrientedVoidReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(classNam) });
                return VoidType.Get(classNam);
            }
            else
            {
                var message = "Unknown class " + classNam;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
        }

        return null;
    }
}
