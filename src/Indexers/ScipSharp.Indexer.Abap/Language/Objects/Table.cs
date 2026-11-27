// Ported from abaplint packages/core/src/objects/table.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Text.RegularExpressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Objects;

public static class EnhancementCategory
{
    public const string NotClassified = "0";
    public const string CannotBeEnhanced = "1";
    public const string Character = "2";
    public const string CharacterOrNumeric = "3";
    public const string Deep = "4";
}

public static class TableCategory
{
    public const string Transparent = "TRANSP";
    public const string Structure = "INTTAB";
    public const string Cluster = "CLUSTER";
    public const string Pooled = "POOL";
    public const string View = "VIEW";
    public const string Append = "APPEND";
}

public class SecondaryIndex
{
    public string? Name { get; set; }
    public List<string> Fields { get; set; } = new();
}

public class TableField
{
    public string FIELDNAME { get; set; }
    public string? ROLLNAME { get; set; }
    public string? COMPTYPE { get; set; }
    public string? PRECFIELD { get; set; }
    public string? LENG { get; set; }
    public string? INTLEN { get; set; }
    public string? INTTYPE { get; set; }
    public string? DATATYPE { get; set; }
    public string? DECIMALS { get; set; }
    public string? KEYFLAG { get; set; }
    public string? GROUPNAME { get; set; }
    public string? CHECKTABLE { get; set; }
    public string? REFTABLE { get; set; }
    public string? REFFIELD { get; set; }
    public string? REFTYPE { get; set; }
    public string? DDTEXT { get; set; }
}

public class Table : AbstractObject, IParseTypeObject
{
    private sealed class ParsedData
    {
        public string? tableCategory;
        public string? dataClass;
        public string? deliveryClass;
        public string? sizeCategory;
        public string? enhancementCategory;
        public string? description;
        public List<SecondaryIndex>? secondaryIndexes;
        public List<TableField> fields = new();
    }

    private ParsedData? parsedData;
    private AbstractType? parsedType = null;

    public Table(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "TABL";
    }

    public override string? GetDescription()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }

        return this.parsedData?.description;
    }

    public List<SecondaryIndex>? GetSecondaryIndexes()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }

        return this.parsedData?.secondaryIndexes;
    }

    public List<TableField>? GetFields()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }
        return this.parsedData?.fields;
    }

    private static readonly Regex NamingRegex = new Regex(@"^((\/[A-Z_\d]{3,8}\/)|[a-zA-Z0-9]{3}|CI_)\w+$", RegexOptions.ECMAScript);

    public override IAllowedNaming GetAllowedNaming()
    {
        var length = 30;

        if (this.GetTableCategory() == TableCategory.Transparent)
        {
            length = 16;
        }

        return new IAllowedNaming { MaxLength = length, AllowNamespace = true, CustomRegex = NamingRegex };
    }

    public override void SetDirty()
    {
        this.parsedData = null;
        this.parsedType = null;
        base.SetDirty();
    }

    public List<string> ListKeys(IRegistry reg)
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }
        if (this.parsedData == null)
        {
            return new List<string>();
        }

        var ret = new List<string>();
        foreach (var p in this.parsedData.fields)
        {
            if (p.KEYFLAG == "X" && (p.FIELDNAME == ".INCLUDE" || p.FIELDNAME.StartsWith(".INCLU-", StringComparison.Ordinal)))
            {
                object lookup = new DDIC(reg).LookupTableOrView(p.PRECFIELD).Type;
                if (lookup is TypedIdentifier ti)
                {
                    lookup = ti.GetType();
                }
                // same naming as in parseType(), "--AP" are .APPENDS and get no suffix
                var postfix = p.FIELDNAME.StartsWith(".INCLU-", StringComparison.Ordinal) && p.FIELDNAME != ".INCLU--AP"
                    ? p.FIELDNAME.Substring(".INCLU-".Length) : "";
                if (lookup is StructureType st)
                {
                    foreach (var c in st.GetComponents())
                    {
                        ret.Add(c.Name + postfix);
                    }
                }
            }
            else if (p.KEYFLAG == "X")
            {
                ret.Add(p.FIELDNAME);
            }
        }
        return ret;
    }

    public AbstractType ParseType(IRegistry reg)
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
            if (this.parsedData == null)
            {
                return new UnknownType("Table, parser error");
            }
        }

        if (reg.GetConfig().GetLanguageVersion() == LanguageVersion.Cloud
            && this.parsedData.dataClass == "USER3")
        {
            return new UnknownType("Data class = USER3 not allowed in cloud");
        }

        if (this.GetTableCategory() == TableCategory.Transparent)
        {
            if (this.ListKeys(reg).Count == 0)
            {
                return new UnknownType("Table " + this.GetName() + " has no key fields");
            }
            else if (this.KeyFieldsNotFirst())
            {
                return new UnknownType("Table " + this.GetName() + " key fields must be first");
            }
        }

        if (this.parsedType != null)
        {
            return this.parsedType;
        }

        var references = new List<IObjectAndToken>();
        var components = new List<IStructureComponent>();
        var ddic = new DDIC(reg);
        foreach (var field in this.parsedData.fields)
        {
            var comptype = !string.IsNullOrEmpty(field.COMPTYPE) ? field.COMPTYPE : "";
            if (comptype == "E")
            {
                // data element
                var lookup = ddic.LookupDataElement(field.ROLLNAME);
                components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = lookup.Type });
                if (lookup.Object != null)
                {
                    references.Add(new IObjectAndToken { Object = lookup.Object });
                }
            }
            else if (field.FIELDNAME == ".INCLUDE"
                || field.FIELDNAME.StartsWith(".INCLU-", StringComparison.Ordinal))
            {
                if (field.PRECFIELD == null)
                {
                    return new UnknownType("Table, parser error, PRECFIELD undefined, " + this.GetName());
                }
                var lookup = ddic.LookupTableOrView(field.PRECFIELD);
                object found = lookup.Type;
                if (lookup.Object != null)
                {
                    references.Add(new IObjectAndToken { Object = lookup.Object });
                }
                if (found is TypedIdentifier ti)
                {
                    found = ti.GetType();
                }
                if (found is StructureType st)
                {
                    if (field.GROUPNAME != null)
                    {
                        components.Add(new IStructureComponent { Name = field.GROUPNAME, Type = st, AsInclude = true });
                    }

                    // Note: "--AP" are .APPENDS
                    if (field.FIELDNAME.StartsWith(".INCLU-", StringComparison.Ordinal) == false || field.FIELDNAME == ".INCLU--AP")
                    {
                        foreach (var c in st.GetComponents())
                        {
                            components.Add(new IStructureComponent { Name = c.Name, Type = c.Type });
                        }
                    }
                    else if (field.FIELDNAME.StartsWith(".INCLU-", StringComparison.Ordinal))
                    {
                        var postfix = field.FIELDNAME.Substring(".INCLU-".Length);
                        foreach (var c in st.GetComponents())
                        {
                            components.Add(new IStructureComponent { Name = c.Name + postfix, Type = c.Type });
                        }
                    }
                }
                else if ((field.PRECFIELD.StartsWith("CI_", StringComparison.Ordinal) || field.PRECFIELD.StartsWith("SI_", StringComparison.Ordinal))
                    && found is UnknownType)
                {
                    continue;
                }
                else if (found is UnknownType ut)
                {
                    return ut;
                }
                else if (found is VoidType vt)
                {
                    // set the full structure to void
                    return vt;
                }
                else
                {
                    components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = (AbstractType)found });
                }
            }
            else if (comptype == "S")
            {
                var lookup = ddic.LookupTableOrView(field.ROLLNAME);
                components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = lookup.Type });
                if (lookup.Object != null)
                {
                    references.Add(new IObjectAndToken { Object = lookup.Object });
                }
            }
            else if (comptype == "R")
            {
                if (field.ROLLNAME == null)
                {
                    throw new Exception("Expected ROLLNAME");
                }
                if (field.ROLLNAME == "DATA")
                {
                    components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = new DataReference(AnyType.Get()) });
                }
                else if (field.ROLLNAME == "OBJECT")
                {
                    components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = new GenericObjectReferenceType() });
                }
                else if (field.REFTYPE == "S")
                {
                    var lookup = ddic.LookupTableOrView(field.ROLLNAME);
                    components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = new DataReference(lookup.Type) });
                    if (lookup.Object != null)
                    {
                        references.Add(new IObjectAndToken { Object = lookup.Object });
                    }
                }
                else if (field.REFTYPE == "L")
                {
                    var lookup = ddic.LookupTableType(field.ROLLNAME);
                    components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = new DataReference(lookup.Type) });
                    if (lookup.Object != null)
                    {
                        references.Add(new IObjectAndToken { Object = lookup.Object });
                    }
                }
                else if (field.REFTYPE == "E")
                {
                    var lookup = ddic.LookupDataElement(field.ROLLNAME);
                    components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = new DataReference(lookup.Type) });
                    if (lookup.Object != null)
                    {
                        references.Add(new IObjectAndToken { Object = lookup.Object });
                    }
                }
                else
                {
                    var lookup = ddic.LookupObject(field.ROLLNAME);
                    components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = lookup.Type });
                    if (lookup.Object != null)
                    {
                        references.Add(new IObjectAndToken { Object = lookup.Object });
                    }
                }
            }
            else if (comptype == "L")
            {
                var lookup = ddic.LookupTableType(field.ROLLNAME);
                components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = lookup.Type });
                if (lookup.Object != null)
                {
                    references.Add(new IObjectAndToken { Object = lookup.Object });
                }
            }
            else if (comptype == "")
            {
                // built in
                var datatype = field.DATATYPE;
                if (datatype == null)
                {
                    throw new Exception("Expected DATATYPE, while parsing TABL " + this.GetName());
                }
                var length = !string.IsNullOrEmpty(field.LENG) ? field.LENG : field.INTLEN;
                components.Add(new IStructureComponent
                {
                    Name = field.FIELDNAME,
                    Type = ddic.TextToType((datatype, length, field.DECIMALS, this.GetName() + "-" + field.FIELDNAME, null, null, null, field.DDTEXT)),
                });
            }
            else
            {
                components.Add(new IStructureComponent { Name = field.FIELDNAME, Type = new UnknownType("Table " + this.GetName() + ", unknown component type \"" + comptype + "\"") });
            }

            if (!string.IsNullOrEmpty(field.CHECKTABLE))
            {
                var lookup = ddic.LookupTableOrView2(field.CHECKTABLE);
                if (lookup != null)
                {
                    references.Add(new IObjectAndToken { Object = lookup });
                }
            }
        }

        if (components.Count == 0)
        {
            return new UnknownType("Table/Structure " + this.GetName() + " does not contain any components");
        }

        reg.GetDDICReferences().SetUsing(this, references);
        this.parsedType = new StructureType(components, this.GetName(), this.GetName(), this.GetDescription());
        return this.parsedType;
    }

    public string? GetTableCategory()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }

        return this.parsedData?.tableCategory;
    }

    public string? GetDeliveryClass()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }

        return this.parsedData?.deliveryClass;
    }

    public string? GetSizeCategory()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }

        return this.parsedData?.sizeCategory;
    }

    public string GetEnhancementCategory()
    {
        if (this.parsedData == null)
        {
            this.ParseXML();
        }
        if (this.parsedData?.enhancementCategory == null)
        {
            return EnhancementCategory.NotClassified;
        }
        return this.parsedData.enhancementCategory;
    }

    ///////////////

    private bool KeyFieldsNotFirst()
    {
        var seenNonKey = false;
        foreach (var field in this.parsedData?.fields ?? new List<TableField>())
        {
            if (field.KEYFLAG == "X")
            {
                if (seenNonKey == true)
                {
                    return true;
                }
            }
            else
            {
                seenNonKey = true;
            }
        }
        return false;
    }

    private void ParseXML()
    {
        var parsed = base.ParseRaw2();
        if (parsed == null)
        {
            return;
        }

        this.parsedData = new ParsedData();

        if (parsed["abapGit"] == null)
        {
            return;
        }

        var values = parsed["abapGit"]?["asx:abap"]?["asx:values"];

        // enhancement category
        if (values?["DD02V"]?["EXCLASS"] == null)
        {
            this.parsedData.enhancementCategory = EnhancementCategory.NotClassified;
        }
        else
        {
            this.parsedData.enhancementCategory = values?["DD02V"]?["EXCLASS"]?.Text;
        }

        // table category
        this.parsedData.tableCategory = values?["DD02V"]?["TABCLASS"]?.Text;
        this.parsedData.description = values?["DD02V"]?["DDTEXT"]?.Text;
        this.parsedData.dataClass = values?["DD09L"]?["TABART"]?.Text;
        this.parsedData.deliveryClass = values?["DD02V"]?["CONTFLAG"]?.Text;
        this.parsedData.sizeCategory = values?["DD09L"]?["TABKAT"]?.Text;

        // fields
        var fields = values?["DD03P_TABLE"];
        foreach (var field in XmlUtils.XmlToArray(fields, "DD03P"))
        {
            if (field["FIELDNAME"]?.Text == null)
            {
                continue;
            }
            this.parsedData.fields.Add(new TableField
            {
                FIELDNAME = field["FIELDNAME"]!.Text!,
                ROLLNAME = field["ROLLNAME"]?.Text,
                COMPTYPE = field["COMPTYPE"]?.Text,
                PRECFIELD = field["PRECFIELD"]?.Text,
                LENG = field["LENG"]?.Text,
                INTLEN = field["INTLEN"]?.Text,
                INTTYPE = field["INTTYPE"]?.Text,
                DATATYPE = field["DATATYPE"]?.Text,
                DECIMALS = field["DECIMALS"]?.Text,
                KEYFLAG = field["KEYFLAG"]?.Text,
                GROUPNAME = field["GROUPNAME"]?.Text,
                CHECKTABLE = field["CHECKTABLE"]?.Text,
                REFTABLE = field["REFTABLE"]?.Text,
                REFFIELD = field["REFFIELD"]?.Text,
                REFTYPE = field["REFTYPE"]?.Text,
                DDTEXT = field["DDTEXT"]?.Text,
            });
        }

        // secondary indexes
        var indexes = values?["DD12V"];
        this.parsedData.secondaryIndexes = new List<SecondaryIndex>();
        foreach (var index in XmlUtils.XmlToArray(indexes, "DD12V"))
        {
            var indexName = index["INDEXNAME"]?.Text;
            var indexFields = new List<string>();
            var indexFieldsXml = values?["DD17V"];
            foreach (var indexField in XmlUtils.XmlToArray(indexFieldsXml, "DD17V"))
            {
                if (indexField["INDEXNAME"]?.Text == indexName)
                {
                    // assumption: fields are listed by POSITION in the xml
                    indexFields.Add(indexField["FIELDNAME"]?.Text!);
                }
            }
            this.parsedData.secondaryIndexes.Add(new SecondaryIndex { Name = indexName, Fields = indexFields });
        }
    }
}
