// Ported from abaplint packages/core/src/abap/artifacts.ts, getStructures (MIT, see THIRD-PARTY-NOTICES.md in this project)
// The list keeps the export order of 3_structures/structures/index.ts.
using ScipSharp.Indexer.Abap.Structures.Structures;

namespace ScipSharp.Indexer.Abap;

using St = ScipSharp.Indexer.Abap.Structures.Structures;

public static partial class ArtifactsABAP
{
    static partial void AddStructures(List<IStructure> list)
    {
        list.Add(new St.Any());
        list.Add(new St.AtFirst());
        list.Add(new St.AtLast());
        list.Add(new St.At());
        list.Add(new St.Body());
        list.Add(new St.CaseType());
        list.Add(new St.Case());
        list.Add(new St.CatchSystemExceptions());
        list.Add(new St.Catch());
        list.Add(new St.Chain());
        list.Add(new St.ClassData());
        list.Add(new St.ClassDefinition());
        list.Add(new St.ClassGlobal());
        list.Add(new St.ClassImplementation());
        list.Add(new St.Cleanup());
        list.Add(new St.Constants());
        list.Add(new St.Data());
        list.Add(new St.Define());
        list.Add(new St.Do());
        list.Add(new St.DynproLogic());
        list.Add(new St.Else());
        list.Add(new St.ElseIf());
        list.Add(new St.EnhancementSection());
        list.Add(new St.Enhancement());
        list.Add(new St.ExecSQL());
        list.Add(new St.Form());
        list.Add(new St.FunctionModule());
        list.Add(new St.If());
        list.Add(new St.InterfaceGlobal());
        list.Add(new St.Interface());
        list.Add(new St.LoopAtScreen());
        list.Add(new St.Loop());
        list.Add(new St.Method());
        list.Add(new St.Module());
        list.Add(new St.Normal());
        list.Add(new St.OnChange());
        list.Add(new St.PrivateSection());
        list.Add(new St.ProcessAfterInput());
        list.Add(new St.ProcessBeforeOutput());
        list.Add(new St.ProcessOnValueRequest());
        list.Add(new St.ProtectedSection());
        list.Add(new St.Provide());
        list.Add(new St.PublicSection());
        list.Add(new St.SectionContents());
        list.Add(new St.Select());
        list.Add(new St.ProcessOnHelpRequest());
        list.Add(new St.DynproLoop());
        list.Add(new St.Statics());
        list.Add(new St.TestInjection());
        list.Add(new St.TestSeam());
        list.Add(new St.LoopExtract());
        list.Add(new St.Try());
        list.Add(new St.TypeEnum());
        list.Add(new St.TypeMesh());
        list.Add(new St.Types());
        list.Add(new St.WhenType());
        list.Add(new St.When());
        list.Add(new St.While());
        list.Add(new St.With());
    }
}
