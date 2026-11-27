// Ported from abaplint packages/core/src/abap/5_syntax/_current_scope.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Objects;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax;

public class CurrentScope
{
    protected readonly IRegistry reg;
    protected SpaghettiScopeNode? current;
    protected string? allowHeaderUse;
    protected IObject parentObj;
    private readonly Dictionary<string, List<string>> localFriends = new Dictionary<string, List<string>>();

    public static CurrentScope BuildDefault(IRegistry reg, IObject obj)
    {
        var s = new CurrentScope(reg, obj);

        s.Push(ScopeType.BuiltIn, ScopeType.BuiltIn, new Position(1, 1), BuiltIn.filename);
        CurrentScope.AddBuiltIn(s, reg.GetConfig().GetSyntaxSetttings().GlobalConstants!);

        string name = ScopeType.Global;
        if (obj != null)
        {
            name = name + "_" + obj.GetName();
        }

        s.Push(ScopeType.Global, name, new Position(1, 1), name);

        return s;
    }

    private static void AddBuiltIn(CurrentScope s, List<string> extras)
    {
        var b = new BuiltIn();
        var builtin = b.Get(extras);
        s.AddList(builtin);
        foreach (var t in b.GetTypes())
        {
            s.AddType(t);
        }
    }

    private CurrentScope(IRegistry reg, IObject obj)
    {
        this.current = null;
        this.parentObj = obj;
        this.reg = reg;
    }

    ///////////////////////////
    public ABAPRelease GetRelease()
    {
        return this.reg.GetConfig().GetRelease();
    }

    public Version GetVersion()
    {
        return this.reg.GetConfig().GetVersion();
    }

    public LanguageVersion GetLanguageVersion()
    {
        return this.reg.GetConfig().GetLanguageVersion();
    }

    public bool GetOpenABAP()
    {
        return this.reg.GetConfig().IsOpenABAP();
    }

    public IRegistry GetRegistry()
    {
        return this.reg;
    }

    public void AddType(TypedIdentifier? type)
    {
        if (type == null)
        {
            return;
        }
        this.AddTypeNamed(type.GetName(), type);
    }

    public void AddTypeNamed(string name, TypedIdentifier? type)
    {
        if (type == null)
        {
            return;
        }
        if (this.current == null)
        {
            return;
        }
        var upper = JsUtils.ToUpper(name);
        if (this.current.GetData().Types.GetValueOrDefault(upper) != null)
        {
            throw new Exception($"Type name \"{name}\" already defined");
        }
        else if (this.IsOO() && this.current.GetData().Vars.GetValueOrDefault(upper) != null)
        {
            throw new Exception($"\"{name}\" already defined");
        }
        this.current.GetData().Types[upper] = type;
    }

    public void AddExtraLikeType(TypedIdentifier? type)
    {
        if (type == null)
        {
            return;
        }
        this.AddExtraLikeTypeNamed(type.GetName(), type);
    }

    public void AddExtraLikeTypeNamed(string name, TypedIdentifier? type)
    {
        if (type == null)
        {
            return;
        }
        if (this.current == null)
        {
            return;
        }
        var upper = JsUtils.ToUpper(name);
        if (this.current.GetData().ExtraLikeTypes.GetValueOrDefault(upper) != null)
        {
            throw new Exception($"Type name \"{name}\" already defined");
        }
        this.current.GetData().ExtraLikeTypes[upper] = type;
    }

    public void AddClassDefinition(IClassDefinition c)
    {
        if (this.current == null)
        {
            return;
        }
        var name = JsUtils.ToUpper(c.GetName());
        if (this.current.GetData().Cdefs.GetValueOrDefault(name) != null)
        {
            throw new Exception($"Class \"{name}\" already defined");
        }
        this.current.GetData().Cdefs[name] = c;
    }

    public void AddFormDefinitions(List<IFormDefinition> f)
    {
        if (this.current == null)
        {
            return;
        }
        this.current.GetData().Forms.AddRange(f);
    }

    /** The FORM definitions are built up front, at that point the program level types are not known.
     * When the FORM statement is traversed it is replaced with one resolved in the scope of the FORM */
    public void UpdateFormDefinition(IFormDefinition f)
    {
        SpaghettiScopeNode? search = this.current;
        while (search != null)
        {
            var forms = search.GetData().Forms;
            for (var i = 0; i < forms.Count; i++)
            {
                if (forms[i].Equals(((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)f)))
                {
                    forms[i] = f;
                    return;
                }
            }
            search = search.GetParent();
        }
    }

    public void AddInterfaceDefinition(IInterfaceDefinition i)
    {
        if (this.current == null)
        {
            return;
        }
        var name = JsUtils.ToUpper(i.GetName());
        if (this.current.GetData().Cdefs.GetValueOrDefault(name) != null)
        {
            throw new Exception($"Interface \"{name}\" already defined");
        }
        this.current.GetData().Idefs[name] = i;
    }

    public void AddNamedIdentifier(string name, TypedIdentifier identifier)
    {
        if (this.current == null)
        {
            return;
        }
        var upper = JsUtils.ToUpper(name);
        if (this.current.GetData().Vars.GetValueOrDefault(upper) != null)
        {
            //      console.dir(new Error().stack);
            throw new Exception($"Variable name \"{name}\" already defined");
        }
        else if (this.IsOO() && this.current.GetData().Types.GetValueOrDefault(upper) != null)
        {
            throw new Exception($"\"{name}\" already defined");
        }
        this.current.GetData().Vars[upper] = identifier;
    }

    public void AddNamedIdentifierToParent(string name, TypedIdentifier identifier)
    {
        if (this.current == null)
        {
            return;
        }
        var parent = this.current.GetParent();
        if (parent == null)
        {
            return;
        }

        var upper = JsUtils.ToUpper(name);
        if (parent.GetData().Vars.GetValueOrDefault(upper) != null)
        {
            //      console.dir(new Error().stack);
            throw new Exception($"Variable name \"{name}\" already defined");
        }
        else if (this.IsOO() && parent.GetData().Types.GetValueOrDefault(upper) != null)
        {
            throw new Exception($"\"{name}\" already defined");
        }
        parent.GetData().Vars[upper] = identifier;
    }

    public void AddIdentifier(TypedIdentifier? identifier)
    {
        if (identifier == null)
        {
            return;
        }
        this.AddNamedIdentifier(identifier.GetName(), identifier);
    }

    public void AddDeferred(AbstractToken? token, string type)
    {
        if (token == null)
        {
            return;
        }
        this.current!.GetData().Deferred[JsUtils.ToUpper(token.GetStr())] = new DeferredInformation { Token = token, OoType = type };
    }

    public void AddListPrefix(List<TypedIdentifier> identifiers, string prefix)
    {
        foreach (var id in identifiers)
        {
            this.AddNamedIdentifier(prefix + id.GetName(), id);
        }
    }

    public void AddList(List<TypedIdentifier> identifiers)
    {
        foreach (var id in identifiers)
        {
            this.AddIdentifier(id);
        }
    }

    public void AddReference(AbstractToken? usage, global::ScipSharp.Indexer.Abap.FileInformation.Identifier? referencing, object? type, string filename, IReferenceExtras? extra = null)
    {

        if (usage == null || type == null)
        {
            return;
        }

        var position = new global::ScipSharp.Indexer.Abap.FileInformation.Identifier(usage, filename);
        if ((type is System.Collections.IList))
        {
            foreach (var t in ((List<string>)type))
            {
                this.current?.GetData().References.Add(new IReference { Position = position, Resolved = referencing, ReferenceType = t, Extra = extra });
            }
        }
        else
        {
            this.current?.GetData().References.Add(new IReference { Position = position, Resolved = referencing, ReferenceType = ((string)type), Extra = extra });
        }
    }

    public void AddSQLConversion(string fieldName, string message, AbstractToken token)
    {
        this.current?.GetData().SqlConversion.Add((fieldName, message, token));
    }

    ///////////////////////////
    public FunctionModuleDefinition? FindFunctionModule(string? name)
    {
        if (name == null)
        {
            return null;
        }
        foreach (var fugr in this.reg.GetObjectsByType("FUGR"))
        {
            var func = (((FunctionGroup)fugr)).GetModule(name);
            if (func != null)
            {
                return func;
            }
        }
        return null;
    }

    public IInterfaceDefinition? FindObjectDefinition(string? name)
    {
        if (name == null)
        {
            return null;
        }
        var clas = this.FindClassDefinition(name);
        if (clas != null)
        {
            return ((IInterfaceDefinition?)clas);
        }
        var intf = this.FindInterfaceDefinition(name);
        if (intf != null)
        {
            return intf;
        }
        return null;
    }

    /** the BDEF from "FOR BEHAVIOR OF" of the enclosing global class, undefined if not a behavior pool */
    public BehaviorDefinition? FindBehaviorDefinition()
    {
        if (this.parentObj.GetType() != "CLAS")
        {
            return null;
        }
        var name = (((Class)this.parentObj)).GetClassDefinition()?.BehaviorDefinitionName;
        if (name == null)
        {
            return null;
        }
        return ((BehaviorDefinition?)(this.reg.GetObject("BDEF", name)));
    }

    public bool IsBadiDef(string name)
    {
        var upper = JsUtils.ToUpper(name);
        foreach (var enhs in this.reg.GetObjectsByType("ENHS"))
        {
            foreach (var def in (((EnhancementSpot)enhs)).ListBadiDefinitions())
            {
                if (JsUtils.ToUpper(def.Name) == upper)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public (global::ScipSharp.Indexer.Abap.FileInformation.Identifier? id, string? ooType, string? RTTIName)? ExistsObject(string? name)
    {
        if (name == null)
        {
            return null;
        }

        string prefixRTTI = "";
        if (this.parentObj.GetType() == "PROG")
        {
            prefixRTTI = "\\PROGRAM=" + this.parentObj.GetName();
        }
        else if (this.parentObj.GetType() == "CLAS")
        {
            prefixRTTI = "\\CLASS-POOL=" + this.parentObj.GetName();
        }

        var findLocalClass = this.current?.FindClassDefinition(name);
        if (findLocalClass != null)
        {
            if (findLocalClass.IsGlobal() == true)
            {
                prefixRTTI = "";
            }
            return ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier)findLocalClass, "CLAS", prefixRTTI + "\\CLASS=" + findLocalClass.GetName());
        }

        var globalClas = this.reg.GetObject("CLAS", name);
        if (globalClas != null)
        {
            return (globalClas.GetIdentifier(), "CLAS", "\\CLASS=" + globalClas.GetName());
        }

        var findLocalInterface = this.current?.FindInterfaceDefinition(name);
        if (findLocalInterface != null)
        {
            if (findLocalInterface.IsGlobal() == true)
            {
                prefixRTTI = "";
            }
            return ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier)findLocalInterface, "INTF", prefixRTTI + "\\INTERFACE=" + findLocalInterface.GetName());
        }

        var globalIntf = this.reg.GetObject("INTF", name);
        if (globalIntf != null)
        {
            return (globalIntf.GetIdentifier(), "INTF", "\\INTERFACE=" + globalIntf.GetName());
        }

        var def = this.current?.FindDeferred(name);
        if (def != null)
        {
            string rttiName = prefixRTTI;
            switch (def.Value.ooType)
            {
                case "INTF":
                    rttiName = rttiName + "\\INTERFACE=" + name;
                    break;
                default:
                    rttiName = rttiName + "\\CLASS=" + name;
                    break;
            }
            return (def.Value.id, def.Value.ooType, rttiName);
        }

        return null;
    }

    ///////////////////////////
    /** Lookup class in local and global scope */
    public IClassDefinition? FindClassDefinition(string? name)
    {
        if (name == null)
        {
            return null;
        }

        var clocal = this.current?.FindClassDefinition(name);
        if (clocal != null)
        {
            return clocal;
        }

        var cglobal = ((Class?)(this.reg.GetObject("CLAS", name)));
        if (cglobal != null)
        {
            return cglobal.GetDefinition();
        }

        return null;
    }

    public TypedIdentifier? FindTypePoolConstant(string? name)
    {
        if (name == null || name.Contains("_", StringComparison.Ordinal) == false)
        {
            return null;
        }

        var typePoolName = name.Split("_").ToList().ElementAtOrDefault(0);
        if (typePoolName.Length <= 1 || typePoolName.Length > 5)
        {
            return null;
        }

        if (this.parentObj.GetType() == "TYPE" && JsUtils.ToUpper(this.parentObj.GetName()) == JsUtils.ToUpper(typePoolName))
        {
            // dont recurse into itself
            return null;
        }

        var typePool = ((TypePool?)(this.reg.GetObject("TYPE", typePoolName)));
        if (typePool == null)
        {
            return null;
        }

        var spag = new SyntaxLogic(this.reg, typePool).Run().Spaghetti.GetFirstChild()?.GetFirstChild();

        var found = spag?.FindVariable(name);
        return found;
    }

    public TypedIdentifier? FindTypePoolType(string name)
    {
        if (name.Contains("_", StringComparison.Ordinal) == false)
        {
            return null;
        }

        var typePoolName = name.Split("_").ToList().ElementAtOrDefault(0);

        if (typePoolName.Length <= 2 || typePoolName.Length > 5)
        {
            return null;
        }

        if (this.parentObj.GetType() == "TYPE" && JsUtils.ToUpper(this.parentObj.GetName()) == JsUtils.ToUpper(typePoolName))
        {
            // dont recurse into itself
            return null;
        }

        if (new DDIC(this.reg).LookupNoVoid(name) != null)
        {
            // this is tricky, it should not do recursion when parsing the type pool itself,
            // think about DTEL ABAP_ENCOD vs TYPE ABAP
            return null;
        }

        var typePool = ((TypePool?)(this.reg.GetObject("TYPE", typePoolName)));
        if (typePool == null)
        {
            return null;
        }

        var spag = new SyntaxLogic(this.reg, typePool).Run().Spaghetti.GetFirstChild()?.GetFirstChild();

        var found = spag?.FindType(name);
        return found;
    }

    /** Lookup interface in local and global scope */
    public IInterfaceDefinition? FindInterfaceDefinition(string? name)
    {
        if (name == null)
        {
            return null;
        }

        var ilocal = this.current?.FindInterfaceDefinition(name);
        if (ilocal != null)
        {
            return ilocal;
        }

        var iglobal = ((Interface?)(this.reg.GetObject("INTF", name)));
        if (iglobal != null)
        {
            return iglobal.GetDefinition();
        }

        return null;
    }

    public IFormDefinition? FindFormDefinition(string name)
    {
        return this.current?.FindFormDefinition(name);
    }

    public TypedIdentifier? FindType(string? name)
    {
        if (name == null)
        {
            return null;
        }
        return this.current?.FindType(name);
    }

    public TypedIdentifier? FindExtraLikeType(string? name)
    {
        if (name == null)
        {
            return null;
        }
        return this.current?.FindExtraLikeType(name);
    }

    public TypedIdentifier? FindVariable(string? name)
    {
        if (name == null)
        {
            return null;
        }
        name = new Regex(@"^!", RegexOptions.ECMAScript).Replace(name, "", 1);
        var found = this.current?.FindVariable(name);
        if (found != null)
        {
            return found;
        }
        return this.FindTypePoolConstant(name);
    }

    public TypedIdentifier? FindReturningParameter()
    {
        foreach (var v in (this.current?.GetData().Vars ?? new Dictionary<string, TypedIdentifier>()).Keys.ToList())
        {
            var variable = this.current!.GetData().Vars.GetValueOrDefault(v);
            if (variable.GetMeta().Contains(IdentifierMeta.MethodReturning))
            {
                return variable;
            }
        }

        return null;
    }

    ///////////////////////////
    public DDIC GetDDIC()
    {
        return new DDIC(this.reg);
    }

    public IDDICReferences GetDDICReferences()
    {
        return this.reg.GetDDICReferences();
    }

    public IMSAGReferences GetMSAGReferences()
    {
        return this.reg.GetMSAGReferences();
    }

    public IObject GetParentObj()
    {
        return this.parentObj;
    }

    public string GetName()
    {
        if (this.current == null)
        {
            throw new Exception("error, getName");
        }
        return this.current.GetIdentifier().Sname;
    }

    public new string GetType()
    {
        if (this.current == null)
        {
            throw new Exception("error, getType");
        }
        return this.current.GetIdentifier().Stype;
    }

    public void Push(string stype, string sname, Position start, string filename)
    {
        IScopeIdentifier identifier = new IScopeIdentifier { Stype = stype, Sname = sname, Start = start, Filename = filename, End = null };

        //    console.dir("push scope, " + stype);
        if (this.current == null)
        {
            // the top node
            this.current = new SpaghettiScopeNode(identifier, null);
        }
        else
        {
            var parent = this.current;
            this.current = new SpaghettiScopeNode(identifier, parent);
            parent.AddChild(this.current);
        }
    }

    public bool IsOO()
    {
        SpaghettiScopeNode? curr = this.current;
        while (curr != null)
        {
            var stype = curr.GetIdentifier().Stype;
            if (stype == ScopeType.ClassDefinition || stype == ScopeType.Interface)
            {
                return true;
            }
            curr = curr.GetParent();
        }
        return false;
    }

    public bool IsAnyOO()
    {
        SpaghettiScopeNode? curr = this.current;
        while (curr != null)
        {
            var stype = curr.GetIdentifier().Stype;
            if (stype == ScopeType.ClassDefinition || stype == ScopeType.ClassImplementation || stype == ScopeType.Interface)
            {
                return true;
            }
            curr = curr.GetParent();
        }
        return false;
    }

    public bool IsInStaticMethod()
    {
        SpaghettiScopeNode? curr = this.current;
        while (curr != null)
        {
            var stype = curr.GetIdentifier().Stype;
            if (stype == ScopeType.MethodInstance)
            {
                return false;
            }
            else if (stype == ScopeType.ClassImplementation)
            {
                return true;
            }
            curr = curr.GetParent();
        }
        return false;
    }

    public void AddLocalFriend(string className, string friendName)
    {
        var key = JsUtils.ToUpper(className);
        var list = this.localFriends.GetValueOrDefault(key) ?? new List<string>();
        list.Add(JsUtils.ToUpper(friendName));
        this.localFriends[key] = list;
    }

    public bool IsLocalFriend(string className, string friendName)
    {
        return this.localFriends.GetValueOrDefault(JsUtils.ToUpper(className))?.Contains(JsUtils.ToUpper(friendName)) == true;
    }

    public string? GetEnclosingMethodName()
    {
        SpaghettiScopeNode? curr = this.current;
        while (curr != null)
        {
            var stype = curr.GetIdentifier().Stype;
            if (stype == ScopeType.Method || stype == ScopeType.MethodInstance)
            {
                return curr.GetIdentifier().Sname;
            }
            curr = curr.GetParent();
        }
        return null;
    }

    public string? GetEnclosingClassName()
    {
        SpaghettiScopeNode? curr = this.current;
        while (curr != null)
        {
            if (curr.GetIdentifier().Stype == ScopeType.ClassImplementation)
            {
                return curr.GetIdentifier().Sname;
            }
            curr = curr.GetParent();
        }
        return null;
    }

    public bool IsGlobalOO()
    {
        return this.parentObj.GetType() == "INTF" || this.parentObj.GetType() == "CLAS";
    }

    public bool IsTypePool()
    {
        return this.current?.GetIdentifier().Filename.EndsWith(".type.abap", StringComparison.Ordinal) == true || false;
    }

    public void SetAllowHeaderUse(string name)
    {
        // workaround for SELECT FOR ALL ENTRIES
        this.allowHeaderUse = name;
    }

    public bool IsAllowHeaderUse(string name)
    {
        return JsUtils.ToUpper(name) == JsUtils.ToUpper(this.allowHeaderUse);
    }

    public SpaghettiScope Pop(Position end)
    {
        //    console.dir("pop scope, " + this.current?.getIdentifier().stype);
        this.allowHeaderUse = null;
        if (this.current == null)
        {
            throw new Exception("something wrong, top scope popped");
        }
        this.current.SetEnd(end);

        var current = this.current;
        this.current = this.current.GetParent();
        return new SpaghettiScope(current);
    }
}
