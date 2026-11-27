// Ported from abaplint packages/core/src/abap/5_syntax/structures/cleanup.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Structures.Structures;

namespace ScipSharp.Indexer.Abap.Syntax.Structures;

// type StructureType = new () => IStructure; (mapped at use sites)

// a CLEANUP block cannot contain statements that statically leave it: RETURN, and EXIT, CONTINUE or CHECK
// that do not belong to a loop inside the block, these end a loop outside the TRY or the processing block
public class Cleanup
{

    internal static readonly List<System.Type> LOOPS = new List<System.Type> { typeof(global::ScipSharp.Indexer.Abap.Structures.Structures.Loop), typeof(global::ScipSharp.Indexer.Abap.Structures.Structures.LoopAtScreen), typeof(global::ScipSharp.Indexer.Abap.Structures.Structures.LoopExtract), typeof(global::ScipSharp.Indexer.Abap.Structures.Structures.Do), typeof(global::ScipSharp.Indexer.Abap.Structures.Structures.While), typeof(global::ScipSharp.Indexer.Abap.Structures.Structures.Select), typeof(global::ScipSharp.Indexer.Abap.Structures.Structures.With), typeof(global::ScipSharp.Indexer.Abap.Structures.Structures.Provide) };
    public static void RunSyntax(StructureNode node, SyntaxInput input)
    {
        if (input.Scope.GetOpenABAP())
        {
            return;
        }
        var body = node.FindDirectStructure<Body>();
        if (body != null)
        {
            Cleanup.Walk(body, false, input);
        }
    }

    private static void Walk(StructureNode node, bool inLoop, SyntaxInput input)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is StructureNode)
            {
                var stru = ((StructureNode)child).Get();
                // a nested CLEANUP block is checked on its own, macro definitions and test injections do not run here
                if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.Cleanup || stru is global::ScipSharp.Indexer.Abap.Structures.Structures.Define || stru is global::ScipSharp.Indexer.Abap.Structures.Structures.TestInjection)
                {
                    continue;
                }
                Cleanup.Walk(((StructureNode)child), inLoop || LOOPS.Any(l => l.IsInstanceOfType(stru)), input);
            }
            else if (child is StatementNode)
            {
                var name = Cleanup.LeavingStatement(((StatementNode)child), inLoop);
                if (name != null)
                {
                    var message = $"{name} cannot leave a CLEANUP block";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, ((StatementNode)child).GetFirstToken(), message));
                }
            }
        }
    }

    private static string? LeavingStatement(StatementNode node, bool inLoop)
    {
        var s = node.Get();
        if (s is Return)
        {
            return "RETURN";
        }
        else if (inLoop == true)
        {
            return null;
        }
        else if (s is Exit && node.FindDirectTokenByText("FROM") == null)
        {
            return "EXIT";
        }
        else if (s is Continue)
        {
            return "CONTINUE";
        }
        else if (s is Check)
        {
            return "CHECK";
        }
        return null;
    }
}
