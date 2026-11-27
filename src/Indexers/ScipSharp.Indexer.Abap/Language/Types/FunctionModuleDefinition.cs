// Ported from abaplint packages/core/src/abap/types/function_module_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types;

public static class FunctionModuleParameterDirection
{
    public const string importing = "importing";
    public const string exporting = "exporting";
    public const string changing = "changing";
    public const string tables = "tables";
}

public class IFunctionModuleParameter
{
    public string Name { get; set; }
    public string Direction { get; set; }
    public string? Type { get; set; }
    public bool Optional { get; set; }
    public string? DefaultValue { get; set; }
    public bool PassByValue { get; set; }
}

public static class FunctionModuleType
{
    public const string regular = "regular";
    public const string remote = "remoteEnabled";
    public const string update = "update";
}

public class FunctionModuleDefinition
{
    private string name;
    private string? description;
    private List<IFunctionModuleParameter> parameters;
    private bool globalParameters;
    private string? moduleType;

    public FunctionModuleDefinition(XmlNode data)
    {
        this.Parse(data);
    }

    public List<IFunctionModuleParameter> GetParameters()
    {
        return this.parameters;
    }

    public string? GetModuleType()
    {
        return this.moduleType;
    }

    public string? GetDescription()
    {
        return this.description;
    }

    public string GetName()
    {
        return this.name;
    }

    public bool IsGlobalParameters()
    {
        return this.globalParameters;
    }

    ///////////////
    private void Parse(XmlNode data)
    {
        if (data["FUNCNAME"] == null)
        {
            throw new Exception("Function module name undefined");
        }
        this.name = data["FUNCNAME"]!.Text!;
        this.description = data["SHORT_TEXT"]?.Text;
        this.parameters = new List<IFunctionModuleParameter>();
        this.globalParameters = data["GLOBAL_FLAG"]?.Text == "X";

        this.moduleType = FunctionModuleType.regular;
        if (data["REMOTE_CALL"]?.Text == "R")
        {
            this.moduleType = FunctionModuleType.remote;
        }
        else if (data["UPDATE_TASK"] != null)
        {
            this.moduleType = FunctionModuleType.update;
        }

        if (data["IMPORT"] != null)
        {
            foreach (var param in XmlUtils.XmlToArray(data["IMPORT"], "RSIMP"))
            {
                if (param["PARAMETER"] == null)
                {
                    throw new Exception("Function module name parameter undefined, importing");
                }
                this.parameters.Add(new IFunctionModuleParameter { Name = param["PARAMETER"]!.Text!, Direction = FunctionModuleParameterDirection.importing, Type = param["TYP"]?.Text ?? param["DBFIELD"]?.Text, Optional = param["OPTIONAL"]?.Text == "X", DefaultValue = param["DEFAULT"]?.Text, PassByValue = param["REFERENCE"]?.Text != "X" });
            }
        }

        if (data["CHANGING"] != null)
        {
            foreach (var param in XmlUtils.XmlToArray(data["CHANGING"], "RSCHA"))
            {
                if (param["PARAMETER"] == null)
                {
                    throw new Exception("Function module name parameter undefined, changing");
                }
                this.parameters.Add(new IFunctionModuleParameter { Name = param["PARAMETER"]!.Text!, Direction = FunctionModuleParameterDirection.changing, Type = param["TYP"]?.Text ?? param["DBFIELD"]?.Text, Optional = param["OPTIONAL"]?.Text == "X", DefaultValue = param["DEFAULT"]?.Text, PassByValue = param["REFERENCE"]?.Text != "X" });
            }
        }

        if (data["EXPORT"] != null)
        {
            foreach (var param in XmlUtils.XmlToArray(data["EXPORT"], "RSEXP"))
            {
                if (param["PARAMETER"] == null)
                {
                    throw new Exception("Function module name parameter undefined, exporting");
                }
                this.parameters.Add(new IFunctionModuleParameter { Name = param["PARAMETER"]!.Text!, Direction = FunctionModuleParameterDirection.exporting, Type = param["TYP"]?.Text ?? param["DBFIELD"]?.Text, Optional = true, DefaultValue = null, PassByValue = param["REFERENCE"]?.Text != "X" });
            }
        }

        if (data["TABLES"] != null)
        {
            foreach (var param in XmlUtils.XmlToArray(data["TABLES"], "RSTBL"))
            {
                if (param["PARAMETER"] == null)
                {
                    throw new Exception("Function module name parameter undefined, tables");
                }
                this.parameters.Add(new IFunctionModuleParameter { Name = param["PARAMETER"]!.Text!, Direction = FunctionModuleParameterDirection.tables, Type = param["DBSTRUCT"]?.Text ?? param["TYP"]?.Text, Optional = param["OPTIONAL"]?.Text == "X", DefaultValue = null, PassByValue = false });
            }
        }
    }
}
