// Ported from abaplint packages/core/src/abap/types/basic/structure_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class IStructureComponent
{
    public string Name { get; set; }
    public AbstractType Type { get; set; }
    public bool? AsInclude { get; set; }
    public string? Suffix { get; set; }
}

public class StructureType : AbstractType
{
    private readonly Dictionary<string, AbstractType> indexed;
    private readonly List<IStructureComponent> components;

    public StructureType(List<IStructureComponent> components, string? qualifiedName = null, string? ddicName = null, string? description = null)
        : base(new AbstractTypeData { QualifiedName = qualifiedName, DdicName = ddicName, Description = description })
    {
        if (components.Count == 0)
        {
            throw new Exception("Structure does not contain any components");
        }

        this.indexed = new Dictionary<string, AbstractType>();
        foreach (var c in components)
        {
            var upper = JsUtils.ToUpper(c.Name);
            if (this.indexed.GetValueOrDefault(upper) != null)
            {
                throw new Exception("Structure, duplicate field name \"" + upper + "\", " + JsUtils.Str(qualifiedName));
            }
            this.indexed[upper] = c.Type;
        }
        this.components = components;
    }

    public List<IStructureComponent> GetComponents()
    {
        return this.components;
    }

    public AbstractType? GetComponentByName(string name)
    {
        return this.indexed.GetValueOrDefault(JsUtils.ToUpper(name));
    }

    public override string ToText(int level)
    {
        List<string> compo = new List<string>();
        foreach (var c in this.components)
        {
            compo.Add(c.Name + " TYPE " + c.Type.ToText(level + 1));
        }
        var spaces = JsUtils.Repeat("  ", level);
        return "Structure\n" + spaces + "* " + string.Join("\n" + spaces + "* ", compo);
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override string ToABAP()
    {
        var ret = this.GetQualifiedName();
        if (!string.IsNullOrEmpty(ret))
        {
            return ret;
        }
        return "StructureTypetoABAPtodo";
    }

    public override bool ContainsVoid()
    {
        return this.GetComponents().Any(c =>
        {
            return c.Type.ContainsVoid();
        });
    }

    public override string ToCDS()
    {
        return "abap.TODO_STRUCTURE";
    }
}
