// Ported from abaplint packages/core/src/abap/5_syntax/statements/selection_screen.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class SelectionScreen : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        var blockNode = node.FindFirstExpression<BlockName>();
        var blockToken = blockNode?.GetFirstToken();
        var blockName = blockNode?.ConcatTokens();
        var concat = JsUtils.ToUpper(node.ConcatTokens());

        int maxNameLengthAllowed = concat.Contains("BEGIN OF TABBED BLOCK", StringComparison.Ordinal) ? 16 : 20;

        if (blockName != null && blockName.Length > maxNameLengthAllowed)
        {
            var message = "SELECTION-SCREEN block name too long, " + blockName;
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var field = node.FindFirstExpression<InlineField>();
        if (field != null && field.ConcatTokens().Length > 8)
        {
            var message = "SELECTION-SCREEN name too long, " + field.ConcatTokens();
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, field.GetFirstToken(), message));
            return;
        }

        var fieldName = field?.GetFirstToken();

        if (concat.Contains("BEGIN OF TABBED BLOCK", StringComparison.Ordinal) && blockToken != null)
        {
            var type = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "PROG", Type = new CharacterType(40) }, new IStructureComponent { Name = "DYNNR", Type = new CharacterType(4) }, new IStructureComponent { Name = "ACTIVETAB", Type = new CharacterType(132) } });

            input.Scope.AddIdentifier(new TypedIdentifier(blockToken, input.Filename, type, new List<string> { IdentifierMeta.SelectionScreenTab }));
        }
        else if (concat.StartsWith("SELECTION-SCREEN TAB", StringComparison.Ordinal) && fieldName != null)
        {
            var id = new TypedIdentifier(fieldName, input.Filename, new CharacterType(83), new List<string> { IdentifierMeta.SelectionScreenTab });
            input.Scope.AddNamedIdentifier(field!.ConcatTokens(), id);
        }
        else if (fieldName != null)
        {
            var id = new TypedIdentifier(fieldName, input.Filename, new CharacterType(83));
            input.Scope.AddNamedIdentifier(field!.ConcatTokens(), id);
        }
    }
}
