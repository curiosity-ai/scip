// Ported from abaplint packages/core/src/abap/5_syntax/statements/include_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class IncludeType
{
    public object RunSyntax(StatementNode node, SyntaxInput input)
    {
        List<IStructureComponent> components = new List<IStructureComponent>();

        var iname = node.FindFirstExpression<TypeName>();
        if (iname == null)
        {
            throw new AssertError("IncludeType, unexpected node structure");
        }
        var firstToken = iname.GetFirstToken();
        var name = firstToken.GetStr();
        var isStructure = node.FindDirectTokenByText("STRUCTURE") != null;

        AbstractType? ityp = new BasicTypes(input).ParseType(iname);
        if ((ityp is VoidType && isStructure) || ityp is UnknownType)
        {
            var found = input.Scope.FindVariable(name);
            if (found != null)
            {
                input.Scope.AddReference(firstToken, found, ReferenceType.DataReadReference, input.Filename);
                ityp = found.GetType();
            }
        }

        var @as = node.FindExpressionAfterToken("AS")?.ConcatTokens();
        if (!string.IsNullOrEmpty(@as) && ityp is StructureType)
        {
            ityp = new StructureType(((StructureType)ityp).GetComponents().Concat(new List<IStructureComponent> { new IStructureComponent { Name = @as, Type = ((StructureType)ityp), AsInclude = true } }).ToList());
        }
        else if (ityp is TableType && isStructure)
        {
            ityp = ((TableType)ityp).GetRowType();
        }
        if (ityp is VoidType)
        {
            return ((VoidType)ityp);
        }
        else if (ityp is UnknownType)
        {
            return ((UnknownType)ityp);
        }

        if (!((ityp is StructureType)))
        {
            var message = "not structured, " + name;
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, iname.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        var suffix = node.FindExpressionAfterToken("SUFFIX")?.ConcatTokens();
        if (!string.IsNullOrEmpty(suffix) && ityp is StructureType)
        {
            List<IStructureComponent> components2 = new List<IStructureComponent>();
            foreach (var c in ((StructureType)ityp).GetComponents())
            {
                if (c.Name == @as)
                {
                    components2.Add(new IStructureComponent { Name = c.Name, Type = c.Type, Suffix = suffix, AsInclude = c.AsInclude });
                    continue;
                }
                components2.Add(new IStructureComponent { Name = c.Name + suffix, Type = c.Type });
            }
            ityp = new StructureType(components2);
        }

        if (ityp != null && ((object)ityp) is TypedIdentifier && ((TypedIdentifier)(object)ityp).GetType() is StructureType)
        {
            var stru = ((StructureType)((TypedIdentifier)(object)ityp).GetType());
            components.AddRange(stru.GetComponents());
        }
        else if (ityp != null && ityp is StructureType)
        {
            components.AddRange(((StructureType)ityp).GetComponents());
        }
        else if (ityp != null && ityp is VoidType)
        {
            return ((VoidType)ityp);
        }
        else if (input.Scope.GetDDIC().InErrorNamespace(name) == false)
        {
            return VoidType.Get(name);
        }
        else
        {
            var message = "IncludeType, type not found \"" + iname.ConcatTokens() + "\"";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        return components;
    }
}
