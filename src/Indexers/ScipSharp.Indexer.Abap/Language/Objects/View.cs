// Ported from abaplint packages/core/src/objects/view.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Objects;

public class ViewField
{
    public string VIEWFIELD { get; set; }
    public string TABNAME { get; set; }
    public string FIELDNAME { get; set; }
    public string? KEYFLAG { get; set; }
}

public class ViewJoin
{
    public string? LTAB { get; set; }
    public string? LFIELD { get; set; }
    public string? OPERATOR { get; set; }
    public string? RTAB { get; set; }
    public string? RFIELD { get; set; }
}

public class View : AbstractObject, IParseTypeObject
{
    private static class ViewClass
    {
        public const string ExternalView = "X";
    }

    private sealed class ParsedData
    {
        public (string VIEWCLASS, string DDTEXT) header;
        public List<ViewField> fields = new();
        public List<ViewJoin> join = new();
    }

    private ParsedData? parsedData;

    public View(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "VIEW";
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 16, AllowNamespace = true };
    }

    public List<ViewField>? GetFields()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }
        return this.parsedData?.fields;
    }

    public List<ViewJoin>? GetJoin()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }
        return this.parsedData?.join;
    }

    public override void SetDirty()
    {
        this.parsedData = null;
        base.SetDirty();
    }

    public AbstractType ParseType(IRegistry reg)
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }
        if (this.parsedData == null)
        {
            return new UnknownType("View, parser error", this.GetName());
        }

        var components = new List<IStructureComponent>();
        var references = new List<IObjectAndToken>();

        var ddic = new DDIC(reg);
        foreach (var field in this.parsedData.fields)
        {
            if (field.VIEWFIELD == "*" || field.VIEWFIELD == "-")
            {
                // ignore, this is a special case of old style .INCLUDE
                continue;
            }
            else if (this.parsedData.header.VIEWCLASS == ViewClass.ExternalView)
            {
                components.Add(new IStructureComponent { Name = field.VIEWFIELD, Type = VoidType.Get("ExternalView") });
                continue;
            }
            else if (field.TABNAME == this.GetName())
            {
                throw new Exception("Unexpected self reference in view " + this.GetName() + ", " + field.FIELDNAME + " " + field.FIELDNAME);
            }

            var lookup = ddic.LookupTableOrView(field.TABNAME);
            var found = lookup.Type;
            if (lookup.Object != null)
            {
                references.Add(new IObjectAndToken { Object = lookup.Object });
            }
            if (field.VIEWFIELD == ".APPEND")
            {
                // it is already expanded in the abapGit xml
                continue;
            }
            if (found is StructureType st)
            {
                var s = st.GetComponentByName(field.FIELDNAME);
                if (s == null)
                {
                    found = new UnknownType(field.FIELDNAME + " not found in " + field.TABNAME + ", VIEW parse type");
                }
                else
                {
                    found = s;
                }
            }
            components.Add(new IStructureComponent { Name = field.VIEWFIELD, Type = found });
        }

        reg.GetDDICReferences().SetUsing(this, references);
        if (components.Count == 0)
        {
            return new UnknownType("View " + this.GetName() + " does not contain any components");
        }

        return new StructureType(components, this.GetName());
    }

    public List<string> ListKeys()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }

        var ret = new List<string>();
        foreach (var p in this.parsedData?.fields ?? new List<ViewField>())
        {
            if (p.KEYFLAG == "X")
            {
                ret.Add(p.FIELDNAME);
            }
        }
        return ret;
    }

    public override string? GetDescription()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }
        return this.parsedData?.header.DDTEXT;
    }

    ///////////////

    private void ParseXML()
    {
        this.parsedData = new ParsedData { header = ("", "") };

        var parsed = base.ParseRaw2();
        if (parsed == null || parsed["abapGit"] == null)
        {
            return;
        }

        var values = parsed["abapGit"]?["asx:abap"]?["asx:values"];
        var header = values?["DD25V"];
        this.parsedData.header = (JsUtils.Or(header?["VIEWCLASS"]?.Text, "")!, JsUtils.Or(header?["DDTEXT"]?.Text, "")!);

        var fields = values?["DD27P_TABLE"];
        foreach (var field in XmlUtils.XmlToArray(fields, "DD27P"))
        {
            if (field["VIEWFIELD"]?.Text == null
                || field["TABNAME"]?.Text == null
                || field["FIELDNAME"]?.Text == null)
            {
                continue;
            }
            this.parsedData.fields.Add(new ViewField
            {
                VIEWFIELD = field["VIEWFIELD"]!.Text!,
                TABNAME = field["TABNAME"]!.Text!,
                FIELDNAME = field["FIELDNAME"]!.Text!,
                KEYFLAG = field["KEYFLAG"]?.Text,
            });
        }

        var join = values?["DD28J_TABLE"];
        foreach (var j in XmlUtils.XmlToArray(join, "DD28J"))
        {
            this.parsedData.join.Add(new ViewJoin
            {
                LTAB = j["LTAB"]?.Text,
                LFIELD = j["LFIELD"]?.Text,
                OPERATOR = j["OPERATOR"]?.Text,
                RTAB = j["RTAB"]?.Text,
                RFIELD = j["RFIELD"]?.Text,
            });
        }
    }
}
