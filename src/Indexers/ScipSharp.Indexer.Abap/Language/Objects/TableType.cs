// Ported from abaplint packages/core/src/objects/table_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Objects;

using Types = ScipSharp.Indexer.Abap.Types.Basic;

public class TableType : AbstractObject, IParseTypeObject
{
    private sealed class ParsedXML
    {
        public string? rowtype;
        public string? rowkind;
        public string? datatype;
        public string? leng;
        public string? decimals;
        public string? accessmode;
        public string? keykind;
        public string? ddtext;
        public string? keydef;
        public List<(string keyname, string keyfield)> dd42v = new();
        public List<(string keyname, string accessmode, string kind, bool unique)> dd43v = new();
    }

    private ParsedXML? parsedXML = null;

    public TableType(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "TTYP";
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 30, AllowNamespace = true };
    }

    public override string? GetDescription()
    {
        this.ParseXML();
        return this.parsedXML?.ddtext;
    }

    public string? GetRowType()
    {
        this.ParseXML();
        return this.parsedXML?.rowtype;
    }

    public override void SetDirty()
    {
        this.parsedXML = null;
        base.SetDirty();
    }

    private ITableKey BuildPrimaryKey()
    {
        var primaryKey = new ITableKey
        {
            IsUnique = this.parsedXML?.keykind == "U",
            Type = TableAccessType.standard,
            KeyFields = new List<string>(),
            Name = "primary_key",
        };
        if (this.parsedXML?.accessmode == "S")
        {
            primaryKey.Type = TableAccessType.sorted;
        }
        else if (this.parsedXML?.accessmode == "H")
        {
            primaryKey.Type = TableAccessType.hashed;
        }

        foreach (var f in this.parsedXML?.dd42v ?? new())
        {
            if (f.keyname == "")
            {
                primaryKey.KeyFields.Add(f.keyfield);
            }
        }
        if (this.parsedXML?.keydef == "T" && primaryKey.KeyFields.Count == 0)
        {
            primaryKey.KeyFields.Add("table_line");
        }
        return primaryKey;
    }

    private ITableOptions BuildTableOptions()
    {
        var keyType = TableKeyType.user;
        if (this.parsedXML?.keydef == "D")
        {
            keyType = TableKeyType.@default;
        }

        var tableOptions = new ITableOptions
        {
            WithHeader = false,
            KeyType = keyType,
            PrimaryKey = this.BuildPrimaryKey(),
            Secondary = new List<ITableKey>(),
        };

        foreach (var k in this.parsedXML?.dd43v ?? new())
        {
            var fields = new List<string>();
            foreach (var f in this.parsedXML?.dd42v ?? new())
            {
                if (f.keyname == k.keyname)
                {
                    fields.Add(f.keyfield);
                }
            }
            var accessType = TableAccessType.standard;
            switch (k.accessmode)
            {
                case "S":
                    accessType = TableAccessType.sorted;
                    break;
                case "H":
                    accessType = TableAccessType.hashed;
                    break;
                default:
                    break;
            }
            tableOptions.Secondary?.Add(new ITableKey
            {
                Name = k.keyname,
                Type = accessType,
                KeyFields = fields,
                IsUnique = k.unique,
            });
        }

        return tableOptions;
    }

    public AbstractType ParseType(IRegistry reg)
    {
        this.ParseXML();

        var ddic = new DDIC(reg);

        var references = new List<IObjectAndToken>();
        AbstractType type;
        var tableOptions = this.BuildTableOptions();

        if (this.parsedXML == null)
        {
            type = new UnknownType("Table Type, parser error", this.GetName());
        }
        else if (this.parsedXML.rowkind == "S")
        {
            var lookup = ddic.LookupTableOrView(this.parsedXML.rowtype);
            type = new Types.TableType(lookup.Type, tableOptions, this.GetName(), this.GetDescription());
            if (lookup.Object != null)
            {
                references.Add(new IObjectAndToken { Object = lookup.Object });
            }
        }
        else if (this.parsedXML.rowkind == "E")
        {
            var lookup = ddic.LookupDataElement(this.parsedXML.rowtype);
            type = new Types.TableType(lookup.Type, tableOptions, this.GetName(), this.GetDescription());
            if (lookup.Object != null)
            {
                references.Add(new IObjectAndToken { Object = lookup.Object });
            }
        }
        else if (this.parsedXML.rowkind == "L")
        {
            var lookup = ddic.LookupTableType(this.parsedXML.rowtype);
            type = new Types.TableType(lookup.Type, tableOptions, this.GetName(), this.GetDescription());
            if (lookup.Object != null)
            {
                references.Add(new IObjectAndToken { Object = lookup.Object });
            }
        }
        else if (this.parsedXML.rowkind == "R" && this.parsedXML.rowtype == "OBJECT")
        {
            type = new Types.TableType(new GenericObjectReferenceType(), tableOptions, this.GetName(), this.GetDescription());
        }
        else if (this.parsedXML.rowkind == "R" && this.parsedXML.rowtype == "DATA")
        {
            type = new Types.TableType(new DataReference(new DataType()), tableOptions, this.GetName(), this.GetDescription());
        }
        else if (this.parsedXML.rowkind == "R" && this.parsedXML.rowtype != null)
        {
            var lookup = ddic.LookupObject(this.parsedXML.rowtype);
            type = new Types.TableType(lookup.Type, tableOptions, this.GetName(), this.GetDescription());
            if (lookup.Object != null)
            {
                references.Add(new IObjectAndToken { Object = lookup.Object });
            }
        }
        else if (this.parsedXML.rowkind == "")
        {
            if (this.parsedXML.datatype == null)
            {
                type = new UnknownType("Table Type, empty DATATYPE" + this.GetName(), this.GetName());
            }
            else
            {
                var row = ddic.TextToType((this.parsedXML.datatype, this.parsedXML.leng, this.parsedXML.decimals, this.GetName(), null, null, null, null));
                type = new Types.TableType(row, tableOptions, this.GetName(), this.GetDescription());
            }
        }
        else
        {
            type = new UnknownType("Table Type, unknown kind \"" + JsUtils.Str(this.parsedXML.rowkind) + "\"" + this.GetName(), this.GetName());
        }

        reg.GetDDICReferences().SetUsing(this, references);
        return type;
    }

    ////////////////////

    private void ParseXML()
    {
        if (this.parsedXML != null)
        {
            return;
        }

        this.parsedXML = new ParsedXML();

        var parsed = base.ParseRaw2();
        if (parsed == null || parsed["abapGit"] == null)
        {
            return;
        }

        var values = parsed["abapGit"]?["asx:abap"]?["asx:values"];
        if (values == null)
        {
            return;
        }

        var dd40v = values["DD40V"];
        if (dd40v == null)
        {
            return;
        }
        this.parsedXML.rowtype = !string.IsNullOrEmpty(dd40v["ROWTYPE"]?.Text) ? dd40v["ROWTYPE"]!.Text : "";
        this.parsedXML.rowkind = !string.IsNullOrEmpty(dd40v["ROWKIND"]?.Text) ? dd40v["ROWKIND"]!.Text : "";
        this.parsedXML.datatype = dd40v["DATATYPE"]?.Text;
        this.parsedXML.leng = dd40v["LENG"]?.Text;
        this.parsedXML.decimals = dd40v["DECIMALS"]?.Text;
        this.parsedXML.accessmode = dd40v["ACCESSMODE"]?.Text;
        this.parsedXML.keykind = dd40v["KEYKIND"]?.Text;
        this.parsedXML.ddtext = dd40v["DDTEXT"]?.Text;
        this.parsedXML.keydef = dd40v["KEYDEF"]?.Text;

        foreach (var x in XmlUtils.XmlToArray(values["DD42V"], "DD42V"))
        {
            this.parsedXML.dd42v.Add((JsUtils.Or(x["SECKEYNAME"]?.Text, "")!, JsUtils.Or(x["KEYFIELD"]?.Text, "")!));
        }
        foreach (var x in XmlUtils.XmlToArray(values["DD43V"], "DD43V"))
        {
            this.parsedXML.dd43v.Add((JsUtils.Or(x["SECKEYNAME"]?.Text, "")!, JsUtils.Or(x["ACCESSMODE"]?.Text, "")!, JsUtils.Or(x["KIND"]?.Text, "")!, x["SECKEYUNIQUE"]?.Text == "X"));
        }
    }
}
