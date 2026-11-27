// Ported from abaplint packages/core/src/abap/3_structures/structure_result.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Structures;

public sealed record IStructureResult(IReadOnlyList<Issue> Issues, StructureNode? Node);
