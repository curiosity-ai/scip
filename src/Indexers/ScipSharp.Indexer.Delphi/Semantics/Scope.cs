namespace ScipSharp.Indexer.Delphi.Semantics;

/// <summary>A lexical scope. Lookups walk outwards; the first scope with a matching entity wins.</summary>
internal abstract class Scope(Scope? parent)
{
    public Scope? Parent { get; } = parent;

    /// <summary>Adds the entities called <paramref name="name"/> that this scope (not its parents) declares.</summary>
    public abstract void Find(string name, List<Entity> into);

    public List<Entity> Lookup(string name, Func<Entity, bool>? filter = null)
    {
        var found = new List<Entity>();
        for (var s = this; s is not null; s = s.Parent)
        {
            found.Clear();
            s.Find(name, found);
            if (filter is not null)
            {
                found.RemoveAll(e => !filter(e));
            }

            if (found.Count > 0)
            {
                return found;
            }
        }

        return found;
    }

    /// <summary>The innermost unit scope (gives access to the unit and the units it uses).</summary>
    public UnitScope? UnitScope
    {
        get
        {
            for (var s = this; s is not null; s = s.Parent)
            {
                if (s is UnitScope u)
                {
                    return u;
                }
            }

            return null;
        }
    }

    /// <summary>The innermost routine scope (method context for <c>Self</c>, <c>inherited</c> and <c>Result</c>).</summary>
    public RoutineScope? RoutineScope
    {
        get
        {
            for (var s = this; s is not null; s = s.Parent)
            {
                if (s is RoutineScope r)
                {
                    return r;
                }
            }

            return null;
        }
    }
}

/// <summary>A scope backed by a member table: type parameters, routine locals, inline variables.</summary>
internal sealed class TableScope(MemberTable table, Scope? parent) : Scope(parent)
{
    public MemberTable Table { get; } = table;

    public override void Find(string name, List<Entity> into) => into.AddRange(Table.Get(name));
}

/// <summary>
/// The scope of a unit section: its own declarations (implementation, then interface), then the interface sections of
/// the used units with the last-listed unit winning, then <c>System</c>.
/// </summary>
internal sealed class UnitScope(Entity unit, bool implementation, Model model) : Scope(null)
{
    public Entity Unit { get; } = unit;

    public bool Implementation { get; } = implementation;

    public override void Find(string name, List<Entity> into)
    {
        if (Implementation && Unit.ImplementationMembers is { } impl)
        {
            into.AddRange(impl.Get(name));
        }

        into.AddRange(Unit.Members.Get(name));
        if (into.Count > 0)
        {
            return;
        }

        if (Implementation)
        {
            for (var i = Unit.ImplementationUses.Count - 1; i >= 0; i--)
            {
                into.AddRange(Unit.ImplementationUses[i].Members.Get(name));
                if (into.Count > 0)
                {
                    return;
                }
            }
        }

        for (var i = Unit.InterfaceUses.Count - 1; i >= 0; i--)
        {
            into.AddRange(Unit.InterfaceUses[i].Members.Get(name));
            if (into.Count > 0)
            {
                return;
            }
        }

        if (!ReferenceEquals(Unit, model.SystemUnit))
        {
            into.AddRange(model.SystemUnit.Members.Get(name));
        }
    }

    /// <summary>Used units that could be found neither in the project nor in the stubs.</summary>
    public IEnumerable<Entity> UnknownUnits =>
        (Implementation ? Unit.ImplementationUses.Concat(Unit.InterfaceUses) : Unit.InterfaceUses).Where(u => u.IsUnknownUnit);
}

/// <summary>The members of a structured type (and its ancestors and helpers), as seen from inside its declaration or methods.</summary>
internal sealed class TypeBodyScope(Entity type, Model model, Scope? parent) : Scope(parent)
{
    public Entity Type { get; } = type;

    public override void Find(string name, List<Entity> into)
    {
        var r = model.FindMember(Model.SelfType(Type), name);
        into.AddRange(r.Found);
    }
}

/// <summary>The members of the expression of a <c>with</c> statement.</summary>
internal sealed class WithScope(Ty type, Model model, Scope? parent) : Scope(parent)
{
    public Ty Type { get; } = type;

    public override void Find(string name, List<Entity> into) => into.AddRange(model.FindMember(Type, name).Found);
}

/// <summary>Parameters and locals of a routine body; also carries the method context.</summary>
internal sealed class RoutineScope(Entity? routine, Entity? ownerType, MemberTable table, Scope? parent) : Scope(parent)
{
    public Entity? Routine { get; } = routine;

    /// <summary>The class/record whose method this is (for <c>Self</c> and <c>inherited</c>).</summary>
    public Entity? OwnerType { get; } = ownerType;

    /// <summary>Parent entity of the routine's local declarations (locals are <c>local N</c> symbols).</summary>
    public Entity? LocalsOwner { get; set; }

    public MemberTable Table { get; } = table;

    public override void Find(string name, List<Entity> into) => into.AddRange(Table.Get(name));
}
