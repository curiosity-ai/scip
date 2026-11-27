// Ported from abaplint packages/core/src/abap/5_syntax/global_definitions/find_global_definitions.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap.Syntax.GlobalDefinitions;

// todo: rewrite all of this to use a graph based deterministic approach instead
// this makes sure to cache global interface and class definitions in the corresponding object
public class FindGlobalDefinitions
{
    private readonly IRegistry reg;

    public FindGlobalDefinitions(IRegistry reg)
    {
        this.reg = reg;
    }

    public void Run(IProgress? progress = null)
    {
        var MAX_PASSES = 10;
        int lastPass = int.MaxValue;

        // the setDirty method in the objects clears the definitions
        List<ABAPObject> candidates = new List<ABAPObject>();
        // not in abaplint: where this is, for a run watched from another thread (Registry.ParseStep)
        var watched = this.reg as Registry;
        var position = 0;
        foreach (var o in this.reg.GetObjects())
        {
            position++;
            if (watched != null)
            {
                watched.ParseIndex = position;
                watched.ParseStep = "dictionary types, on a " + o.GetType();
            }
            if ((o is global::ScipSharp.Indexer.Abap.Objects.Interface || o is Class) && GetDefinition((ABAPObject)o) == null)
            {
                candidates.Add(((ABAPObject)o));
            }
            else if (o is DataElement || o is Table || o is global::ScipSharp.Indexer.Abap.Objects.TableType || o is View || o is LockObject || o is AuthorizationCheckField)
            {
                ((IParseTypeObject)o).ParseType(this.reg);
                // make sure the references are set after parsing finishes
            }
        }
        // make sure the sequence is always the same, disregarding the sequence they were added to the registry
        // this will hopefully make it easier to debug
        JsUtils.Sort(candidates, (a, b) =>
        {
            return string.CompareOrdinal(a.GetName(), b.GetName());
        });

        for (var i = 1; i <= MAX_PASSES; i++)
        {
            progress?.Set(candidates.Count, "Global OO types, pass " + i);
            int thisPass = 0;
            List<ABAPObject> next = new List<ABAPObject>();
            var at = 0;
            foreach (var o in candidates)
            {
                at++;
                if (watched != null)
                {
                    watched.ParseIndex = at;
                    watched.ParseTotal = candidates.Count;
                    watched.ParseStep = "class and interface definitions, pass " + i + " of at most " + MAX_PASSES + ", on a " + o.GetType();
                    System.Threading.Interlocked.Exchange(ref watched.ParseSize, o.GetFiles().Sum(f => (long)f.GetRaw().Length));
                }
                progress?.TickSync("Global OO types(pass " + i + "), next pass: " + next.Count);
                this.Update(o);
                var untypedCount = this.CountUntyped(o);
                if (untypedCount > 0)
                {
                    next.Add(o);
                }
                thisPass = thisPass + untypedCount;
            }

            candidates = next;

            if (lastPass == thisPass || thisPass == 0)
            {
                break;
            }
            lastPass = thisPass;
        }
    }

    /////////////////////////////
    public int CountUntyped(ABAPObject obj)
    {
        var def = GetDefinition(obj);
        if (def == null)
        {
            return 1;
        }

        int count = 0;
        foreach (var t in def.GetTypeDefinitions().GetAll())
        {
            count = count + this.Count(t.Type.GetType());
        }
        foreach (var a in def.GetAttributes().GetAll())
        {
            count = count + this.Count(a.GetType());
        }
        foreach (var a in def.GetAttributes().GetConstants())
        {
            count = count + this.Count(a.GetType());
        }

        foreach (var m in def.GetMethodDefinitions().GetAll())
        {
            foreach (var p in m.GetParameters().GetAll())
            {
                count = count + this.Count(p.GetType());
            }
        }
        foreach (var e in def.GetEvents() ?? new List<IEventDefinition>())
        {
            foreach (var p in e.GetParameters())
            {
                count = count + this.Count(p.GetType());
            }
        }

        return count;
    }

    private int Count(object type)
    {
        if (type is UnknownType || type is VoidType)
        {
            return 1;
        }
        else if (type is global::ScipSharp.Indexer.Abap.Types.Basic.TableType)
        {
            return this.Count(((global::ScipSharp.Indexer.Abap.Types.Basic.TableType)type).GetRowType());
        }
        else if (type is DataReference)
        {
            return this.Count(((DataReference)type).GetType());
        }
        else if (type is StructureType)
        {
            int count = 0;
            foreach (var c in ((StructureType)type).GetComponents())
            {
                count = count + this.Count(c.Type);
            }
            return count;
        }
        return 0;
    }

    private void Update(ABAPObject obj)
    {
        var file = obj.GetMainABAPFile();
        if (file == null)
        {
            SetDefinition(obj, null);
            return;
        }

        var struc = file?.GetStructure();
        if (struc == null)
        {
            SetDefinition(obj, null);
            return;
        }

        var input = new SyntaxInput { Filename = file.GetFilename(), Scope = CurrentScope.BuildDefault(this.reg, obj), Issues = new List<Issue>(), Deferred = new List<Action>() };

        if (obj is global::ScipSharp.Indexer.Abap.Objects.Interface)
        {
            var found = struc.FindFirstStructure<global::ScipSharp.Indexer.Abap.Structures.Structures.Interface>();
            if (found != null)
            {
                try
                {
                    var def = new InterfaceDefinition(found, input);
                    ((global::ScipSharp.Indexer.Abap.Objects.Interface)obj).SetDefinition(def);
                }
                catch
                {
                    ((global::ScipSharp.Indexer.Abap.Objects.Interface)obj).SetDefinition(null);
                }
            }
            else
            {
                ((global::ScipSharp.Indexer.Abap.Objects.Interface)obj).SetDefinition(null);
            }
        }
        else
        {
            var found = struc.FindFirstStructure<global::ScipSharp.Indexer.Abap.Structures.Structures.ClassDefinition>();
            if (found != null)
            {
                try
                {
                    var def = new global::ScipSharp.Indexer.Abap.Types.ClassDefinition(found, input);
                    ((Class)obj).SetDefinition(def);
                }
                catch
                {
                    ((Class)obj).SetDefinition(null);
                }
            }
            else
            {
                ((Class)obj).SetDefinition(null);
            }
        }
    }

    // TS calls getDefinition()/setDefinition() on Class | Interface
    private static IInterfaceDefinition? GetDefinition(ABAPObject obj)
    {
        return obj is Class c ? c.GetDefinition() : ((global::ScipSharp.Indexer.Abap.Objects.Interface)obj).GetDefinition();
    }

    private static void SetDefinition(ABAPObject obj, IInterfaceDefinition? def)
    {
        if (obj is Class c)
        {
            c.SetDefinition((IClassDefinition?)def);
        }
        else
        {
            ((global::ScipSharp.Indexer.Abap.Objects.Interface)obj).SetDefinition(def);
        }
    }
}
