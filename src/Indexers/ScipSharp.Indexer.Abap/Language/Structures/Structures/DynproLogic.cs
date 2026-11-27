// Ported from abaplint packages/core/src/abap/3_structures/structures/dynpro_logic.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class DynproLogic : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return Seq(
          Sub<ProcessBeforeOutput>(),
          Opt(Sub<ProcessAfterInput>()),
          Opt(Sub<ProcessOnHelpRequest>()),
          Opt(Sub<ProcessOnValueRequest>()),
          Opt(Sub<ProcessOnHelpRequest>())
        );
    }
}
