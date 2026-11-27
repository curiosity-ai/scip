// Ported from abaplint packages/core/src/abap/types/_typed_identifier.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Types;

public static class IdentifierMeta
{
    public const string MethodImporting = "importing";
    public const string MethodExporting = "exporting";
    public const string MethodChanging = "changing";
    public const string MethodReturning = "returning";
    public const string FunctionModuleImporting = "function_module_importing";
    public const string FunctionModuleExporting = "function_module_exporting";
    public const string FunctionModuleChanging = "function_module_changing";
    public const string FunctionModuleTables = "function_module_tables";
    public const string EventParameter = "event_parameter";
    public const string FormParameter = "form_parameter";
    public const string FormParameterStructure = "form_parameter_structure";
    public const string ReadOnly = "read_only";
    public const string Tables = "tables";
    public const string Abstract = "abstract";
    public const string PassByValue = "pass_by_value";
    public const string InlineDefinition = "inline";
    public const string BuiltIn = "built-in";
    public const string DDIC = "ddic";
    public const string Static = "static";
    public const string Enum = "enum";
    public const string SelectionScreenTab = "selection_screen_tab";
}

public class TypedIdentifier : global::ScipSharp.Indexer.Abap.FileInformation.Identifier
{
    private readonly AbstractType type;
    private readonly List<string> meta;
    private readonly object? value;
    public static TypedIdentifier From(global::ScipSharp.Indexer.Abap.FileInformation.Identifier id, object type, List<string>? meta = null)
    {
        return new TypedIdentifier(id.GetToken(), id.GetFilename(), type, meta);
    }

    public TypedIdentifier(AbstractToken token, string filename, object type, List<string>? meta = null, object? value = null)
        : base(token, filename)
    {

        if (type is TypedIdentifier)
        {
            this.type = ((TypedIdentifier)type).GetType();
        }
        else
        {
            this.type = ((AbstractType)type);
        }

        this.value = value;
        this.meta = new List<string>();
        if (meta != null)
        {
            this.meta = meta;
        }
    }

    public string ToText()
    {
        return "Identifier: ```" + this.GetName() + "```";
    }

    public new AbstractType GetType()
    {
        return this.type;
    }

    public List<string> GetMeta()
    {
        return this.meta;
    }

    public object? GetValue()
    {
        return this.value;
    }
}
