// Ported from abaplint packages/core/src/abap/3_structures/structures/any.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Any : IStructure
{
    public IStructureRunnable GetMatcher()
    {

        return Star(Alt(Sta<Statements.ClassLocalFriends>(),
                        Sta<Statements.ClassDeferred>(),
                        Sta<Statements.Report>(),
                        Sta<Statements.Program>(),
                        Sta<Statements.Parameter>(),
                        Sta<Statements.CheckSelectOptions>(),
                        Sta<Statements.Get>(),
                        Sta<Statements.Initialization>(),
                        Sta<Statements.InterfaceDeferred>(),
                        Sta<Statements.SelectionScreen>(),
                        Sta<Statements.SelectOption>(),
                        Sta<Statements.AtSelectionScreen>(),
                        Sta<Statements.AtLineSelection>(),
                        Sta<Statements.AtPF>(),
                        Sta<Statements.AtUserCommand>(),
                        Sta<Statements.StartOfSelection>(),
                        Sta<Statements.EndOfSelection>(),
                        Sta<Statements.LoadOfProgram>(),
                        Sta<Statements.TopOfPage>(),
                        Sta<Statements.EndOfPage>(),
                        Sta<Statements.Controls>(),
                        Sta<Statements.TypePools>(),
                        Sta<Statements.TypePool>(),
                        Sta<Statements.FunctionPool>(),
                        Sub<Normal>(),
                        Sub<Form>(),
                        Sub<Module>(),
                        Sub<FunctionModule>(),
                        Sub<Interface>(),
                        Sub<ClassDefinition>(),
                        Sub<ClassImplementation>()));
    }
}
