// Ported from abaplint packages/core/src/abap/5_syntax/_object_oriented.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax;

// todo, think some of the public methods can be made private
// todo: changet this class to static? for performance
public class ObjectOriented
{
    private readonly CurrentScope scope;

    public ObjectOriented(CurrentScope scope)
    {
        this.scope = scope;
    }

    private List<string> FromInterfaceByName(string name, List<string> ignore)
    {
        var idef = this.scope.FindInterfaceDefinition(name);
        if (idef == null || ignore.Contains(JsUtils.ToUpper(name)))
        {
            return new List<string>();
        }
        List<string> ret = new List<string> { JsUtils.ToUpper(name) };

        foreach (var t in idef.GetTypeDefinitions().GetAll())
        {
            var n = name + "~" + t.Type.GetName();
            this.scope.AddTypeNamed(n, t.Type);
        }

        this.scope.AddListPrefix(idef.GetAttributes().GetConstants().Cast<TypedIdentifier>().ToList(), name + "~");
        this.scope.AddListPrefix(idef.GetAttributes().GetStatic().Cast<TypedIdentifier>().ToList(), name + "~");
        this.scope.AddListPrefix(idef.GetAttributes().GetInstance().Cast<TypedIdentifier>().ToList(), name + "~");

        foreach (var i in idef.GetImplementing())
        {
            if (ignore.Contains(JsUtils.ToUpper(i.Name)))
            {
                continue;
            }
            ret.AddRange(this.FromInterfaceByName(i.Name, ignore));
            ignore.Add(JsUtils.ToUpper(i.Name));
            ret.Add(JsUtils.ToUpper(i.Name));
        }
        return ret;
    }

    public void AddAliasedAttributes(IClassDefinition classDefinition)
    {
        foreach (var alias in classDefinition.GetAliases() ?? new List<Alias>())
        {
            var comp = alias.GetComponent();
            var idef = this.scope.FindInterfaceDefinition(comp.Split("~").ToList().ElementAtOrDefault(0));
            if (idef != null)
            {
                var found = idef.GetAttributes()!.FindByName(comp.Split("~").ToList().ElementAtOrDefault(1));
                if (found != null)
                {
                    this.scope.AddNamedIdentifier(alias.GetName(), found);
                }
            }
        }
        var superName = classDefinition.GetSuperClass();
        if (superName != null)
        {
            var def = this.scope.FindClassDefinition(superName);
            if (def != null)
            {
                this.AddAliasedAttributes(def);
            }
        }
    }

    public void AddAliasedTypes(IClassDefinition classDefinition)
    {
        foreach (var alias in classDefinition.GetAliases() ?? new List<Alias>())
        {
            var comp = alias.GetComponent();
            var idef = this.scope.FindInterfaceDefinition(comp.Split("~").ToList().ElementAtOrDefault(0));
            if (idef != null)
            {
                var found = idef.GetTypeDefinitions()!.GetByName(comp.Split("~").ToList().ElementAtOrDefault(1));
                if (found != null)
                {
                    this.scope.AddType(found);
                }
            }
        }
        var superName = classDefinition.GetSuperClass();
        if (superName != null)
        {
            var def = this.scope.FindClassDefinition(superName);
            if (def != null)
            {
                this.AddAliasedTypes(def);
            }
        }
    }

    private (IMethodDefinition method, IInterfaceDefinition def)? FindMethodInInterface(string interfaceName, string methodName)
    {

        var idef = this.scope.FindInterfaceDefinition(interfaceName);
        if (idef != null)
        {
            var method = idef.GetMethodDefinitions().GetByName(methodName);
            if (method != null)
            {
                return (method, idef);
            }
            return this.FindMethodViaAlias(methodName, idef);
        }
        return null;
    }

    private (IMethodDefinition method, IInterfaceDefinition def)? FindMethodViaAlias(string methodName, IInterfaceDefinition def)
    {

        foreach (var a in def.GetAliases() ?? new List<Alias>())
        {
            if (JsUtils.ToUpper(a.GetName()) == JsUtils.ToUpper(methodName))
            {
                var comp = a.GetComponent();
                var res = this.FindMethodInInterface(comp.Split("~").ToList().ElementAtOrDefault(0), comp.Split("~").ToList().ElementAtOrDefault(1));
                if (res != null)
                {
                    return res.Value;
                }
            }
        }
        return null;
    }

    public IReferenceExtras MethodReferenceExtras(IInterfaceDefinition? foundDef, string? ooName)
    {
        if (foundDef == null)
        {
            return new IReferenceExtras { OoName = ooName, OoType = null };
        }
        return new IReferenceExtras { OoName = foundDef?.GetName(), OoType = foundDef is global::ScipSharp.Indexer.Abap.Types.ClassDefinition ? "CLAS" : "INTF" };
    }

    public string FindClassName(StatementNode node)
    {
        if (!((node.Get() is global::ScipSharp.Indexer.Abap.Statements.Statements.ClassImplementation || node.Get() is global::ScipSharp.Indexer.Abap.Statements.Statements.ClassDefinition)))
        {
            throw new Exception("findClassName, unexpected node type");
        }
        var className = node.FindFirstExpression<ClassName>();
        if (className == null)
        {
            throw new Exception("findClassName, unexpected node type");
        }
        return className.GetFirstToken().GetStr();
    }

    public List<IImplementing> FindInterfaces(IInterfaceDefinition cd)
    {
        List<IImplementing> ret = new List<IImplementing>([.. cd.GetImplementing()]);

        // JS for-of also visits the elements pushed while iterating
        for (var ri = 0; ri < ret.Count; ri++)
        {
            var r = ret[ri];
            var nested = this.scope.FindInterfaceDefinition(r.Name)?.GetImplementing();
            if (nested != null)
            {
                ret.AddRange(nested);
            }
        }

        var sup = cd.GetSuperClass();
        if (!string.IsNullOrEmpty(sup))
        {
            try
            {
                ret.AddRange(this.FindInterfaces(((IInterfaceDefinition?)this.FindSuperDefinition(sup))));
            }
            catch
            {
                // ignore errors, they will show up as variable not found anyhow
            }
        }

        return ret;
    }

    // does the supplied class have friendship with "friendName"?
    // note that interfaces of the friend, and subclasses of the friend also have friendship
    // returns the visibility as text if a member declared in foundDef, reached via def, cannot be accessed from enclosing
    public string? MemberNotVisible(Visibility visibility, IInterfaceDefinition? def, IInterfaceDefinition? foundDef, string? enclosing)
    {

        if (visibility == Visibility.Public || foundDef == null)
        {
            return null;
        }
        else if (!((foundDef is global::ScipSharp.Indexer.Abap.Types.ClassDefinition)))
        {
            // interface members are always public
            return null;
        }

        var name = JsUtils.ToUpper(((global::ScipSharp.Indexer.Abap.Types.ClassDefinition)foundDef).GetName());
        enclosing = JsUtils.ToUpper(enclosing);
        if (enclosing == null || enclosing == name)
        {
            return null;
        }

        // friendship with the class used at the call site also gives access to inherited members
        List<string> visited = new List<string>();
        IClassDefinition? current = def is global::ScipSharp.Indexer.Abap.Types.ClassDefinition ? ((global::ScipSharp.Indexer.Abap.Types.ClassDefinition)def) : ((global::ScipSharp.Indexer.Abap.Types.ClassDefinition)foundDef);
        while (current != null && visited.Contains(JsUtils.ToUpper(current.GetName())) == false)
        {
            visited.Add(JsUtils.ToUpper(current.GetName()));
            if (this.HasFriendship(current, enclosing))
            {
                return null;
            }
            string? sup = current.GetSuperClass();
            current = sup == null ? (IClassDefinition?)null : this.scope.FindClassDefinition(sup);
        }

        if (visibility == Visibility.Protected)
        {
            // subclasses can access protected members
            string? sup = this.scope.FindClassDefinition(enclosing)?.GetSuperClass();
            while (sup != null)
            {
                if (JsUtils.ToUpper(sup) == name)
                {
                    return null;
                }
                sup = this.scope.FindClassDefinition(sup)?.GetSuperClass();
            }
            return "protected";
        }

        return "private";
    }

    // the fixture methods of a test class are called by ABAP Unit only, returns the message otherwise
    public string? CheckSpecialTestMethodCall(IMethodDefinition? method, IInterfaceDefinition? foundDef)
    {

        if (method == null || !((foundDef is global::ScipSharp.Indexer.Abap.Types.ClassDefinition)) || ((global::ScipSharp.Indexer.Abap.Types.ClassDefinition)foundDef).IsForTesting() == false)
        {
            return null;
        }
        var name = JsUtils.ToUpper(method.GetName());
        if (new List<string> { "SETUP", "TEARDOWN", "CLASS_SETUP", "CLASS_TEARDOWN" }.Contains(name) == false)
        {
            return null;
        }
        return $"The special method \"{name}\" cannot be called directly";
    }

    // SUPER-> can only call the previous implementation of the method it is used in, returns the message otherwise
    // both names are resolved in the super class, so an alias and the method it stands for are the same
    public string? CheckSuperCall(IInterfaceDefinition? superDef, string? methodName)
    {

        var enclosing = this.scope.GetEnclosingMethodName();
        if (methodName == null || enclosing == null || JsUtils.ToUpper(methodName) == JsUtils.ToUpper(enclosing))
        {
            return null;
        }
        var message = "SUPER-> can only be used to call the previous implementation of the same method";
        // a method that is not a redefinition has no previous implementation
        var enclosingClass = this.scope.FindClassDefinition(this.scope.GetEnclosingClassName());
        var ownDefinition = enclosingClass?.GetMethodDefinitions().GetByName(enclosing);
        if (ownDefinition != null && ownDefinition.IsRedefinition() == false)
        {
            return message;
        }
        var called = this.SearchMethodName(superDef, methodName).method;
        var own = this.SearchMethodName(superDef, enclosing).method;
        if (called == null || own == null || called == own)
        {
            return null;
        }
        return message;
    }

    // the class declaring the attribute or constant, searched from def up through the super classes
    public IClassDefinition? FindAttributeOwner(IInterfaceDefinition? def, TypedIdentifier found)
    {

        List<string> visited = new List<string>();
        IInterfaceDefinition? current = def;
        while (current is global::ScipSharp.Indexer.Abap.Types.ClassDefinition && visited.Contains(JsUtils.ToUpper(((global::ScipSharp.Indexer.Abap.Types.ClassDefinition)current).GetName())) == false)
        {
            visited.Add(JsUtils.ToUpper(((global::ScipSharp.Indexer.Abap.Types.ClassDefinition)current).GetName()));
            var attributes = ((global::ScipSharp.Indexer.Abap.Types.ClassDefinition)current).GetAttributes();
            if (attributes.GetAll().Contains((found as ClassAttribute)!) || attributes.GetConstants().Contains((found as ClassConstant)!))
            {
                return ((global::ScipSharp.Indexer.Abap.Types.ClassDefinition)current);
            }
            string? sup = ((global::ScipSharp.Indexer.Abap.Types.ClassDefinition)current).GetSuperClass();
            current = ((IInterfaceDefinition?)(sup == null ? (IInterfaceDefinition?)null : this.FindSuperDefinition(sup)));
        }
        return null;
    }

    public bool HasFriendship(IClassDefinition cd, string friendName)
    {
        var friends = cd.GetFriends().Select(f => JsUtils.ToUpper(f)).ToList();
        Func<string, bool> isFriend = name => friends.Contains(JsUtils.ToUpper(name)) || this.scope.IsLocalFriend(cd.GetName(), name);

        if (isFriend(friendName))
        {
            return true;
        }

        var friendDefinition = this.scope.FindClassDefinition(friendName);
        if (friendDefinition == null)
        {
            return false;
        }

        if (this.FindInterfaces(((IInterfaceDefinition?)friendDefinition)).Any(i => friends.Contains(JsUtils.ToUpper(i.Name))))
        {
            return true;
        }

        string? sup = friendDefinition.GetSuperClass();
        while (sup != null)
        {
            if (isFriend(sup))
            {
                return true;
            }
            sup = this.scope.FindClassDefinition(sup)?.GetSuperClass();
        }

        return false;
    }

    public IEventDefinition? SearchEvent(IInterfaceDefinition? def, string? name)
    {

        if (def == null || name == null)
        {
            return null;
        }

        var found = def.GetEvents().FirstOrDefault(e => JsUtils.ToUpper(e.GetName()) == JsUtils.ToUpper(name));
        if (found != null)
        {
            return found;
        }

        if (name.Contains("~", StringComparison.Ordinal))
        {
            // event of an implemented interface, ie. "if_name~event_name"
            var __a0 = name.Split("~").ToList();
            var interfaceName = __a0.ElementAtOrDefault(0);
            var eventName = __a0.ElementAtOrDefault(1);
            return this.SearchEvent(this.scope.FindInterfaceDefinition(interfaceName), eventName);
        }

        foreach (var a in def.GetAliases() ?? new List<Alias>())
        {
            if (JsUtils.ToUpper(a.GetName()) == JsUtils.ToUpper(name))
            {
                var comp = a.GetComponent();
                var res = this.SearchEvent(this.scope.FindObjectDefinition(comp.Split("~").ToList().ElementAtOrDefault(0)), comp.Split("~").ToList().ElementAtOrDefault(1));
                if (res != null)
                {
                    return res;
                }
            }
        }

        var sup = def.GetSuperClass();
        if (!string.IsNullOrEmpty(sup))
        {
            return this.SearchEvent(((IInterfaceDefinition?)this.FindSuperDefinition(sup)), name);
        }

        return null;
    }

    // search in via super class, interfaces and aliases
    public ClassAttribute? SearchAttributeName(IInterfaceDefinition? def, string? name)
    {

        if (def == null || name == null)
        {
            return null;
        }

        var upper = JsUtils.ToUpper(name);
        var attr = def.GetAttributes();
        if (attr != null)
        {
            foreach (var a in attr.GetAll())
            {
                if (JsUtils.ToUpper(a.GetName()) == upper)
                {
                    return a;
                }
            }
        }

        foreach (var a in def.GetAliases() ?? new List<Alias>())
        {
            if (JsUtils.ToUpper(a.GetName()) == upper)
            {
                var comp = a.GetComponent();
                var res = this.SearchAttributeName(this.scope.FindObjectDefinition(comp.Split("~").ToList().ElementAtOrDefault(0)), comp.Split("~").ToList().ElementAtOrDefault(1));
                if (res != null)
                {
                    return res;
                }
            }
        }

        if (name.Contains("~", StringComparison.Ordinal))
        {
            var interfaceName = upper.Split("~").ToList().ElementAtOrDefault(0);
            if (this.ListInterfacesRecursive(def).Contains(interfaceName))
            {
                return this.SearchAttributeName(this.scope.FindInterfaceDefinition(interfaceName), name.Split("~").ToList().ElementAtOrDefault(1));
            }
        }

        var sup = def.GetSuperClass();
        if (!string.IsNullOrEmpty(sup))
        {
            return this.SearchAttributeName(((IInterfaceDefinition?)this.FindSuperDefinition(sup)), name);
        }

        return null;
    }

    // search in via super class, interfaces and aliases
    public TypedIdentifier? SearchTypeName(IInterfaceDefinition? def, string? name)
    {

        if (def == null || name == null)
        {
            return null;
        }

        var typeDefinitions = def.GetTypeDefinitions();
        if (typeDefinitions == null)
        {
            // a class referring to its own types while its definition is built: JS throws a TypeError here
            throw new Exception("Cannot read properties of undefined (reading 'getByName')");
        }
        var search = typeDefinitions.GetByName(name);
        if (search != null)
        {
            return search;
        }

        foreach (var a in def.GetAliases())
        {
            if (JsUtils.ToUpper(a.GetName()) == JsUtils.ToUpper(name))
            {
                var comp = a.GetComponent();
                var res = this.SearchTypeName(this.scope.FindObjectDefinition(comp.Split("~").ToList().ElementAtOrDefault(0)), comp.Split("~").ToList().ElementAtOrDefault(1));
                if (res != null)
                {
                    return res;
                }
            }
        }

        if (name.Contains("~", StringComparison.Ordinal))
        {
            var interfaceName = name.Split("~").ToList().ElementAtOrDefault(0);
            if (def.GetImplementing().Any(a => JsUtils.ToUpper(a.Name) == JsUtils.ToUpper(interfaceName)))
            {
                return this.SearchTypeName(this.scope.FindInterfaceDefinition(interfaceName), name.Split("~").ToList().ElementAtOrDefault(1));
            }
        }

        var sup = def.GetSuperClass();
        if (!string.IsNullOrEmpty(sup))
        {
            return this.SearchTypeName(((IInterfaceDefinition?)this.FindSuperDefinition(sup)), name);
        }

        return null;
    }

    // search in via super class, interfaces and aliases
    public ClassConstant? SearchConstantName(IInterfaceDefinition? def, string? name)
    {

        if (def == null || name == null)
        {
            return null;
        }

        var upper = JsUtils.ToUpper(name);
        foreach (var a in def.GetAttributes().GetConstants())
        {
            if (JsUtils.ToUpper(a.GetName()) == upper)
            {
                return a;
            }
        }

        foreach (var a in def.GetAliases())
        {
            if (JsUtils.ToUpper(a.GetName()) == upper)
            {
                var comp = a.GetComponent();
                var res = this.SearchConstantName(this.scope.FindObjectDefinition(comp.Split("~").ToList().ElementAtOrDefault(0)), comp.Split("~").ToList().ElementAtOrDefault(1));
                if (res != null)
                {
                    return res;
                }
            }
        }

        if (name.Contains("~", StringComparison.Ordinal))
        {
            var interfaceName = upper.Split("~").ToList().ElementAtOrDefault(0);
            if (def.GetImplementing().Any(a => JsUtils.ToUpper(a.Name) == interfaceName))
            {
                return this.SearchConstantName(this.scope.FindInterfaceDefinition(interfaceName), name.Split("~").ToList().ElementAtOrDefault(1));
            }
        }

        var sup = def.GetSuperClass();
        if (!string.IsNullOrEmpty(sup))
        {
            return this.SearchConstantName(((IInterfaceDefinition?)this.FindSuperDefinition(sup)), name);
        }

        return null;
    }

    // search in via super class, interfaces and aliases
    public (IMethodDefinition? method, IInterfaceDefinition? def) SearchMethodName(IInterfaceDefinition? def, string? name)
    {

        if (def == null || name == null)
        {
            return (null, null);
        }

        var methodDefinition = this.FindMethod(def, name);
        if (methodDefinition != null)
        {
            return (methodDefinition, def);
        }

        string? interfaceName = null;
        if (name.Contains("~", StringComparison.Ordinal))
        {
            interfaceName = name.Split("~").ToList().ElementAtOrDefault(0);
            if (!string.IsNullOrEmpty(interfaceName) && this.FindInterfaces(def).Any(i => JsUtils.ToUpper(i.Name) == JsUtils.ToUpper(interfaceName)) == false)
            {
                return (null, null);
            }
        }

        // todo, this is not completely correct? hmm, why? visibility?
        if (methodDefinition == null && !string.IsNullOrEmpty(interfaceName))
        {
            name = name.Split("~").ToList().ElementAtOrDefault(1);
            var found = this.FindMethodInInterface(interfaceName, name);
            if (found != null)
            {
                return found.Value;
            }
        }
        else if (methodDefinition == null)
        {
            var found = this.FindMethodViaAlias(name, def);
            if (found != null)
            {
                return found.Value;
            }
        }

        var sup = def.GetSuperClass();
        if (methodDefinition == null && !string.IsNullOrEmpty(sup))
        {
            return this.SearchMethodName(((IInterfaceDefinition?)this.FindSuperDefinition(sup)), name);
        }

        return (null, null);
    }

    public IMethodDefinition? FindMethod(IInterfaceDefinition def, string methodName)
    {
        var defs = def.GetMethodDefinitions();
        if (defs == null)
        {
            return null;
        }

        var method = defs.GetByName(methodName);
        if (method == null)
        {
            return null;
        }

        if (method.IsRedefinition())
        {
            return this.FindMethodInSuper(def, methodName);
        }

        return method;
    }

    private IMethodDefinition? FindMethodInSuper(IInterfaceDefinition child, string methodName)
    {
        string? sup = child.GetSuperClass();
        while (sup != null)
        {
            var cdef = this.FindSuperDefinition(sup);
            if (cdef == null)
            {
                break;
            }
            var found = this.FindMethod(((IInterfaceDefinition?)cdef), methodName);
            if (found != null)
            {
                return found;
            }
            sup = cdef.GetSuperClass();
        }
        return null;
    }

    private IClassDefinition? FindSuperDefinition(string name)
    {
        var csup = this.scope.FindClassDefinition(name);
        if (csup == null)
        {
            if (this.scope.GetRegistry().GetConfig().GetSyntaxSetttings().TolerateMissingSuperClass)
            {
                return null; // not in abaplint: the super class is outside the sources, what it declares is unknown
            }
            throw new Exception("Super class \"" + name + "\" not found or contains errors");
        }
        return csup;
    }

    public void FromSuperClassesAndInterfaces(IClassDefinition child)
    {
        var implemented = this.FromSuperClasses(child);
        this.FromInterfaces(((IInterfaceDefinition?)child), implemented);
    }

    // returns list of interfaces implemented
    private List<string> FromSuperClasses(IClassDefinition child)
    {
        string? sup = child.GetSuperClass();
        List<string> ignore = new List<string>();

        while (sup != null)
        {
            var cdef = this.FindSuperDefinition(sup);
            if (cdef == null)
            {
                break;
            }
            foreach (var a in cdef.GetAttributes().GetAll())
            {
                if (a.GetVisibility() != Visibility.Private)
                {
                    this.scope.AddIdentifier(a);
                    // todo, handle scope and instance vs static
                }
            }
            foreach (var c in cdef.GetAttributes().GetConstants())
            {
                if (c.GetVisibility() != Visibility.Private)
                {
                    this.scope.AddIdentifier(c);
                }
            }
            foreach (var t in cdef.GetTypeDefinitions().GetAll())
            {
                if (t.Visibility != Visibility.Private)
                {
                    this.scope.AddType(t.Type);
                }
            }
            ignore.AddRange(this.FromInterfaces(((IInterfaceDefinition?)cdef), ignore));
            sup = cdef.GetSuperClass();
        }

        return ignore;
    }

    /** returns list of interfaces implemented, recursive */
    public List<string> ListInterfacesRecursive(IInterfaceDefinition definition)
    {
        List<string> list = new List<string>();
        foreach (var i in definition.GetImplementing())
        {
            var upper = JsUtils.ToUpper(i.Name);
            list.Add(upper);

            var def = this.scope.FindInterfaceDefinition(upper);
            if (def != null)
            {
                list.AddRange(this.ListInterfacesRecursive(def));
            }
        }

        return new List<string>([.. new HashSet<string>(list)]);
    }

    public List<string> FromInterfaces(IInterfaceDefinition definition, List<string>? skip = null)
    {
        List<string> ignore = new List<string>();
        foreach (var i in definition.GetImplementing())
        {
            ignore.AddRange(this.FromInterfaceByName(i.Name, ignore.Concat(skip ?? new List<string>()).ToList()));
        }
        return ignore;
    }
}
