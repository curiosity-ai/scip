// Ported from abaplint packages/core/src/abap/types/class_attributes.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Structures.Structures;
using ScipSharp.Indexer.Abap.Syntax;

namespace ScipSharp.Indexer.Abap.Types;

public class Attributes : IAttributes
{
    private readonly List<ClassAttribute> @static;
    private readonly List<ClassAttribute> instance;
    private readonly List<ClassConstant> constants;
    private readonly TypeDefinitions types;
    private readonly List<(TypedIdentifier type, Visibility visibility)> tlist;
    private readonly string filename;
    private readonly List<Alias> aliases;
    private readonly List<string> declaredInterfaces;
    private readonly List<ClassAttribute> all;
    private readonly Dictionary<string, TypedIdentifier> byName;

    public Attributes(StructureNode node, SyntaxInput input)
    {
        this.@static = new List<ClassAttribute>();
        this.instance = new List<ClassAttribute>();
        this.constants = new List<ClassConstant>();
        this.aliases = new List<Alias>();
        this.declaredInterfaces = new List<string>();
        this.tlist = new List<(TypedIdentifier type, Visibility visibility)>();
        this.filename = input.Filename;
        this.Parse(node, input);
        this.all = this.@static.Concat(this.instance).ToList();
        this.byName = this.BuildByName();
        this.types = new TypeDefinitions(this.tlist.Select(t => new TypeDefinitionsEntry { Type = t.type, Visibility = t.visibility }).ToList());
    }

    public TypeDefinitions GetTypes()
    {
        return this.types;
    }

    public List<ClassAttribute> GetStatic()
    {
        return this.@static;
    }

    public List<Alias> GetAliases()
    {
        return this.aliases;
    }

    public List<ClassAttribute> GetAll()
    {
        return this.all;
    }

    public List<ClassAttribute> GetStaticsByVisibility(Visibility visibility)
    {
        List<ClassAttribute> attributes = new List<ClassAttribute>();
        foreach (var attr in this.@static)
        {
            if (attr.GetVisibility() == visibility)
            {
                attributes.Add(attr);
            }
        }
        return attributes;
    }

    public List<ClassAttribute> GetInstance()
    {
        return this.instance;
    }

    public List<ClassAttribute> GetInstancesByVisibility(Visibility visibility)
    {
        List<ClassAttribute> attributes = new List<ClassAttribute>();
        foreach (var attr in this.instance)
        {
            if (attr.GetVisibility() == visibility)
            {
                attributes.Add(attr);
            }
        }
        return attributes;
    }

    public List<ClassConstant> GetConstants()
    {
        return this.constants;
    }

    public List<ClassConstant> GetConstantsByVisibility(Visibility visibility)
    {
        List<ClassConstant> attributes = new List<ClassConstant>();
        foreach (var attr in this.constants)
        {
            if (attr.GetVisibility() == visibility)
            {
                attributes.Add(attr);
            }
        }
        return attributes;
    }

    public TypedIdentifier? FindByName(string name)
    {
        return this.byName.GetValueOrDefault(JsUtils.ToUpper(name));
    }

    /////////////////////////////
    private Dictionary<string, TypedIdentifier> BuildByName()
    {
        Dictionary<string, TypedIdentifier> ret = new Dictionary<string, TypedIdentifier>();

        foreach (var a in this.@static)
        {
            ret[JsUtils.ToUpper(a.GetName())] = a;
        }
        foreach (var a in this.instance)
        {
            var name = JsUtils.ToUpper(a.GetName());
            if (ret.GetValueOrDefault(name) == null)
            {
                ret[name] = a;
            }
        }
        foreach (var a in this.constants)
        {
            var name = JsUtils.ToUpper(a.GetName());
            if (ret.GetValueOrDefault(name) == null)
            {
                ret[name] = a;
            }
        }

        return ret;
    }

    private void Parse(StructureNode node, SyntaxInput input)
    {
        var cdef = node.FindDirectStructure<global::ScipSharp.Indexer.Abap.Structures.Structures.ClassDefinition>();
        if (cdef != null)
        {
            foreach (var i in cdef.FindAllStatements<InterfaceDef>())
            {
                var name = i.FindFirstExpression<InterfaceName>()?.GetFirstToken().GetStr();
                if (!string.IsNullOrEmpty(name))
                {
                    this.declaredInterfaces.Add(JsUtils.ToUpper(name));
                }
            }
            this.ParseSection(cdef.FindDirectStructure<PublicSection>(), Visibility.Public, input);
            this.ParseSection(cdef.FindDirectStructure<ProtectedSection>(), Visibility.Protected, input);
            this.ParseSection(cdef.FindDirectStructure<PrivateSection>(), Visibility.Private, input);
            return;
        }

        var idef = node.FindDirectStructure<global::ScipSharp.Indexer.Abap.Structures.Structures.Interface>();
        if (idef != null)
        {
            foreach (var i in idef.FindAllStatements<InterfaceDef>())
            {
                var name = i.FindFirstExpression<InterfaceName>()?.GetFirstToken().GetStr();
                if (!string.IsNullOrEmpty(name))
                {
                    this.declaredInterfaces.Add(JsUtils.ToUpper(name));
                }
            }
            this.ParseSection(idef.FindDirectStructure<SectionContents>(), Visibility.Public, input);
            return;
        }

        throw new Exception("MethodDefinition, expected ClassDefinition or InterfaceDefinition");
    }

    private void ParseSection(StructureNode? node, Visibility visibility, SyntaxInput input)
    {
        if (node == null)
        {
            return;
        }

        foreach (var c in node.GetChildren())
        {
            var ctyp = c.Get();
            // not in abaplint: the declaration being read, for a run watched from another thread
            global::ScipSharp.Indexer.Abap.Statements.StatementParser.Current = c is StructureNode block ? block.FindAllStatementNodes().SelectMany(x => x.GetTokens()).ToList() : (c as StatementNode)?.GetTokens();
            if (c is StructureNode)
            {
                if (ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Data)
                {
                    var found = global::ScipSharp.Indexer.Abap.Syntax.Structures.Data.RunSyntax(((StructureNode)c), input);
                    if (found != null)
                    {
                        var attr = new ClassAttribute(found, visibility, found.GetMeta(), found.GetValue());
                        this.instance.Add(attr);
                        input.Scope.AddIdentifier(attr);
                    }
                }
                else if (ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.ClassData)
                {
                    var found = new global::ScipSharp.Indexer.Abap.Syntax.Structures.ClassData().RunSyntax(((StructureNode)c), input);
                    if (found != null)
                    {
                        var attr = new ClassAttribute(found, visibility, found.GetMeta(), found.GetValue());
                        this.@static.Add(attr);
                        input.Scope.AddIdentifier(attr);
                    }
                }
                else if (ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Constants)
                {
                    var __d0 = new global::ScipSharp.Indexer.Abap.Syntax.Structures.Constants().RunSyntax(((StructureNode)c), input);
                    var found = __d0.type;
                    var values = __d0.values;
                    if (found != null)
                    {
                        var attr = new ClassConstant(found, visibility, values);
                        this.constants.Add(attr);
                        input.Scope.AddIdentifier(attr);
                    }
                }
                else if (ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.TypeEnum)
                {
                    var __d1 = new global::ScipSharp.Indexer.Abap.Syntax.Structures.TypeEnum().RunSyntax(((StructureNode)c), input);
                    var values = __d1.values;
                    var types = __d1.types;
                    foreach (var v in values)
                    {
                        // for now add ENUM values as constants
                        var attr = new ClassConstant(v, visibility, "novalueClassAttributeEnum");
                        this.constants.Add(attr);
                        input.Scope.AddIdentifier(attr);
                    }
                    foreach (var t in types)
                    {
                        this.tlist.Add((t, visibility));
                        //            scope.addIdentifier(attr);
                    }
                }
                else if (ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.TypeMesh)
                {
                    var res = new global::ScipSharp.Indexer.Abap.Syntax.Structures.TypeMesh().RunSyntax(((StructureNode)c), input);
                    if (res != null)
                    {
                        input.Scope.AddType(res);
                        this.tlist.Add((res, visibility));
                    }
                }
                else if (ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Types)
                {
                    var res = new global::ScipSharp.Indexer.Abap.Syntax.Structures.Types().RunSyntax(((StructureNode)c), input);
                    if (res != null)
                    {
                        input.Scope.AddType(res);
                        this.tlist.Add((res, visibility));
                    }
                }
                else
                {
                    // begin recursion
                    this.ParseSection(((StructureNode)c), visibility, input);
                }
            }
            else if (c is StatementNode)
            {
                if (ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Data)
                {
                    this.instance.Add(this.ParseAttribute(((StatementNode)c), visibility, input));
                }
                else if (ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.ClassData)
                {
                    this.@static.Add(this.ParseAttribute(((StatementNode)c), visibility, input));
                }
                else if (ctyp is Aliases)
                {
                    this.ParseAlias(((StatementNode)c), visibility, input);
                }
                else if (ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Constant)
                {
                    var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Constant().RunSyntax(((StatementNode)c), input);
                    if (found != null)
                    {
                        var attr = new ClassConstant(found, visibility, found.GetValue());
                        this.constants.Add(attr);
                        input.Scope.AddIdentifier(attr);
                    }
                }
                else if (ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Type)
                {
                    var res = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Type().RunSyntax(((StatementNode)c), input);
                    if (res != null)
                    {
                        input.Scope.AddType(res);
                        this.tlist.Add((res, visibility));
                    }
                }
            }
        }
    }

    private void ParseAlias(StatementNode node, Visibility visibility, SyntaxInput input)
    {
        var aliasName = node.FindFirstExpression<SimpleName>()!.GetFirstToken();
        var compToken = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Field>()!.GetFirstToken();
        var compName = compToken.GetStr();

        this.aliases.Add(new Alias(aliasName, visibility, compName, this.filename));

        if (compName.Contains("~", StringComparison.Ordinal))
        {
            var name = compName.Split("~").ToList().ElementAtOrDefault(0);
            var idef = input.Scope.FindInterfaceDefinition(name);
            if (idef != null)
            {
                input.Scope.AddReference(compToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)idef), ReferenceType.ObjectOrientedReference, input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(name), OoType = "INTF" });

                var foundType = idef.GetTypeDefinitions()!.GetByName(compName.Split("~").ToList().ElementAtOrDefault(1));
                if (foundType != null)
                {
                    input.Scope.AddTypeNamed(aliasName.GetStr(), foundType);
                }

                var foundAttribute = idef.GetAttributes().FindByName(compName.Split("~").ToList().ElementAtOrDefault(1));
                if (foundAttribute != null)
                {
                    input.Scope.AddNamedIdentifier(aliasName.GetStr(), foundAttribute);
                }

                var component = compName.Split("~").ToList().ElementAtOrDefault(1);
                if (foundType == null && foundAttribute == null && this.ExistsInInterface(idef, component, input, new List<string>()) == false)
                {
                    var message = "Component " + component + " not found in interface " + idef.GetName();
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, compToken, message));
                }
            }
            else if (this.declaredInterfaces.Contains(JsUtils.ToUpper(name)) || input.Scope.GetDDIC().InErrorNamespace(name) == false)
            {
                input.Scope.AddReference(compToken, null, ReferenceType.ObjectOrientedVoidReference, input.Filename);
            }
            else
            {
                throw new Exception("Interface " + name + " not found");
            }
        }
    }

    /** does the component exist in the interface, or in one of its aliases or included interfaces? */
    private bool ExistsInInterface(IInterfaceDefinition idef, string component, SyntaxInput input, List<string> visited)
    {
        var upperName = JsUtils.ToUpper(idef.GetName());
        if (visited.Contains(upperName))
        {
            return false;
        }
        visited.Add(upperName);

        var upper = JsUtils.ToUpper(component);
        if (idef.GetTypeDefinitions().GetByName(component) != null || idef.GetAttributes().FindByName(component) != null || idef.GetMethodDefinitions().GetByName(component) != null || idef.GetEvents().Any(e => JsUtils.ToUpper(e.GetName()) == upper) || idef.GetAliases().Any(a => JsUtils.ToUpper(a.GetName()) == upper))
        {
            return true;
        }

        foreach (var i in idef.GetImplementing())
        {
            var sub = input.Scope.FindInterfaceDefinition(i.Name);
            if (sub == null)
            {
                // voided or not found, assume the component exists
                return true;
            }
            else if (this.ExistsInInterface(sub, component, input, visited))
            {
                return true;
            }
        }

        return false;
    }

    private ClassAttribute ParseAttribute(StatementNode node, Visibility visibility, SyntaxInput input)
    {
        TypedIdentifier? found = null;
        var s = node.Get();

        if (s is global::ScipSharp.Indexer.Abap.Statements.Statements.Data)
        {
            found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Data().RunSyntax(node, input);
        }
        else if (s is global::ScipSharp.Indexer.Abap.Statements.Statements.ClassData)
        {
            found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.ClassData().RunSyntax(node, input);
        }
        else
        {
            throw new Exception("ClassAttribute, unexpected node, 1, " + this.filename);
        }

        if (found == null)
        {
            throw new Exception("ClassAttribute, unexpected node, " + this.filename);
        }

        input.Scope.AddIdentifier(found);

        return new ClassAttribute(found, visibility, found.GetMeta(), found.GetValue());
    }

    ITypeDefinitions IAttributes.GetTypes() => GetTypes();
}
