// Ported from abaplint packages/core/src/abap/5_syntax/statements/controls.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Controls : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        var name = node.FindDirectExpression<NamespaceSimpleName>();
        var token = name?.GetFirstToken();

        if (node.FindDirectTokenByText("TABSTRIP") != null && token != null)
        {
            var type = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "ACTIVETAB", Type = new CharacterType(132) } });
            var id = new TypedIdentifier(token, input.Filename, type);
            input.Scope.AddIdentifier(id);
        }

        if (node.FindDirectTokenByText("TABLEVIEW") != null && token != null)
        {
            var cols = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "SCREEN", Type = VoidType.Get("TABLEVIEW-SCREEN") }, new IStructureComponent { Name = "INDEX", Type = IntegerType.Get() }, new IStructureComponent { Name = "SELECTED", Type = new CharacterType(1) }, new IStructureComponent { Name = "VISLENGTH", Type = IntegerType.Get() }, new IStructureComponent { Name = "INVISIBLE", Type = new CharacterType(1) } });
            var type = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "FIXED_COLS", Type = new CharacterType(132) }, new IStructureComponent { Name = "LINES", Type = IntegerType.Get() }, new IStructureComponent { Name = "TOP_LINE", Type = IntegerType.Get() }, new IStructureComponent { Name = "CURRENT_LINE", Type = IntegerType.Get() }, new IStructureComponent { Name = "LEFT_COL", Type = IntegerType.Get() }, new IStructureComponent { Name = "LINE_SEL_MODE", Type = new CharacterType(1) }, new IStructureComponent { Name = "COL_SEL_MODE", Type = new CharacterType(1) }, new IStructureComponent { Name = "LINE_SELECTOR", Type = new CharacterType(1) }, new IStructureComponent { Name = "H_GRID", Type = new CharacterType(1) }, new IStructureComponent { Name = "V_GRID", Type = new CharacterType(1) }, new IStructureComponent { Name = "COLS", Type = new TableType(cols, new ITableOptions { WithHeader = false, KeyType = TableKeyType.@default }) }, new IStructureComponent { Name = "INVISIBLE", Type = new CharacterType(1) } });
            var id = new TypedIdentifier(token, input.Filename, type);
            input.Scope.AddIdentifier(id);
        }
    }
}
