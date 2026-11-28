// Ported from abaplint packages/core/src/abap/5_syntax/syntax.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Structures.Structures;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap.Syntax;

// -----------------------------------
public static class SyntaxHandlers
{
    static SyntaxHandlers()
    {
        if (Map.Count == 0)
        {
            AddToMap(new Statements.InterfaceDeferred());
            AddToMap(new Statements.Perform());
            AddToMap(new Statements.ClassDeferred());
            AddToMap(new Statements.Call());
            AddToMap(new Statements.SetHandler());
            AddToMap(new Statements.ClassImplementation());
            AddToMap(new Statements.MethodImplementation());
            AddToMap(new Statements.Move());
            AddToMap(new Statements.GetBadi());
            AddToMap(new Statements.CallBadi());
            AddToMap(new Statements.GetCursor());
            AddToMap(new Statements.Replace());
            AddToMap(new Statements.TruncateDataset());
            AddToMap(new Statements.Assert());
            AddToMap(new Statements.Catch());
            AddToMap(new Statements.Loop());
            AddToMap(new Statements.LoopAtScreen());
            AddToMap(new Statements.SetPFStatus());
            AddToMap(new Statements.SetTitlebar());
            AddToMap(new Statements.Submit());
            AddToMap(new Statements.InsertTextpool());
            AddToMap(new Statements.ReadTable());
            AddToMap(new Statements.SyntaxCheck());
            AddToMap(new Statements.DeleteReport());
            AddToMap(new Statements.Import());
            AddToMap(new Statements.Collect());
            AddToMap(new Statements.Export());
            AddToMap(new Statements.Scan());
            AddToMap(new Statements.Transfer());
            AddToMap(new Statements.Split());
            AddToMap(new Statements.CallFunction());
            AddToMap(new Statements.DeleteInternal());
            AddToMap(new Statements.DeleteCluster());
            AddToMap(new Statements.Clear());
            AddToMap(new Statements.Free());
            AddToMap(new Statements.Refresh());
            AddToMap(new Statements.Return());
            AddToMap(new Statements.Receive());
            AddToMap(new Statements.GetBit());
            AddToMap(new Statements.ClassLocalFriends());
            AddToMap(new Statements.Select());
            AddToMap(new Statements.ModifyScreen());
            AddToMap(new Statements.InsertInternal());
            AddToMap(new Statements.Pack());
            AddToMap(new Statements.Unpack());
            AddToMap(new Statements.Assign());
            AddToMap(new Statements.SetLocale());
            AddToMap(new Statements.SetParameter());
            AddToMap(new Statements.Convert());
            AddToMap(new Statements.Controls());
            AddToMap(new Statements.When());
            AddToMap(new Statements.InsertDatabase());
            AddToMap(new Statements.DeleteDatabase());
            AddToMap(new Statements.UpdateDatabase());
            AddToMap(new Statements.Sort());
            AddToMap(new Statements.Wait());
            AddToMap(new Statements.Condense());
            AddToMap(new Statements.SetBit());
            AddToMap(new Statements.OpenDataset());
            AddToMap(new Statements.CloseDataset());
            AddToMap(new Statements.ReadReport());
            AddToMap(new Statements.Do());
            AddToMap(new Statements.Describe());
            AddToMap(new Statements.Find());
            AddToMap(new Statements.Message());
            AddToMap(new Statements.SystemCall());
            AddToMap(new Statements.InsertFieldGroup());
            AddToMap(new Statements.GetTime());
            AddToMap(new Statements.Unassign());
            AddToMap(new Statements.CaseType());
            AddToMap(new Statements.GetParameter());
            AddToMap(new Statements.Format());
            AddToMap(new Statements.WhenType());
            AddToMap(new Statements.If());
            AddToMap(new Statements.LogPoint());
            AddToMap(new Statements.While());
            AddToMap(new Statements.GetPFStatus());
            AddToMap(new Statements.With());
            AddToMap(new Statements.WithLoop());
            AddToMap(new Statements.CallTransformation());
            AddToMap(new Statements.CallTransaction());
            AddToMap(new Statements.GetLocale());
            AddToMap(new Statements.GetReference());
            AddToMap(new Statements.ElseIf());
            AddToMap(new Statements.GetRunTime());
            AddToMap(new Statements.CreateObject());
            AddToMap(new Statements.ImportDynpro());
            AddToMap(new Statements.CreateData());
            AddToMap(new Statements.Case());
            AddToMap(new Statements.Shift());
            AddToMap(new Statements.Raise());
            AddToMap(new Statements.Concatenate());
            AddToMap(new Statements.Append());
            AddToMap(new Statements.SelectLoop());
            AddToMap(new Statements.Write());
            AddToMap(new Statements.MoveCorresponding());
            AddToMap(new Statements.AuthorityCheck());
            AddToMap(new Statements.InsertReport());
            AddToMap(new Statements.SelectionScreen());
            AddToMap(new Statements.AtSelectionScreen());
            AddToMap(new Statements.Ranges());
            AddToMap(new Statements.Add());
            AddToMap(new Statements.RaiseEvent());
            AddToMap(new Statements.Subtract());
            AddToMap(new Statements.AddCorresponding());
            AddToMap(new Statements.SubtractCorresponding());
            AddToMap(new Statements.Multiply());
            AddToMap(new Statements.Divide());
            AddToMap(new Statements.Check());
            AddToMap(new Statements.ModifyDatabase());
            AddToMap(new Statements.Form());
            AddToMap(new Statements.SelectOption());
            AddToMap(new Statements.Tables());
            AddToMap(new Statements.Parameter());
            AddToMap(new Statements.FieldSymbol());
            AddToMap(new Statements.ReadEntities());
            AddToMap(new Statements.GetPermissions());
            AddToMap(new Statements.ReadDataset());
            AddToMap(new Statements.ModifyEntities());
            AddToMap(new Statements.CommitEntities());
            AddToMap(new Statements.CallKernel());
            AddToMap(new Statements.OpenCursor());
            AddToMap(new Statements.FetchNextCursor());
            AddToMap(new Statements.CloseCursor());
            AddToMap(new Statements.Search());
            AddToMap(new Statements.Translate());
            AddToMap(new Statements.ModifyInternal());
            AddToMap(new Statements.ReadTextpool());
            AddToMap(new Statements.FieldGroup());
            AddToMap(new Statements.EditorCall());
            AddToMap(new Statements.DeleteMemory());
        }
    }

    // -----------------------------------
    internal static readonly Dictionary<string, StatementSyntax> Map = new Dictionary<string, StatementSyntax>();
    internal static void AddToMap(StatementSyntax handler)
    {
        if (Map.GetValueOrDefault(handler.GetType().Name) != null)
        {
            throw new AssertError("syntax.ts duplicate statement syntax handler");
        }
        Map[handler.GetType().Name] = handler;
    }
}


// -----------------------------------
public class SyntaxLogic
{
    private ABAPFile currentFile;
    // files the traversal walked into through INCLUDEs, outermost first
    private readonly List<string> includeStack = new List<string>();
    // Not in abaplint: how often each include was walked in this run. An include that is INCLUDEd from several places
    // is walked again each time (it may sit in different scopes), but includes that include the same includes (a
    // diamond) would be walked 2^depth times, so each include is walked at most MaxWalksPerInclude times.
    private const int MaxWalksPerInclude = 8;
    private readonly Dictionary<string, int> includeWalks = new Dictionary<string, int>();
    private List<Issue> issues;
    private List<Action> deferred;

    private readonly ABAPObject @object;
    private readonly IRegistry reg;

    private CurrentScope scope;
    private LoopTableChange loopTables;

    private (ObjectOriented oooc, Procedural proc) helpers;

    public SyntaxLogic(IRegistry reg, ABAPObject @object)
    {
        this.reg = reg;
        this.issues = new List<Issue>();
        this.deferred = new List<Action>();

        this.@object = @object;
    }

    public ISyntaxResult Run()
    {
        if (this.@object.syntaxResult != null)
        {
            return this.@object.syntaxResult;
        }

        // the scope is built here instead of in the constructor, cached results must stay cheap
        this.scope = CurrentScope.BuildDefault(this.reg, this.@object);
        this.helpers = (new ObjectOriented(this.scope), new Procedural(this.reg, this.scope));

        this.issues = new List<Issue>();
        this.deferred = new List<Action>();
        this.loopTables = new LoopTableChange();
        this.reg.GetDDICReferences().Clear(this.@object);
        this.reg.GetMSAGReferences().Clear(this.@object);

        if (this.@object is global::ScipSharp.Indexer.Abap.Objects.Program && ((global::ScipSharp.Indexer.Abap.Objects.Program)this.@object).IsInclude())
        {
            // todo, show some kind of error?
            return new ISyntaxResult { Issues = new List<Issue>(), Spaghetti = this.scope.Pop(new Position(int.MaxValue, int.MaxValue)) };
        }

        this.TraverseObject();

        // the FORM definitions are now all traversed, ie. their parameters are typed
        foreach (var d in this.deferred)
        {
            d();
        }
        this.deferred = new List<Action>();

        for (; ; )
        {
            var spaghetti = this.scope.Pop(new Position(int.MaxValue, int.MaxValue));
            if (spaghetti.GetTop().GetIdentifier().Stype == ScopeType.BuiltIn)
            {
                ISyntaxResult result = new ISyntaxResult { Issues = this.issues, Spaghetti = spaghetti };
                this.@object.syntaxResult = result;
                return result;
            }
        }
    }

    /////////////////////////////
    private CurrentScope TraverseObject()
    {
        var traversal = this.@object.GetSequencedFiles();

        if (this.@object is global::ScipSharp.Indexer.Abap.Objects.Program || this.@object is FunctionGroup)
        {

            foreach (var f in this.@object.GetSequencedFiles())
            {
                // add FORM defintions to the _global object scope
                this.helpers.proc.AddAllFormDefinitions(f, this.@object);
            }

            var main = this.@object.GetMainABAPFile();
            if (main != null)
            {
                string stype = ScopeType.Program;
                if (this.@object is FunctionGroup)
                {
                    stype = ScopeType.FunctionGroup;
                }

                this.scope.Push(stype, this.@object.GetName(), new Position(1, 1), main.GetFilename());

                if (this.@object is FunctionGroup)
                {
                    this.helpers.proc.FindFunctionGroupScope(((FunctionGroup)this.@object));
                }
            }
        }
        else if (this.@object is global::ScipSharp.Indexer.Abap.Objects.TypePool)
        {
            var main = ((global::ScipSharp.Indexer.Abap.Objects.TypePool)this.@object).GetMainABAPFile();
            if (main != null)
            {
                this.scope.Push(ScopeType.TypePool, ((global::ScipSharp.Indexer.Abap.Objects.TypePool)this.@object).GetName(), new Position(1, 1), main.GetFilename());
            }
        }

        foreach (var file in traversal)
        {
            this.currentFile = file;
            var structure = this.currentFile.GetStructure();
            if (structure == null)
            {
                return this.scope;
            }
            else
            {
                this.Traverse(structure);
            }
        }

        return this.scope;
    }

    private void NewIssue(AbstractToken token, string message)
    {
        var issue = Issue.AtToken(this.currentFile, token, message, SyntaxInput.CheckSyntaxKey, Severity.Error);
        this.issues.Add(issue);
    }

    private void Traverse(IStructureOrStatementNode node)
    {
        // Not in abaplint: recursion over nested source, give up on the object rather than overflow the stack
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new InsufficientExecutionStackException();
        }
        var loop = node is StructureNode && (((StructureNode)node).Get()) is global::ScipSharp.Indexer.Abap.Structures.Structures.Loop && this.loopTables.Push(((StructureNode)node), this.scope);

        foreach (var child in node.GetChildren())
        {
            var isStructure = child is StructureNode;
            var isStatement = child is StatementNode;

            try
            {
                if (isStructure)
                {
                    var gotoNext = this.UpdateScopeStructure(((StructureNode)(((StructureNode)child))));
                    if (gotoNext == true)
                    {
                        continue;
                    }
                }
                else if (isStatement)
                {
                    this.UpdateScopeStatement(((StatementNode)(((StatementNode)child))));
                }
            }
            catch (Exception e)
            {
                this.NewIssue(child.GetFirstToken(), e.Message);
                break;
            }

            // walk into INCLUDEs
            if (isStatement && (((StatementNode)child).Get()) is Include)
            {
                var file = this.helpers.proc.FindInclude(((StatementNode)(((StatementNode)child))), this.@object);
                // an INCLUDE that leads back to a file already being walked is not followed again
                var cyclic = file != null && (file.GetFilename() == this.currentFile.GetFilename() || this.includeStack.Contains(file.GetFilename()));
                var walks = file == null ? 0 : this.includeWalks.GetValueOrDefault(file.GetFilename());
                if (file != null && file.GetStructure() != null && cyclic == false && walks < MaxWalksPerInclude)
                {
                    this.includeWalks[file.GetFilename()] = walks + 1;
                    var old = this.currentFile;
                    this.includeStack.Add(old.GetFilename());
                    this.currentFile = file;
                    try
                    {
                        this.Traverse(file.GetStructure()!);
                    }
                    finally
                    {
                        this.currentFile = old;
                        JsUtils.Pop(this.includeStack);
                    }
                }
            }

            if (isStructure || isStatement)
            {
                this.Traverse(((IStructureOrStatementNode)(((IStructureOrStatementNode)child))));
            }
        }

        if (loop == true)
        {
            this.loopTables.Pop();
        }
    }

    /**
     * if this returns true, then the traversal should continue with next child
     */
    private bool UpdateScopeStructure(StructureNode node)
    {
        var filename = this.currentFile.GetFilename();
        var stru = node.Get();

        if (this.scope.GetType() == ScopeType.SelectionEvent && this.IsSelectionEventBoundaryStructure(stru))
        {
            this.scope.Pop(node.GetFirstToken().GetStart());
        }

        SyntaxInput input = new SyntaxInput { Scope = this.scope, Filename = filename, Issues = this.issues, Deferred = this.deferred };

        if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.ClassDefinition)
        {
            new global::ScipSharp.Indexer.Abap.Types.ClassDefinition(node, input);
            return true;
        }
        else if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.Interface)
        {
            new InterfaceDefinition(node, input);
            return true;
        }
        else if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.Types)
        {
            this.scope.AddType(new global::ScipSharp.Indexer.Abap.Syntax.Structures.Types().RunSyntax(node, input));
            return true;
        }
        else if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.Constants)
        {
            this.scope.AddIdentifier(new global::ScipSharp.Indexer.Abap.Syntax.Structures.Constants().RunSyntax(node, input).type);
            return true;
        }
        else if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.Data)
        {
            this.scope.AddIdentifier(global::ScipSharp.Indexer.Abap.Syntax.Structures.Data.RunSyntax(node, input));
            return true;
        }
        else if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.Statics)
        {
            this.scope.AddIdentifier(new global::ScipSharp.Indexer.Abap.Syntax.Structures.Statics().RunSyntax(node, input));
            return true;
        }
        else if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.TypeMesh)
        {
            var found = new global::ScipSharp.Indexer.Abap.Syntax.Structures.TypeMesh().RunSyntax(node, input);
            if (found != null)
            {
                this.scope.AddType(found);
            }
            return true;
        }
        else if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.TypeEnum)
        {
            var values = new global::ScipSharp.Indexer.Abap.Syntax.Structures.TypeEnum().RunSyntax(node, input).values;
            this.scope.AddList(values);
            return true;
        }
        else if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.TestInjection)
        {
            // todo: skipped for now
            return true;
        }
        else if (stru is global::ScipSharp.Indexer.Abap.Structures.Structures.Cleanup)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Structures.Cleanup.RunSyntax(node, input);
        }
        return false;
    }

    private void UpdateScopeStatement(StatementNode node)
    {
        var filename = this.currentFile.GetFilename();
        var s = node.Get();

        this.UpdateSelectionEventScope(node);

        SyntaxInput input = new SyntaxInput { Scope = this.scope, Filename = filename, Issues = this.issues, Deferred = this.deferred };

        // todo, refactor
        if (s is global::ScipSharp.Indexer.Abap.Statements.Statements.Type)
        {
            this.scope.AddType(new global::ScipSharp.Indexer.Abap.Syntax.Statements.Type().RunSyntax(node, input));
            return;
        }
        else if (s is global::ScipSharp.Indexer.Abap.Statements.Statements.Constant)
        {
            this.scope.AddIdentifier(new global::ScipSharp.Indexer.Abap.Syntax.Statements.Constant().RunSyntax(node, input));
            return;
        }
        else if (s is global::ScipSharp.Indexer.Abap.Statements.Statements.Static)
        {
            this.scope.AddIdentifier(new global::ScipSharp.Indexer.Abap.Syntax.Statements.Static().RunSyntax(node, input));
            return;
        }
        else if (s is global::ScipSharp.Indexer.Abap.Statements.Statements.Data)
        {
            this.scope.AddIdentifier(new global::ScipSharp.Indexer.Abap.Syntax.Statements.Data().RunSyntax(node, input));
            return;
        }

        this.loopTables.Check(node, input);

        var name = s.GetType().Name;
        if ((SyntaxHandlers.Map.GetValueOrDefault(name)) != null)
        {
            SyntaxHandlers.Map.GetValueOrDefault(name).RunSyntax(node, input);
            return;
        }

        if (s is global::ScipSharp.Indexer.Abap.Statements.Statements.FunctionModule)
        {
            this.helpers.proc.FindFunctionScope(this.@object, node, filename);
        }
        else if (s is EndForm || s is EndFunction || s is EndClass || s is EndInterface)
        {
            this.scope.Pop(node.GetLastToken().GetEnd());
        }
        else if (s is EndMethod)
        {
            if (this.scope.GetType() == ScopeType.Method)
            {
                this.scope.Pop(node.GetLastToken().GetEnd());
            }
            if (this.scope.GetType() == ScopeType.MethodInstance)
            {
                this.scope.Pop(node.GetLastToken().GetEnd());
            }
        }
    }

    private void UpdateSelectionEventScope(StatementNode node)
    {
        var statement = node.Get();

        if (this.scope.GetType() == ScopeType.SelectionEvent && this.IsSelectionEventBoundary(statement))
        {
            this.scope.Pop(node.GetFirstToken().GetStart());
        }

        if (this.OpensSelectionEventScope(statement))
        {
            this.scope.Push(ScopeType.SelectionEvent, statement.GetType().Name, node.GetFirstToken().GetStart(), this.currentFile.GetFilename());
        }
    }

    private bool OpensSelectionEventScope(IStatement statement)
    {
        return statement is global::ScipSharp.Indexer.Abap.Statements.Statements.AtSelectionScreen;
    }

    private bool IsSelectionEventBoundary(IStatement statement)
    {
        return Stuff.SELECTION_EVENTS.Any(@event => @event.IsInstanceOfType(statement)) || statement is global::ScipSharp.Indexer.Abap.Statements.Statements.Form || statement is global::ScipSharp.Indexer.Abap.Statements.Statements.Module || statement is global::ScipSharp.Indexer.Abap.Statements.Statements.FunctionModule;
    }

    private bool IsSelectionEventBoundaryStructure(IStructure structure)
    {
        return structure is global::ScipSharp.Indexer.Abap.Structures.Structures.ClassDefinition || structure is global::ScipSharp.Indexer.Abap.Structures.Structures.Interface;
    }
}
