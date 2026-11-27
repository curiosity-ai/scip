// Ported from abaplint packages/core/src/abap/3_structures/structures/data.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Data : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.DataBegin>(),
                        Star(Alt(Sta<Statements.Data>(),
                                 Sub<Data>(),
                                 Sta<Statements.Include>(),
                                 Sta<Statements.Ranges>(),
                                 Sta<Statements.Constant>(),
                                 Sub<Constants>(),
                                 Sta<Statements.Type>(),
                                 Sub<Types>(),
                                 Sub<Enhancement>(),
                                 Sub<Define>(),
                                 Sta<Statements.IncludeType>(),
                                 Sta<Statements.Parameter>(),
                                 Sta<Statements.SelectionScreen>(),
                                 Sta<Statements.SelectOption>(),
                                 Sta<Statements.TypePools>(),
                                 Sta<Statements.EnhancementPoint>())),
                        Sta<Statements.DataEnd>());
    }
}
