// Ported from abaplint packages/core/src/objects/class.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Objects;

public static class ClassCategory
{
    public const string Test = "05";
    public const string Persistent = "10";
    public const string PersistentFactory = "11";
    public const string Exception = "40";
    public const string SharedObject = "45";
}

public class Class : ABAPObject
{
    private sealed class ParsedXML
    {
        public string? name;
        public string? description;
        public string? category;
    }

    private IClassDefinition? def = null;
    private ParsedXML? parsedXML = null;

    public Class(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "CLAS";
    }

    public override List<ABAPFile> GetSequencedFiles()
    {
        var sequence = new List<string> { ".clas.locals_def.abap", ".clas.locals_imp.abap", ".clas.abap", ".clas.testclasses.abap" };
        var copy = JsUtils.Sort(this.GetABAPFiles().ToList(), (a, b) =>
        {
            var aValue = JsUtils.FindIndex(sequence, s => a.GetFilename().EndsWith(s, StringComparison.Ordinal));
            var bValue = JsUtils.FindIndex(sequence, s => b.GetFilename().EndsWith(s, StringComparison.Ordinal));
            return aValue - bValue;
        });
        return copy;
    }

    public void SetDefinition(IClassDefinition? def)
    {
        this.def = def;
    }

    public IClassDefinition? GetDefinition()
    {
        return this.def;
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

    public InfoClassDefinition? GetClassDefinition()
    {
        return this.GetMainABAPFile()?.GetInfo().GetClassDefinitionByName(this.GetName());
    }

    public override Identifier? GetIdentifier()
    {
        return this.GetClassDefinition()?.Identifier;
    }

    // -------------------

    public override string? GetDescription()
    {
        this.ParseXML();
        return this.parsedXML?.description;
    }

    public string? GetNameFromXML()
    {
        this.ParseXML();
        return this.parsedXML?.name;
    }

    public string? GetCategory()
    {
        this.ParseXML();
        return this.parsedXML?.category;
    }

    public ABAPFile? GetLocalsImpFile()
    {
        foreach (var file in this.GetABAPFiles())
        {
            if (file.GetFilename().EndsWith(".clas.locals_imp.abap", StringComparison.Ordinal))
            {
                return file;
            }
        }
        return null;
    }

    public ABAPFile? GetTestclassFile()
    {
        foreach (var file in this.GetABAPFiles())
        {
            if (file.GetFilename().EndsWith(".clas.testclasses.abap", StringComparison.Ordinal))
            {
                return file;
            }
        }
        return null;
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
        if (parsed?["abapGit"]?["asx:abap"]?["asx:values"] == null)
        {
            return;
        }

        var vseo = parsed["abapGit"]?["asx:abap"]?["asx:values"]?["VSEOCLASS"];
        if (vseo == null)
        {
            return;
        }

        this.parsedXML.category = vseo["CATEGORY"]?.Text;
        this.parsedXML.description = !string.IsNullOrEmpty(vseo["DESCRIPT"]?.Text) ? vseo["DESCRIPT"]!.Text : "";
        this.parsedXML.name = !string.IsNullOrEmpty(vseo["CLSNAME"]?.Text) ? vseo["CLSNAME"]!.Text : "";
    }
}
