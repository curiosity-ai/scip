// Ported from abaplint packages/core/src/abap/3_structures/structures/class_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class ClassDefinition : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var body = Seq(
          Opt(Sta<Statements.SetExtendedCheck>()),
          Star(Sta<Statements.TypePools>()),
          Opt(Sub<PublicSection>()),
          Opt(Sub<ProtectedSection>()),
          Opt(Sub<PrivateSection>()),
          Opt(Sta<Statements.SetExtendedCheck>()));

        return BeginEnd(Sta<Statements.ClassDefinition>(), body, Sta<Statements.EndClass>());
    }
}
