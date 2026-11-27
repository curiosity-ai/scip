// Ported from abaplint packages/core/src/abap/types/_class_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;

namespace ScipSharp.Indexer.Abap.Types;

public interface IClassDefinition : IInterfaceDefinition
{
    bool IsFinal();
    bool IsForTesting();
    bool IsAbstract();
    bool IsSharedMemory();
    List<string> GetFriends();
    Visibility GetCreateVisibility();
}
