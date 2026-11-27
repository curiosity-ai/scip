// Ported from abaplint packages/core/src/abap/types/_event_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types;

public interface IEventDefinition
{
    string GetName();
    List<TypedIdentifier> GetParameters();
    bool IsStatic();
}
