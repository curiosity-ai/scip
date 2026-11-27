// Ported from abaplint packages/core/src/abap/3_structures/structures/process_after_input.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class ProcessAfterInput : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var pai = Star(Alt(
          Sta<Statements.Module>(),
          Sta<Statements.Field>(),
          Sta<Statements.CallSubscreen>(),
          Sub<Chain>(),
          Sub<DynproLoop>(),
          Sub<LoopExtract>(),
          Sub<Loop>()));

        return Seq(
          Sta<Statements.ProcessAfterInput>(),
          pai
        );
    }
}
