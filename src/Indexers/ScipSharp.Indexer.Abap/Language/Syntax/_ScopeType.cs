// Ported from abaplint packages/core/src/abap/5_syntax/_scope_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Syntax;

public static class ScopeType
{
    public const string BuiltIn = "_builtin";
    public const string Dummy = "_dummy";
    // definitions inside this one are local
    public const string SelectionEvent = "selection_event";
    public const string Global = "_global";
    public const string Program = "_program";
    public const string TypePool = "_type_pool";
    public const string FunctionGroup = "_function_group";

    public const string ClassDefinition = "class_definition";
    public const string Interface = "interface";
    public const string ClassImplementation = "class_implementation";
    public const string Form = "form";
    public const string FunctionModule = "function";
    public const string Method = "method";
    public const string MethodInstance = "method_instance";
    public const string MethodDefinition = "method_definition";
    public const string For = "for";
    public const string Let = "let";
    public const string OpenSQL = "open_sql";
}
