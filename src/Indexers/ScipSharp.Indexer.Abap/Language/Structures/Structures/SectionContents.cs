// Ported from abaplint packages/core/src/abap/3_structures/structures/section_contents.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class SectionContents : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return Star(Alt(Sta<Statements.MethodDef>(),
                        Sta<Statements.InterfaceDef>(),
                        Sta<Statements.Data>(),
                        Sta<Statements.ClassData>(),
                        Sta<Statements.Events>(),
                        Sta<Statements.Constant>(),
                        Sta<Statements.Aliases>(),
                        Sta<Statements.TypePools>(),
                        Sta<Statements.InterfaceLoad>(),
                        Sta<Statements.ClassDefinitionLoad>(),
                        Sta<Statements.Include>(),
                        Sub<Types>(),
                        Sub<Constants>(),
                        Sub<TypeEnum>(),
                        Sub<TypeMesh>(),
                        Sub<Data>(),
                        Sub<ClassData>(),
                        Sta<Statements.Type>()));
    }
}
