// Ported from abaplint packages/core/src/objects/enhancement_spot.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Objects;

public class IBadiDefinition
{
    public string? Name { get; set; }
    public string? Interface { get; set; }
}

public class EnhancementSpot : AbstractObject
{
    private List<IBadiDefinition>? badis;

    public EnhancementSpot(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "ENHS";
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 30, AllowNamespace = true };
    }

    public override void SetDirty()
    {
        this.badis = null;
        base.SetDirty();
    }

    public List<IBadiDefinition> ListBadiDefinitions()
    {
        if (this.badis == null)
        {
            this.badis = this.ParseXML();
        }
        return this.badis;
    }

    /////////////////

    private List<IBadiDefinition> ParseXML()
    {
        var parsed = base.ParseRaw2();
        if (parsed == null)
        {
            return new List<IBadiDefinition>();
        }

        var ret = new List<IBadiDefinition>();
        foreach (var b in XmlUtils.XmlToArray(parsed["abapGit"]?["asx:abap"]?["asx:values"]?["BADI_DATA"], "ENH_BADI_DATA"))
        {
            ret.Add(new IBadiDefinition
            {
                Name = b["BADI_NAME"]?.Text,
                Interface = b["INTERFACE_NAME"]?.Text,
            });
        }

        return ret;
    }
}
