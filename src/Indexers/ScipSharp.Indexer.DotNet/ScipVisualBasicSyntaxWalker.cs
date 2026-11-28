using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System.Diagnostics;

namespace ScipSharp.Indexer.DotNet;

/// <summary>
/// Visits every node of a single VisualBasic syntax tree and produces a SCIP <code>Document</code>.
/// </summary>
internal sealed class ScipVisualBasicSyntaxWalker : VisualBasicSyntaxVisitor
{
    private readonly SemanticModel _semanticModel;
    private readonly ScipDocumentIndexer _scipDocumentIndexer;

    public ScipVisualBasicSyntaxWalker(ScipDocumentIndexer scipSymbolFormatter, SemanticModel semanticModel)
    {
        _scipDocumentIndexer = scipSymbolFormatter;
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Visits every node of <paramref name="root"/> in document (pre-)order, as a node-depth syntax walker would, but
    /// iteratively: the recursive walker throws <see cref="InsufficientExecutionStackException"/> on deeply nested
    /// syntax such as a generated expression with thousands of operands. The <c>base.Visit*</c> calls of the
    /// overrides below are no-ops on a visitor.
    /// </summary>
    public void Walk(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodesAndSelf())
        {
            Visit(node);
        }
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetSymbolInfo(node).Symbol, node.GetLocation(), false);
        base.VisitIdentifierName(node);
    }

    public override void VisitClassStatement(ClassStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitClassStatement(node);
    }

    public override void VisitModuleStatement(ModuleStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitModuleStatement(node);
    }

    public override void VisitEnumStatement(EnumStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitEnumStatement(node);
    }

    public override void VisitCatchStatement(CatchStatementSyntax node)
    {
        // `Catch` and `Catch When condition` declare no exception variable.
        if (node.IdentifierName != null)
        {
            _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.IdentifierName.Identifier.GetLocation(), true);
        }

        base.VisitCatchStatement(node);
    }

    public override void VisitSubNewStatement(SubNewStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.NewKeyword.GetLocation(), true);
        base.VisitSubNewStatement(node);
    }

    public override void VisitDelegateStatement(DelegateStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitDelegateStatement(node);
    }

    public override void VisitEventStatement(EventStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitEventStatement(node);
    }

    public override void VisitInterfaceStatement(InterfaceStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitInterfaceStatement(node);
    }

    public override void VisitPropertyStatement(PropertyStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitPropertyStatement(node);
    }

    public override void VisitStructureStatement(StructureStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitStructureStatement(node);
    }

    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        foreach (var identifiers in node.Names)
        {
            _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(identifiers), identifiers.Identifier.GetLocation(), true);
        }
        base.VisitVariableDeclarator(node);
    }

    public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitEnumMemberDeclaration(node);
    }

    public override void VisitMethodStatement(MethodStatementSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitMethodStatement(node);
    }

    public override void VisitParameter(ParameterSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitParameter(node);
    }

    public override void VisitTypeParameter(TypeParameterSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitTypeParameter(node);
    }

    public override void VisitExpressionRangeVariable(ExpressionRangeVariableSyntax node)
    {
        if (node.NameEquals != null)
        {
            _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node.NameEquals.Identifier), node.NameEquals.Identifier.GetLocation(), true);
        }
        base.VisitExpressionRangeVariable(node);
    }

    public override void VisitAggregationRangeVariable(AggregationRangeVariableSyntax node)
    {
        if (node.NameEquals != null)
        {
            _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node.NameEquals.Identifier), node.NameEquals.Identifier.GetLocation(), true);
        }
        base.VisitAggregationRangeVariable(node);
    }

    public override void VisitCollectionRangeVariable(CollectionRangeVariableSyntax node)
    {
        _scipDocumentIndexer.VisitOccurrence(_semanticModel.GetDeclaredSymbol(node), node.Identifier.GetLocation(), true);
        base.VisitCollectionRangeVariable(node);
    }
}
