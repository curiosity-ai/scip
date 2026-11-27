// Ported from abaplint packages/core/src/objects/interface.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Objects;

public class Interface : ABAPObject
{
    private sealed class ParsedXML
    {
        public string? name;
        public string? description;
    }

    private IInterfaceDefinition? def = null;
    private ParsedXML? parsedXML = null;

    public Interface(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "INTF";
    }

    public void SetDefinition(IInterfaceDefinition? def)
    {
        this.def = def;
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

    public IInterfaceDefinition? GetDefinition()
    {
        return this.def;
    }

    public InfoInterfaceDefinition? GetInterface()
    {
        return this.GetMainABAPFile()?.GetInfo().GetInterfaceDefinitionByName(this.GetName());
    }

    public override Identifier? GetIdentifier()
    {
        return this.GetInterface()?.Identifier;
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 30, AllowNamespace = true };
    }

    public override void SetDirty()
    {
        this.def = null;
        this.parsedXML = null;
        base.SetDirty();
    }

    public string? GetNameFromXML()
    {
        this.ParseXML();
        return this.parsedXML?.name;
    }

    public override string? GetDescription()
    {
        this.ParseXML();
        return this.parsedXML?.description;
    }

    /////////////////////////

    private void ParseXML()
    {
        if (this.parsedXML != null)
        {
            return;
        }

        this.parsedXML = new ParsedXML();

        var parsed = base.ParseRaw2();
        if (parsed == null
            || parsed["abapGit"]?["asx:abap"]?["asx:values"] == null)
        {
            return;
        }

        var vseo = parsed["abapGit"]?["asx:abap"]?["asx:values"]?["VSEOINTERF"];
        if (vseo == null)
        {
            this.parsedXML.description = "";
            this.parsedXML.name = "";
        }
        else
        {
            this.parsedXML.description = !string.IsNullOrEmpty(vseo["DESCRIPT"]?.Text) ? vseo["DESCRIPT"]!.Text : "";
            this.parsedXML.name = !string.IsNullOrEmpty(vseo["CLSNAME"]?.Text) ? vseo["CLSNAME"]!.Text : "";
        }
    }
}
