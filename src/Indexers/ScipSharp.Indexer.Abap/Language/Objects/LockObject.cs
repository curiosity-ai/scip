// Ported from abaplint packages/core/src/objects/lock_object.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Text.RegularExpressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Objects;

public class LockObjectParameter
{
    public string VIEWFIELD { get; set; }
    public string? TABNAME { get; set; }
    public string? FIELDNAME { get; set; }
}

public class LockObject : AbstractObject, IParseTypeObject
{
    private sealed class ParsedXML
    {
        public string? primaryTable;
        public string? description;
        public List<LockObjectParameter>? parameters;
    }

    private ParsedXML? parsedXML;

    public LockObject(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "ENQU";
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming
        {
            MaxLength = 16,
            CustomRegex = new Regex(@"^(\/[A-Z_\d]{3,8}\/)?E[A-Z0-9_]+$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase),
            AllowNamespace = true,
        };
    }

    public override void SetDirty()
    {
        this.parsedXML = null;
        base.SetDirty();
    }

    public string? GetPrimaryTable()
    {
        this.Parse();
        return this.parsedXML?.primaryTable;
    }

    public List<LockObjectParameter>? GetParameters()
    {
        this.Parse();
        return this.parsedXML?.parameters;
    }

    public AbstractType ParseType(IRegistry reg)
    {
        this.Parse();

        var references = new List<IObjectAndToken>();
        var ddic = new DDIC(reg);

        if (!string.IsNullOrEmpty(this.parsedXML?.primaryTable))
        {
            var found = ddic.LookupTableOrView2(this.parsedXML.primaryTable);
            if (found != null)
            {
                references.Add(new IObjectAndToken { Object = found });
                reg.GetDDICReferences().SetUsing(this, references);
                return found.ParseType(reg);
            }
            else if (ddic.InErrorNamespace(this.parsedXML.primaryTable))
            {
                return new UnknownType(this.parsedXML.primaryTable + " not found");
            }
            else
            {
                return VoidType.Get(this.parsedXML.primaryTable);
            }
        }
        else
        {
            return new UnknownType("Parsing error");
        }
    }

    public override IParseResult Parse(ABAPRelease? _release = null, IReadOnlyList<string>? _globalMacros = null, IRegistry? _reg = null, LanguageVersion? _languageVersion = null)
    {
        if (this.parsedXML != null)
        {
            return new IParseResult { Updated = false, Runtime = 0 };
        }

        var start = Environment.TickCount64;
        this.parsedXML = new ParsedXML();
        var parsed = base.ParseRaw2();

        if (parsed == null
            || parsed["abapGit"] == null
            || parsed["abapGit"]?["asx:abap"]?["asx:values"] == null)
        {
            return new IParseResult { Updated = false, Runtime = 0 };
        }

        var values = parsed["abapGit"]?["asx:abap"]?["asx:values"];
        this.parsedXML.primaryTable = values?["DD25V"]?["ROOTTAB"]?.Text;
        this.parsedXML.description = values?["DD25V"]?["DDTEXT"]?.Text;

        this.parsedXML.parameters = new List<LockObjectParameter>();
        var parameters = values?["DD27P_TABLE"];
        foreach (var parameter in XmlUtils.XmlToArray(parameters, "DD27P"))
        {
            if (parameter["VIEWFIELD"]?.Text == null)
            {
                continue;
            }
            this.parsedXML.parameters.Add(new LockObjectParameter
            {
                VIEWFIELD = parameter["VIEWFIELD"]!.Text!,
                TABNAME = parameter["TABNAME"]?.Text,
                FIELDNAME = parameter["FIELDNAME"]?.Text,
            });
        }

        var end = Environment.TickCount64;
        return new IParseResult { Updated = true, Runtime = end - start };
    }

    public override string? GetDescription()
    {
        this.Parse();
        return this.parsedXML?.description;
    }
}
