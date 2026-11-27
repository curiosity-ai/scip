// Ported from abaplint packages/core/src/abap/types/type_definitions.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types;

public class TypeDefinitions : ITypeDefinitions
{
    private readonly List<TypeDefinitionsEntry> list;
    private readonly Dictionary<string, TypeDefinitionsEntry> map = new Dictionary<string, TypeDefinitionsEntry>();

    public TypeDefinitions(List<TypeDefinitionsEntry> list)
    {
        this.list = list;
        foreach (var t in list)
        {
            // todo, can assumptions be made regarding the case of t.getName()?
            this.map[JsUtils.ToUpper(t.Type.GetName())] = t;
        }
    }

    public List<TypeDefinitionsEntry> GetAll()
    {
        return this.list;
    }

    public TypedIdentifier? GetByName(string name)
    {
        return this.map.GetValueOrDefault(JsUtils.ToUpper(name))?.Type;
    }
}
