// Ported from abaplint packages/core/src/objects/authorization_check_field.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Objects;

public class AuthorizationCheckField : AbstractObject, IParseTypeObject
{
    private sealed class ParsedXML
    {
        public string? rollname;
    }

    private ParsedXML? parsedXML;

    public AuthorizationCheckField(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "AUTH";
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 30, AllowNamespace = true };
    }

    public override void SetDirty()
    {
        this.parsedXML = null;
        base.SetDirty();
    }

    public string? GetDataElementName()
    {
        this.Parse();
        return this.parsedXML?.rollname;
    }

    public AbstractType ParseType(IRegistry reg)
    {
        this.Parse();

        var references = new List<IObjectAndToken>();
        var ddic = new DDIC(reg);

        if (!string.IsNullOrEmpty(this.parsedXML?.rollname))
        {
            var found = ddic.LookupDataElement(this.parsedXML.rollname);
            if (found.Object != null)
            {
                references.Add(new IObjectAndToken { Object = found.Object });
                reg.GetDDICReferences().SetUsing(this, references);
                return found.Type;
            }
            else if (ddic.InErrorNamespace(this.parsedXML.rollname))
            {
                return new UnknownType(this.parsedXML.rollname + " not found");
            }
            else
            {
                return VoidType.Get(this.parsedXML.rollname);
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

        if (parsed == null || parsed["abapGit"] == null || parsed["abapGit"]?["asx:abap"]?["asx:values"] == null)
        {
            return new IParseResult { Updated = false, Runtime = 0 };
        }

        this.parsedXML.rollname = parsed["abapGit"]?["asx:abap"]?["asx:values"]?["AUTHX"]?["ROLLNAME"]?.Text;

        var end = Environment.TickCount64;
        return new IParseResult { Updated = true, Runtime = end - start };
    }
}
