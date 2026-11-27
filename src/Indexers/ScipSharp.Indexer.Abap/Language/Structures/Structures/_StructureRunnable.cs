// Ported from abaplint packages/core/src/abap/3_structures/structures/_structure_runnable.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

public interface IStructureRunnable
{
    string ToRailroad();
    List<string> GetUsing();
    IMatch Run(List<StatementNode> statements, INode parent);
    /// <summary>Matches from statements[index] without copying the statements after it.</summary>
    IMatchAt RunAt(List<StatementNode> statements, int index, INode parent);
    /// <summary>Returns first token in upper case, if not applicable then the empty string.</summary>
    List<string> First();
}
