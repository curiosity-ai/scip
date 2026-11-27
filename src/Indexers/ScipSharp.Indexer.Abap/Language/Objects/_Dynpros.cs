// Ported from abaplint packages/core/src/objects/_dynpros.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Objects;

public class DynproField
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? ContType { get; set; }
    public int Length { get; set; }
    public int Vislength { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public int Height { get; set; }
}

public class DynproHeader
{
    public string? Number { get; set; }
    public string? Description { get; set; }
    public string? NextScreen { get; set; }
    public string? Type { get; set; }
    public List<DynproField> Fields { get; set; } = new();
}

public static class Dynpros
{
    // NaN (parseInt of a non number) is returned as int.MinValue
    private static int ParseNumber(string? value)
    {
        if (value == null)
        {
            return 0;
        }

        return JsUtils.ParseInt(value, 10);
    }

    public static List<DynproHeader> ParseDynpros(XmlNode parsed)
    {
        var dynpros = new List<DynproHeader>();
        var xmlDynpros = parsed["abapGit"]?["asx:abap"]?["asx:values"]?["DYNPROS"];
        if (xmlDynpros != null)
        {
            foreach (var d in xmlDynpros.All("item"))
            {
                if (d["HEADER"] == null)
                {
                    continue;
                }
                var fields = new List<DynproField>();
                foreach (var f in XmlUtils.XmlToArray(d["FIELDS"], "RPY_DYFATC"))
                {
                    fields.Add(new DynproField
                    {
                        Name = f["NAME"]?.Text,
                        Type = f["TYPE"]?.Text,
                        ContType = f["CONT_TYPE"]?.Text,
                        Length = ParseNumber(f["LENGTH"]?.Text),
                        Vislength = ParseNumber(f["VISLENGTH"]?.Text),
                        Line = ParseNumber(f["LINE"]?.Text),
                        Column = ParseNumber(f["COLUMN"]?.Text),
                        Height = ParseNumber(f["HEIGHT"]?.Text),
                    });
                }
                dynpros.Add(new DynproHeader
                {
                    Number = d["HEADER"]?["SCREEN"]?.Text,
                    Description = d["HEADER"]?["DESCRIPT"]?.Text,
                    NextScreen = d["HEADER"]?["NEXTSCREEN"]?.Text,
                    Type = d["HEADER"]?["TYPE"]?.Text,
                    Fields = fields,
                });
            }
        }
        return dynpros;
    }
}
