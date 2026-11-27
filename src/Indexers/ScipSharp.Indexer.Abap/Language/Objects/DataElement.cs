// Ported from abaplint packages/core/src/objects/data_element.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Text.Json;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Objects;

using Identifier = ScipSharp.Indexer.Abap.FileInformation.Identifier;
using IdentifierToken = ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier;

public class DataElementTexts
{
    public string? Short { get; set; }
    public string? Medium { get; set; }
    public string? Long { get; set; }
    public string? Heading { get; set; }
}

public class DataElementTextTranslation
{
    public string? Language { get; set; }
    public string? Description { get; set; }
    public string? Short { get; set; }
    public string? Medium { get; set; }
    public string? Long { get; set; }
    public string? Heading { get; set; }
}

public class DataElement : AbstractObject, IParseTypeObject
{
    private sealed class ParsedXML
    {
        public string? description;
        public string? refkind;
        public string? domname;
        public string? datatype;
        public string? leng;
        public DataElementTexts? texts;
        public DataElementTexts? textMaxLengths;
        public List<DataElementTextTranslation>? textsTranslations;
        public string? decimals;
        public string? dtelmaster;
        public string? affCategory;
    }

    private ParsedXML? parsedXML = null;
    private AbstractType? parsedType = null;

    public DataElement(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "DTEL";
    }

    public override string? GetDescription()
    {
        this.Parse();
        return this.parsedXML?.description;
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 30, AllowNamespace = true };
    }

    public override Identifier? GetIdentifier()
    {
        var xmlIdentifier = base.GetIdentifier();
        if (xmlIdentifier != null)
        {
            return xmlIdentifier;
        }

        var file = this.GetAFFFile();
        if (file == null)
        {
            return null;
        }
        return new Identifier(new IdentifierToken(new Position(1, 1), this.GetName()), file.GetFilename());
    }

    public override void SetDirty()
    {
        this.parsedXML = null;
        this.parsedType = null;
        base.SetDirty();
    }

    public string? GetDomainName()
    {
        this.Parse();
        return this.parsedXML?.domname;
    }

    /// <summary>parseType() cannot distinguish DEC, CURR, and QUAN because they all become PackedType.
    /// Return the original DDIC datatype instead, resolving domain-backed data elements when possible.</summary>
    public string? GetDataType(IRegistry? reg = null)
    {
        this.Parse();
        if (!string.IsNullOrEmpty(this.parsedXML?.datatype))
        {
            return this.parsedXML.datatype;
        }
        if (reg != null && this.parsedXML?.refkind == "D" && !string.IsNullOrEmpty(this.parsedXML.domname))
        {
            var domain = reg.GetObject("DOMA", this.parsedXML.domname);
            if (domain is Domain d)
            {
                return d.GetDataType();
            }
        }
        return null;
    }

    public DataElementTexts? GetTexts()
    {
        this.Parse();
        return this.parsedXML?.texts;
    }

    public DataElementTexts? GetTextMaxLengths()
    {
        this.Parse();
        return this.parsedXML?.textMaxLengths;
    }

    public List<DataElementTextTranslation>? GetTextsTranslations()
    {
        this.Parse();
        return this.parsedXML?.textsTranslations;
    }

    public string? GetDtelMaster()
    {
        this.Parse();
        return this.parsedXML?.dtelmaster;
    }

    public AbstractType ParseType(IRegistry reg)
    {
        var references = new List<IObjectAndToken>();
        ILookupResult lookup;

        this.Parse();

        if (this.parsedXML == null)
        {
            lookup = new ILookupResult { Type = new UnknownType("Data Element " + this.GetName() + ", parser error") };
        }
        else
        {
            if (this.parsedType != null)
            {
                return this.parsedType;
            }

            var ddic = new DDIC(reg);
            ILookupResult LookupPredefinedType()
            {
                if (this.parsedXML?.datatype == null || this.parsedXML.datatype == "")
                {
                    return new ILookupResult { Type = new UnknownType("DATATYPE unexpectely empty in " + this.GetName()) };
                }
                return new ILookupResult
                {
                    Type = ddic.TextToType((this.parsedXML.datatype, this.parsedXML.leng, this.parsedXML.decimals, this.GetName(),
                        this.GetName(), null, this.GetName(), this.parsedXML.texts?.Heading)),
                };
            }

            if (this.parsedXML.affCategory == "referenceToPredefinedType")
            {
                var predefined = LookupPredefinedType();
                lookup = predefined.Type is UnknownType
                    ? predefined
                    : new ILookupResult { Type = new DataReference(predefined.Type, this.GetName()) };
            }
            else if (this.parsedXML.affCategory == "referenceDictionaryType")
            {
                if (this.parsedXML.domname == null || this.parsedXML.domname == "")
                {
                    lookup = new ILookupResult { Type = new UnknownType("Type name unexpectely empty in " + this.GetName()) };
                }
                else
                {
                    var referenced = ddic.Lookup(this.parsedXML.domname);
                    lookup = referenced.Type is UnknownType
                        ? referenced
                        : new ILookupResult { Object = referenced.Object, Type = new DataReference(referenced.Type, this.GetName()) };
                }
            }
            else if (this.parsedXML.affCategory == "referenceClasIntType")
            {
                if (this.parsedXML.domname == null || this.parsedXML.domname == "")
                {
                    lookup = new ILookupResult { Type = new UnknownType("Type name unexpectely empty in " + this.GetName()) };
                }
                else
                {
                    lookup = ddic.LookupObject(this.parsedXML.domname);
                }
            }
            else if (this.parsedXML.affCategory != null
                && this.parsedXML.affCategory != "domain"
                && this.parsedXML.affCategory != "predefinedType")
            {
                lookup = new ILookupResult { Type = new UnknownType("Unknown AFF data type category " + this.parsedXML.affCategory + " in " + this.GetName()) };
            }
            else if (this.parsedXML.refkind == "D")
            {
                if (this.parsedXML.domname == null || this.parsedXML.domname == "")
                {
                    lookup = new ILookupResult { Type = new UnknownType("DOMNAME unexpectely empty in " + this.GetName()) };
                }
                else
                {
                    lookup = ddic.LookupDomain(this.parsedXML.domname, this.GetName(), this.GetDescription());
                }
            }
            else if (this.parsedXML.refkind == "R")
            {
                if (this.parsedXML.domname == null || this.parsedXML.domname == "")
                {
                    lookup = new ILookupResult { Type = new UnknownType("DOMNAME unexpectely empty in " + this.GetName()) };
                }
                else
                {
                    lookup = ddic.LookupObject(this.parsedXML.domname);
                }
            }
            else
            {
                lookup = LookupPredefinedType();
            }
        }

        if (lookup.Object != null)
        {
            references.Add(new IObjectAndToken { Object = lookup.Object });
        }
        reg.GetDDICReferences().SetUsing(this, references);

        if (!(lookup.Type is UnknownType))
        {
            // the referenced type might not exist or contain syntax errors(for CLAS)
            // so dont cache it, expect the user to fix it
            this.parsedType = lookup.Type;
        }
        return lookup.Type;
    }

    public override IParseResult Parse(ABAPRelease? _release = null, IReadOnlyList<string>? _globalMacros = null, IRegistry? _reg = null, LanguageVersion? _languageVersion = null)
    {
        if (this.parsedXML != null)
        {
            return new IParseResult { Updated = false, Runtime = 0 };
        }

        var start = Environment.TickCount64;
        this.parsedXML = new ParsedXML();

        var jsonFile = this.GetAFFFile();
        if (jsonFile != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonFile.GetRaw());
                var parsed = doc.RootElement;
                var category = Json.Str(parsed, "dataTypeInformation", "category");
                this.parsedXML = new ParsedXML
                {
                    description = Json.Str(parsed, "header", "description"),
                    affCategory = category,
                    refkind = category == "domain" ? "D" : null,
                    domname = Json.Str(parsed, "dataTypeInformation", "typeName"),
                    datatype = Json.Str(parsed, "dataTypeInformation", "predefinedType", "dataType"),
                    leng = Json.Str(parsed, "dataTypeInformation", "predefinedType", "length"),
                    decimals = Json.Str(parsed, "dataTypeInformation", "predefinedType", "decimals"),
                    texts = new DataElementTexts
                    {
                        Short = Json.Str(parsed, "fieldLabels", "short"),
                        Medium = Json.Str(parsed, "fieldLabels", "medium"),
                        Long = Json.Str(parsed, "fieldLabels", "long"),
                        Heading = Json.Str(parsed, "fieldLabels", "heading"),
                    },
                    textMaxLengths = new DataElementTexts
                    {
                        Short = Json.Str(parsed, "fieldLabels", "shortLength"),
                        Medium = Json.Str(parsed, "fieldLabels", "mediumLength"),
                        Long = Json.Str(parsed, "fieldLabels", "longLength"),
                        Heading = Json.Str(parsed, "fieldLabels", "headingLength"),
                    },
                };
            }
            catch (JsonException)
            {
                // handled by parseType()
            }
            var end1 = Environment.TickCount64;
            return new IParseResult { Updated = true, Runtime = end1 - start };
        }

        var raw = base.ParseRaw2();
        if (raw == null)
        {
            return new IParseResult { Updated = false, Runtime = 0 };
        }

        var values = raw["abapGit"]?["asx:abap"]?["asx:values"];
        var dd04v = values?["DD04V"];
        this.parsedXML = new ParsedXML
        {
            description = dd04v?["DDTEXT"]?.Text,
            refkind = dd04v?["REFKIND"]?.Text,
            domname = dd04v?["DOMNAME"]?.Text,
            datatype = dd04v?["DATATYPE"]?.Text,
            leng = dd04v?["LENG"]?.Text,
            decimals = dd04v?["DECIMALS"]?.Text,
            dtelmaster = dd04v?["DTELMASTER"]?.Text,
            texts = new DataElementTexts
            {
                Short = dd04v?["SCRTEXT_S"]?.Text,
                Medium = dd04v?["SCRTEXT_M"]?.Text,
                Long = dd04v?["SCRTEXT_L"]?.Text,
                Heading = dd04v?["REPTEXT"]?.Text,
            },
            textMaxLengths = new DataElementTexts
            {
                Short = dd04v?["SCRLEN1"]?.Text,
                Medium = dd04v?["SCRLEN2"]?.Text,
                Long = dd04v?["SCRLEN3"]?.Text,
                Heading = dd04v?["HEADLEN"]?.Text,
            },
        };

        this.parsedXML.textsTranslations = new List<DataElementTextTranslation>();
        foreach (var item in XmlUtils.XmlToArray(values?["DD04_TEXTS"], "item"))
        {
            this.parsedXML.textsTranslations.Add(new DataElementTextTranslation
            {
                Language = item["DDLANGUAGE"]?.Text,
                Description = item["DDTEXT"]?.Text,
                Short = item["SCRTEXT_S"]?.Text,
                Medium = item["SCRTEXT_M"]?.Text,
                Long = item["SCRTEXT_L"]?.Text,
                Heading = item["REPTEXT"]?.Text,
            });
        }

        var end = Environment.TickCount64;
        return new IParseResult { Updated = true, Runtime = end - start };
    }

    private IFile? GetAFFFile()
    {
        return this.GetFiles().FirstOrDefault(file => file.GetFilename().ToLowerInvariant().EndsWith(".dtel.json", StringComparison.Ordinal));
    }
}
