// Ported from abaplint packages/core/src/objects/program.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap;

namespace ScipSharp.Indexer.Abap.Objects;

public class Program : ABAPObject, IIncludeProgram
{
    private sealed class ParsedXML
    {
        public bool isInclude;
        public bool isModulePool;
        public string? description;
        public List<DynproHeader> dynpros = new();
        public ITextElements selectionTexts = new();
    }

    private ParsedXML? parsedXML;

    public Program(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "PROG";
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

    public override string? GetDescription()
    {
        this.ParseXML();
        return this.parsedXML?.description;
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 40, AllowNamespace = true };
    }

    public override void SetDirty()
    {
        this.parsedXML = null;
        base.SetDirty();
    }

    public bool IsInclude()
    {
        this.ParseXML();
        return this.parsedXML!.isInclude;
    }

    public bool IsModulePool()
    {
        this.ParseXML();
        return this.parsedXML!.isModulePool;
    }

    public List<DynproHeader> GetDynpros()
    {
        this.ParseXML();
        return this.parsedXML!.dynpros ?? new List<DynproHeader>();
    }

    public ITextElements GetSelectionTexts()
    {
        this.ParseXML();
        return this.parsedXML!.selectionTexts;
    }

    ////////////////////////////

    private void ParseXML()
    {
        if (this.parsedXML != null)
        {
            return;
        }

        var file = this.GetXMLFile();
        var parsed = this.ParseRaw2();
        if (parsed == null)
        {
            this.parsedXML = new ParsedXML
            {
                isInclude = false,
                isModulePool = false,
                description = null,
                dynpros = new List<DynproHeader>(),
                selectionTexts = new ITextElements(),
            };
            return;
        }

        var description = "";
        var selectionTexts = new ITextElements();
        foreach (var t in XmlUtils.XmlToArray(parsed["abapGit"]?["asx:abap"]?["asx:values"]?["TPOOL"], "item"))
        {
            var entry = t["ENTRY"]?.Text;
            if (t["ID"]?.Text == "R")
            {
                description = !string.IsNullOrEmpty(entry) ? XmlUtils.Unescape(entry) : "";
            }
            else if (t["ID"]?.Text == "S" && t["KEY"]?.Text != null)
            {
                selectionTexts[JsString.ToUpper(t["KEY"]!.Text!)] = new ITextElement(!string.IsNullOrEmpty(entry) ? XmlUtils.Unescape(entry) : "", JsUtils.ParseIntOrNaN(t["LENGTH"]?.Text, 10));
            }
        }

        var dynpros = Dynpros.ParseDynpros(parsed);

        this.parsedXML = new ParsedXML
        {
            isInclude = file != null && file.GetRaw().Contains("<SUBC>I</SUBC>", StringComparison.Ordinal),
            isModulePool = file != null && file.GetRaw().Contains("<SUBC>M</SUBC>", StringComparison.Ordinal),
            dynpros = dynpros,
            description = description,
            selectionTexts = selectionTexts,
        };
    }
}
