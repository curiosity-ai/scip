// Ported from abaplint packages/core/src/objects/domain.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Text.Json;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Objects;

using Identifier = ScipSharp.Indexer.Abap.FileInformation.Identifier;
using IdentifierToken = ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier;

public class DomainValue
{
    public string? Language { get; set; }
    public string? Low { get; set; }
    public string? High { get; set; }
    public string? Description { get; set; }
}

public class DomainValueTranslation
{
    public string? Language { get; set; }
    public string? Description { get; set; }
}

public class Domain : AbstractObject, IParseTypeObject
{
    private sealed class ParsedXML
    {
        public string? description;
        public string? datatype;
        public string? length;
        public string? decimals;
        public string? conversionExit;
        public List<DomainValue>? values;
        public List<DomainValueTranslation>? valuesTranslations;
    }

    private ParsedXML? parsedXML;

    public Domain(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "DOMA";
    }

    public override string? GetDescription()
    {
        this.Parse();
        return this.parsedXML?.description;
    }

    public string? GetConversionExit()
    {
        this.Parse();
        return this.parsedXML?.conversionExit;
    }

    public string? GetDataType()
    {
        this.Parse();
        return this.parsedXML?.datatype;
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
        base.SetDirty();
    }

    public AbstractType ParseType(IRegistry reg)
    {
        return this.ParseDomainType(reg);
    }

    public AbstractType ParseDomainType(IRegistry reg, string? dataElement = null, string? description = null)
    {
        // dont cache the DOMA parsed type, they are cached on DTEL level
        // also note that the type carries the name of the DTEL
        if (this.parsedXML == null)
        {
            this.Parse();
        }
        if (this.parsedXML == null)
        {
            return new UnknownType("Domain " + this.GetName() + " parser error", this.GetName());
        }
        var ddic = new DDIC(reg);
        return ddic.TextToType((this.parsedXML.datatype, this.parsedXML.length, this.parsedXML.decimals, this.GetName(),
            dataElement, this.parsedXML.conversionExit, dataElement, description));
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
                var language = Json.Str(parsed, "header", "originalLanguage");
                var values = new List<DomainValue>();
                foreach (var fixedValue in Json.Array(parsed, "fixedValues"))
                {
                    values.Add(new DomainValue
                    {
                        Low = Json.Str(fixedValue, "fixedValue"),
                        High = "",
                        Description = Json.Str(fixedValue, "description"),
                        Language = language,
                    });
                }
                foreach (var interval in Json.Array(parsed, "fixedValueIntervals"))
                {
                    values.Add(new DomainValue
                    {
                        Low = Json.Str(interval, "lowLimit"),
                        High = Json.Str(interval, "highLimit"),
                        Description = Json.Str(interval, "description"),
                        Language = language,
                    });
                }
                this.parsedXML = new ParsedXML
                {
                    description = Json.Str(parsed, "header", "description"),
                    datatype = Json.Str(parsed, "format", "dataType"),
                    length = Json.Str(parsed, "format", "length"),
                    decimals = Json.Str(parsed, "format", "decimals"),
                    conversionExit = Json.Str(parsed, "outputCharacteristics", "conversionRoutine"),
                    values = values,
                    valuesTranslations = new List<DomainValueTranslation>(),
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

        var asxValues = raw["abapGit"]?["asx:abap"]?["asx:values"];
        var dd01v = asxValues?["DD01V"];
        var dd07v_tab = XmlUtils.XmlToArray(asxValues?["DD07V_TAB"], "DD07V");
        var values2 = new List<DomainValue>();
        foreach (var ddo7v in dd07v_tab)
        {
            values2.Add(new DomainValue
            {
                Description = ddo7v["DDTEXT"]?.Text,
                Low = ddo7v["DOMVALUE_L"]?.Text,
                High = ddo7v["DOMVALUE_H"]?.Text,
                Language = ddo7v["DDLANGUAGE"]?.Text,
            });
        }

        var dd07v_texts = XmlUtils.XmlToArray(asxValues?["DD07_TEXTS"], "item");
        var translationValues = new List<DomainValueTranslation>();
        foreach (var dd07v in dd07v_texts)
        {
            translationValues.Add(new DomainValueTranslation
            {
                Language = dd07v["DDLANGUAGE"]?.Text,
                Description = dd07v["DDTEXT"]?.Text,
            });
        }

        this.parsedXML = new ParsedXML
        {
            description = dd01v?["DDTEXT"]?.Text,
            datatype = dd01v?["DATATYPE"]?.Text,
            length = dd01v?["LENG"]?.Text,
            conversionExit = dd01v?["CONVEXIT"]?.Text,
            decimals = dd01v?["DECIMALS"]?.Text,
            values = values2,
            valuesTranslations = translationValues,
        };
        var end = Environment.TickCount64;
        return new IParseResult { Updated = true, Runtime = end - start };
    }

    public List<DomainValue> GetFixedValues()
    {
        this.Parse();
        return this.parsedXML?.values ?? new List<DomainValue>();
    }

    public List<DomainValueTranslation> GetFixedValuesTranslations()
    {
        this.Parse();
        return this.parsedXML?.valuesTranslations ?? new List<DomainValueTranslation>();
    }

    private IFile? GetAFFFile()
    {
        return this.GetFiles().FirstOrDefault(file => file.GetFilename().ToLowerInvariant().EndsWith(".doma.json", StringComparison.Ordinal));
    }
}
