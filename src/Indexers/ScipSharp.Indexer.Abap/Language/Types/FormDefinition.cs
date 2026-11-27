// Ported from abaplint packages/core/src/abap/types/form_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Syntax;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Types;

public class FormDefinition : global::ScipSharp.Indexer.Abap.FileInformation.Identifier, IFormDefinition
{
    private readonly StatementNode node;
    private readonly List<TypedIdentifier> tableParameters;
    private readonly List<TypedIdentifier> usingParameters;
    private readonly List<TypedIdentifier> changingParameters;

    // TS computes the name token before calling super()
    private static AbstractToken NameToken(IStructureOrStatementNode node)
    {
        StatementNode st = node is StructureNode ? ((StructureNode)node).FindFirstStatement<Form>()! : ((StatementNode)node);

        // FORMs can contain a dash in the name
        var formName = st.FindFirstExpression<FormName>();
        var pos = formName!.GetFirstToken().GetStart();
        var name = formName!.ConcatTokens();
        return new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(pos, name);
    }

    public FormDefinition(IStructureOrStatementNode node, SyntaxInput input)
        : base(NameToken(node), input.Filename)
    {
        StatementNode st = node is StructureNode ? ((StructureNode)node).FindFirstStatement<Form>()! : ((StatementNode)node);
        this.node = st;

        this.tableParameters = this.FindTables(input);
        this.usingParameters = this.FindType(typeof(FormUsing), input);
        this.changingParameters = this.FindType(typeof(FormChanging), input);
    }

    public List<TypedIdentifier> GetTablesParameters()
    {
        return this.tableParameters;
    }

    public List<TypedIdentifier> GetUsingParameters()
    {
        return this.usingParameters;
    }

    public List<TypedIdentifier> GetChangingParameters()
    {
        return this.changingParameters;
    }

    ///////////////
    private List<TypedIdentifier> FindTables(SyntaxInput input)
    {
        List<TypedIdentifier> ret = new List<TypedIdentifier>();

        var tables = this.node.FindFirstExpression<FormTables>();
        if (tables == null)
        {
            return new List<TypedIdentifier>();
        }

        foreach (var param in tables.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.FormParam>())
        {
            if (param.GetChildren().Count == 1)
            {
                // untyped TABLES parameter
                ret.Add(new TypedIdentifier(param.GetFirstToken(), input.Filename, VoidType.Get("FORM:UNTYPED"), new List<string> { IdentifierMeta.FormParameter }));
            }
            else
            {
                var p = global::ScipSharp.Indexer.Abap.Syntax.Expressions.FormParam.RunSyntax(param, input);

                AbstractType type = p.GetType();

                var isStructure = param.FindDirectTokenByText("STRUCTURE") != null;
                if (isStructure)
                {
                    type = new TableType(type, new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default });
                }

                if (type is TableType)
                {
                    type = new TableType(((TableType)type).GetRowType(), new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default });
                }
                else if (!((type is UnknownType)) && !((type is VoidType)))
                {
                    type = new UnknownType("FORM TABLES type must be table type");
                }

                // keep the meta from FormParam, so STRUCTURE typing can be recognized later
                ret.Add(new TypedIdentifier(p.GetToken(), input.Filename, type, p.GetMeta()));
            }
        }

        return ret;
    }

    private List<TypedIdentifier> FindType(System.Type type, SyntaxInput input)
    {
        var found = this.node.FindFirstExpression(type);
        if (found == null)
        {
            return new List<TypedIdentifier>();
        }
        return this.FindParams(found, input);
    }

    private List<TypedIdentifier> FindParams(INode node, SyntaxInput input)
    {
        List<TypedIdentifier> res = new List<TypedIdentifier>();
        foreach (var param in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.FormParam>())
        {
            var p = global::ScipSharp.Indexer.Abap.Syntax.Expressions.FormParam.RunSyntax(param, input);
            res.Add(p);
        }
        return res;
    }
}
