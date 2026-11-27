// Ported from abaplint packages/core/src/abap/types/_method_definitions.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types;

public interface IMethodDefinitions
{
    IEnumerable<IMethodDefinition> GetAll();
    IMethodDefinition? GetByName(string? name);
}
