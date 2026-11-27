// Ported from abaplint packages/core/src/abap/types/class_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Structures.Structures;
using ScipSharp.Indexer.Abap.Syntax;

namespace ScipSharp.Indexer.Abap.Types;

public class ClassDefinition : global::ScipSharp.Indexer.Abap.FileInformation.Identifier, IClassDefinition
{
    private readonly MethodDefinitions methodDefs;
    private readonly TypeDefinitions types;
    private readonly Attributes attributes;
    private readonly List<IEventDefinition> events;
    private readonly List<string> friends;
    private readonly string? superClass;
    private readonly List<IImplementing> implementing;
    private readonly bool testing;
    private readonly bool @abstract;
    private readonly bool finalValue;
    private readonly bool globalValue;
    private readonly bool sharedMemory;
    private readonly List<Alias> aliases;
    private readonly Visibility createVisibilityValue;

    // TS checks the node and finds the name before calling super()
    private static AbstractToken NameToken(StructureNode node)
    {
        if (!((node.Get() is global::ScipSharp.Indexer.Abap.Structures.Structures.ClassDefinition)))
        {
            throw new Exception("ClassDefinition, unexpected node type");
        }
        var def = node.FindFirstStatement<global::ScipSharp.Indexer.Abap.Statements.Statements.ClassDefinition>();
        return def!.FindDirectExpression<ClassName>()!.GetFirstToken();
    }

    /// <summary>Not in abaplint. The part of a class definition being built right now; null outside.</summary>
    public static volatile string? Step;

    public ClassDefinition(StructureNode node, SyntaxInput input)
        : base(NameToken(node), input.Filename)
    {
        var def = node.FindFirstStatement<global::ScipSharp.Indexer.Abap.Statements.Statements.ClassDefinition>();
        var name = this.GetToken();
        input.Scope.AddClassDefinition(this);

        this.events = new List<IEventDefinition>();
        this.implementing = new List<IImplementing>();
        this.globalValue = def!.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ClassGlobal>() != null;
        this.finalValue = def!.FindFirstExpression<ClassFinal>() != null;

        input.Scope.Push(ScopeType.ClassDefinition, name.GetStr(), name.GetStart(), input.Filename);

        // not in abaplint: Step says which part of the definition is being built, for a run watched from another thread
        Step = "super class and friends";
        this.superClass = this.FindSuper(def, input);
        this.friends = this.FindFriends(def, input);

        Step = "interfaces";
        this.Parse(input, node);

        Step = "what the super classes and interfaces bring";
        var helper = new ObjectOriented(input.Scope);
        helper.FromSuperClassesAndInterfaces(this);

        Step = "attributes, constants and types";
        this.attributes = new Attributes(node, input);
        this.types = this.attributes.GetTypes();
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
            // todo, all these are not Public
        }

        Step = "method declarations";
        this.methodDefs = new MethodDefinitions(node, input);
        Step = null;
        global::ScipSharp.Indexer.Abap.Statements.StatementParser.Current = null;

        input.Scope.Pop(node.GetLastToken().GetEnd());

        var concat = JsUtils.ToUpper(def!.ConcatTokens());

        this.testing = concat.Contains(" FOR TESTING", StringComparison.Ordinal);
        this.sharedMemory = concat.Contains(" SHARED MEMORY ENABLED", StringComparison.Ordinal);
        this.@abstract = def?.FindDirectTokenByText("ABSTRACT") != null;
        if (concat.Contains(" CREATE PRIVATE", StringComparison.Ordinal))
        {
            this.createVisibilityValue = Visibility.Private;
        }
        else if (concat.Contains(" CREATE PROTECTED", StringComparison.Ordinal))
        {
            this.createVisibilityValue = Visibility.Protected;
        }
        else
        {
            this.createVisibilityValue = Visibility.Public;
        }

        // perform checks after everything has been initialized
        this.CheckInterfaceVisibility(input, node);
        this.CheckMethodsFromSuperClasses(input);
        this.CheckMethodNameConflicts(input);
        this.CheckEventNameConflicts(input);
        this.CheckClassNameLength(input);
        this.CheckMethodNameLength(input);
        this.CheckClassConstructor(input);
    }

    public List<string> GetFriends()
    {
        return this.friends;
    }

    public List<IEventDefinition> GetEvents()
    {
        return this.events;
    }

    public IMethodDefinitions GetMethodDefinitions()
    {
        return this.methodDefs;
    }

    public TypeDefinitions GetTypeDefinitions()
    {
        return this.types;
    }

    public string? GetSuperClass()
    {
        return this.superClass;
    }

    public Attributes GetAttributes()
    {
        return this.attributes;
    }

    public bool IsGlobal()
    {
        return this.globalValue;
    }

    public bool IsFinal()
    {
        return this.finalValue;
    }

    public List<IImplementing> GetImplementing()
    {
        return this.implementing;
    }

    public List<Alias> GetAliases()
    {
        return this.aliases;
    }

    public bool IsForTesting()
    {
        return this.testing;
    }

    public bool IsAbstract()
    {
        return this.@abstract;
    }

    public bool IsSharedMemory()
    {
        return this.sharedMemory;
    }

    public Visibility GetCreateVisibility()
    {
        return this.createVisibilityValue;
    }

    /*
    public getEvents() {
    }
     */
    ///////////////////
    private void CheckClassNameLength(SyntaxInput input)
    {
        if (this.GetName().Length > 30)
        {
            var message = $"Class name \"{this.GetName()}\" is too long, maximum length is 30 characters";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, this.GetToken(), message));
        }
    }

    private string? FindSuper(StatementNode? def, SyntaxInput input)
    {
        var token = def?.FindDirectExpression<SuperClassName>()?.GetFirstToken();
        this.AddReference(token, input);
        var name = token?.GetStr();
        return name;
    }

    private void CheckMethodNameLength(SyntaxInput input)
    {
        foreach (var m in this.methodDefs.GetAll())
        {
            if (m.GetName().Length > 30 && m.GetName().Contains("~", StringComparison.Ordinal) == false)
            {
                var message = $"Method name \"{m.GetName()}\" is too long, maximum length is 30 characters";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, m.GetToken(), message));
            }
        }
    }

    private void CheckClassConstructor(SyntaxInput input)
    {
        foreach (var m in this.methodDefs.GetAll())
        {
            if (JsUtils.ToUpper(m.GetName()) != "CLASS_CONSTRUCTOR")
            {
                continue;
            }
            if (m.IsStatic() == false)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, m.GetToken(), "CLASS_CONSTRUCTOR must be static"));
            }
            else if (m.GetVisibility() != Visibility.Public)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, m.GetToken(), "CLASS_CONSTRUCTOR must be declared in the public section"));
            }
        }
    }

    private void CheckInterfaceVisibility(SyntaxInput input, StructureNode node)
    {
        List<StructureNode?> sections = new List<StructureNode?> { node.FindDirectStructure<ProtectedSection>(), node.FindDirectStructure<PrivateSection>() };
        foreach (var section in sections)
        {
            foreach (var statement in section?.FindAllStatements<InterfaceDef>() ?? new List<StatementNode>())
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, statement.GetFirstToken(), "INTERFACES can only be declared in public sections"));
            }
        }
    }

    private void CheckMethodsFromSuperClasses(SyntaxInput input)
    {
        var scope = input.Scope;
        string? sup = this.GetSuperClass();
        HashSet<string> names = new HashSet<string>();
        Dictionary<string, Visibility> methods = new Dictionary<string, Visibility>();

        while (sup != null)
        {
            var cdef = scope.FindClassDefinition(sup);
            foreach (var m in cdef?.GetMethodDefinitions()?.GetAll() ?? new List<IMethodDefinition>())
            {
                var name = JsUtils.ToUpper(m.GetName());
                if (m.GetVisibility() == Visibility.Private)
                {
                    continue;
                }
                else if (name == "CONSTRUCTOR" || name == "DESTRUCTOR" || name == "CLASS_CONSTRUCTOR")
                {
                    continue;
                }
                names.Add(name);
                if (methods.ContainsKey(name) == false)
                {
                    methods[name] = m.GetVisibility();
                }
            }
            foreach (var a in cdef?.GetAliases() ?? new List<Alias>())
            {
                names.Add(JsUtils.ToUpper(a.GetName()));
            }
            sup = cdef?.GetSuperClass();
        }

        foreach (var m in this.GetMethodDefinitions().GetAll())
        {
            if (names.Contains(JsUtils.ToUpper(m.GetName())) && m.IsRedefinition() == false)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, m.GetToken(), $"{JsUtils.ToUpper(m.GetName())} already declared in superclass"));
            }
            Visibility? superVisibility = methods.TryGetValue(JsUtils.ToUpper(m.GetName()), out var sv) ? sv : null;
            if (m.IsRedefinition() == true && superVisibility != null && m.GetVisibility() != superVisibility.Value)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, m.GetToken(), $"{JsUtils.ToUpper(m.GetName())} redefinition visibility cannot be changed"));
            }
        }
    }

    private void CheckMethodNameConflicts(SyntaxInput input)
    {
        foreach (var m in this.methodDefs.GetAll())
        {
            if (this.attributes.FindByName(m.GetName()) != null || this.types.GetByName(m.GetName()) != null)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, m.GetToken(), $"\"{m.GetName()}\" already defined"));
            }
        }
    }

    private void CheckEventNameConflicts(SyntaxInput input)
    {
        foreach (var e in this.events)
        {
            if (this.attributes.FindByName(e.GetName()) != null || this.types.GetByName(e.GetName()) != null || this.methodDefs.GetByName(e.GetName()) != null)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, (((EventDefinition)e)).GetToken(), $"\"{e.GetName()}\" already defined"));
            }
        }
    }

    private List<string> FindFriends(StatementNode? def, SyntaxInput input)
    {
        List<string> result = new List<string>();
        foreach (var n in def?.FindDirectExpression<ClassFriends>()?.FindDirectExpressions<ClassName>() ?? new List<ExpressionNode>())
        {
            var token = n.GetFirstToken();
            var name = token.GetStr();
            var s = input.Scope.FindClassDefinition(name);
            if (s != null)
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)s), ReferenceType.ObjectOrientedReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "CLAS" });
            }
            else if (input.Scope.ExistsObject(name) != null)
            {
                // DEFINITION DEFERRED friend
            }
            else if (input.Scope.GetDDIC().InErrorNamespace(name) == false)
            {
                input.Scope.AddReference(token, null, ReferenceType.ObjectOrientedVoidReference, input.Filename);
            }
            else
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, token, "Friend class " + JsUtils.ToUpper(name) + " does not exist"));
            }
            result.Add(name);
        }
        return result;
    }

    private void AddReference(AbstractToken? token, SyntaxInput input)
    {
        var name = token?.GetStr();
        if (!string.IsNullOrEmpty(name))
        {
            var s = input.Scope.FindClassDefinition(name);
            if (s != null)
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)s), ReferenceType.ObjectOrientedReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "CLAS" });
            }
            else if (input.Scope.GetDDIC().InErrorNamespace(name) == false)
            {
                input.Scope.AddReference(token, null, ReferenceType.ObjectOrientedVoidReference, input.Filename);
            }
        }
    }

    private void Parse(SyntaxInput input, StructureNode inputNode)
    {
        foreach (var node in inputNode.FindAllStatements<InterfaceDef>())
        {
            var partial = node.FindDirectTokenByText("PARTIALLY") != null;
            var token = node.FindFirstExpression<InterfaceName>()?.GetFirstToken();
            if (token == null)
            {
                throw new Exception("ClassDefinition, unable to find interface token");
            }
            var name = JsUtils.ToUpper(token.GetStr());
            this.implementing.Add(new IImplementing { Name = name, Partial = partial });

            var intf = input.Scope.FindInterfaceDefinition(name);
            if (intf != null)
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)intf), ReferenceType.ObjectOrientedReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "INTF" });
            }
            else if (input.Scope.GetDDIC().InErrorNamespace(name) == false)
            {
                input.Scope.AddReference(token, null, ReferenceType.ObjectOrientedVoidReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "INTF" });
            }
            else
            {
                input.Scope.AddReference(token, null, ReferenceType.ObjectOrientedUnknownReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "INTF" });
            }
        }
    }

    ITypeDefinitions IInterfaceDefinition.GetTypeDefinitions() => GetTypeDefinitions();
    IAttributes IInterfaceDefinition.GetAttributes() => GetAttributes();
}
