// Ported from abaplint packages/core/src/abap/types/interface_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Syntax;

namespace ScipSharp.Indexer.Abap.Types;

public class InterfaceDefinition : global::ScipSharp.Indexer.Abap.FileInformation.Identifier, IInterfaceDefinition
{
    private IAttributes attributes;
    private readonly List<IImplementing> implementing;
    private ITypeDefinitions typeDefinitions;
    private IMethodDefinitions methodDefinitions;
    private readonly List<IEventDefinition> events;
    private readonly bool globalValue;
    private List<Alias> aliases;

    // TS checks the node and finds the name before calling super()
    private static AbstractToken NameToken(StructureNode node)
    {
        if (!((node.Get() is global::ScipSharp.Indexer.Abap.Structures.Structures.Interface)))
        {
            throw new Exception("InterfaceDefinition, unexpected node type");
        }
        return node.FindFirstStatement<global::ScipSharp.Indexer.Abap.Statements.Statements.Interface>()!.FindFirstExpression<InterfaceName>()!.GetFirstToken();
    }

    public InterfaceDefinition(StructureNode node, SyntaxInput input)
        : base(NameToken(node), input.Filename)
    {
        var name = this.GetToken();
        input.Scope.AddInterfaceDefinition(this);

        this.events = new List<IEventDefinition>();
        this.implementing = new List<IImplementing>();
        this.globalValue = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ClassGlobal>() != null;

        input.Scope.Push(ScopeType.Interface, name.GetStr(), node.GetFirstToken().GetStart(), input.Filename);
        this.Parse(input, node);
        input.Scope.Pop(node.GetLastToken().GetEnd());

        // perform checks after everything has been initialized
        this.CheckMethodNameLength(input);
    }

    public string? GetSuperClass()
    {
        return null;
    }

    public List<IImplementing> GetImplementing()
    {
        return this.implementing;
    }

    public List<Alias> GetAliases()
    {
        return this.aliases;
    }

    public List<IEventDefinition> GetEvents()
    {
        return this.events;
    }

    public IAttributes GetAttributes()
    {
        return this.attributes;
    }

    public ITypeDefinitions GetTypeDefinitions()
    {
        return this.typeDefinitions;
    }

    public bool IsLocal()
    {
        return !this.globalValue;
    }

    public bool IsGlobal()
    {
        return this.globalValue;
    }

    public IMethodDefinitions GetMethodDefinitions()
    {
        return this.methodDefinitions;
    }

    /////////////////
    private void CheckMethodNameLength(SyntaxInput input)
    {
        foreach (var m in this.methodDefinitions.GetAll())
        {
            if (m.GetName().Length > 30)
            {
                var message = $"Method name \"{m.GetName()}\" is too long, maximum length is 30 characters";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, m.GetToken(), message));
            }
        }
    }

    private void CheckInterfacesExists(SyntaxInput input, StructureNode node)
    {
        foreach (var i in node.FindAllStatements<InterfaceDef>())
        {
            var token = i.FindDirectExpression<InterfaceName>()?.GetFirstToken();
            var name = token?.GetStr();
            if (!string.IsNullOrEmpty(name))
            {
                this.implementing.Add(new IImplementing { Name = name, Partial = false });

                var idef = input.Scope.FindInterfaceDefinition(name);
                if (idef != null)
                {
                    input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)idef), ReferenceType.ObjectOrientedReference, this.filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "INTF" });
                }
                else if (input.Scope.GetDDIC().InErrorNamespace(name) == false)
                {
                    input.Scope.AddReference(token, null, ReferenceType.ObjectOrientedVoidReference, this.filename);
                }
                else
                {
                    throw new Exception("Interface " + name + " unknown");
                }
            }
        }
    }

    private void Parse(SyntaxInput input, StructureNode node)
    {
        this.CheckInterfacesExists(input, node);

        var helper = new ObjectOriented(input.Scope);
        helper.FromInterfaces(this);

        // todo, proper sequencing, the statements should be processed line by line
        this.attributes = new Attributes(node, input);
        this.typeDefinitions = this.attributes.GetTypes();

        this.aliases = this.attributes.GetAliases();

        var events = node.FindAllStatements<Events>();
        foreach (var e in events)
        {
            var eventName = JsUtils.ToUpper(e.FindDirectExpression<EventName>()?.ConcatTokens());
            if (this.events.FirstOrDefault(ev => JsUtils.ToUpper(ev.GetName()) == eventName) != null)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, e.GetFirstToken(), "Event " + JsUtils.Str(eventName) + " already defined"));
                continue;
            }
            this.events.Add(new EventDefinition(e, Visibility.Public, input));
        }

        this.methodDefinitions = new MethodDefinitions(node, input);
        if (this.methodDefinitions.GetByName("CONSTRUCTOR") != null)
        {
            var constructor = this.methodDefinitions.GetByName("CONSTRUCTOR")!;
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, constructor.GetToken(), "Interfaces cannot have constructor methods"));
        }
    }

}
