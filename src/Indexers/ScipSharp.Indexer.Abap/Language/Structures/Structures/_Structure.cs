// Ported from abaplint packages/core/src/abap/3_structures/structures/_structure.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Structures.Structures;

public interface IStructure
{
    IStructureRunnable GetMatcher();
}
