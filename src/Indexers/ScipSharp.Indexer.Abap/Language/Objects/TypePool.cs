// Ported from abaplint packages/core/src/objects/type_pool.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap;

namespace ScipSharp.Indexer.Abap.Objects;

public class TypePool : ABAPObject
{
    public TypePool(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "TYPE";
    }

    public override List<ABAPFile> GetSequencedFiles()
    {
        var main = this.GetMainABAPFile();
        if (main == null)
        {
            return new List<ABAPFile>();
        }
        return new List<ABAPFile> { main };
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 5, AllowNamespace = false };
    }

    public override string? GetDescription()
    {
        var parsed = base.ParseRaw2();
        return JsUtils.Or(parsed?["abapGit"]?["asx:abap"]?["asx:values"]?["DDTEXT"]?.Text, "");
    }
}
