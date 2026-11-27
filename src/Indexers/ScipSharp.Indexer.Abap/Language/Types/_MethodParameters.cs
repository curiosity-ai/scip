// Ported from abaplint packages/core/src/abap/types/_method_parameters.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Types;

public interface IMethodParameters
{
    List<TypedIdentifier> GetAll();
    List<TypedIdentifier> GetImporting();
    List<TypedIdentifier> GetRequiredParameters();
    List<TypedIdentifier> GetExporting();
    List<TypedIdentifier> GetChanging();
    List<string> GetOptional();
    TypedIdentifier? GetReturning();
    List<string> GetExceptions();
    string GetFilename();
    string? GetDefaultImporting();
    ExpressionNode? GetParameterDefault(string parameter);
}
