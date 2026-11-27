// Ported from abaplint packages/core/src/abap/5_syntax/_reference.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;

namespace ScipSharp.Indexer.Abap.Syntax;

public static class ReferenceType
{
    /** for classes and interface references */
    public const string ObjectOrientedReference = "Object";
    public const string ObjectOrientedVoidReference = "Object (Void)";
    public const string ObjectOrientedUnknownReference = "Object (Unknown)";

    public const string TableReference = "Table";
    public const string TableVoidReference = "Table (Void)";

    public const string MethodReference = "Method";
    public const string BuiltinMethodReference = "Builtin Method";
    public const string ConstructorReference = "Constructor Reference";
    public const string MethodImplementationReference = "Method Implementation";

    public const string TypeReference = "Type";
    public const string BuiltinTypeReference = "Builtin Type";
    public const string VoidType = "Type (Void)";
    public const string InferredType = "Inferred Type";

    public const string FormReference = "Form";
    //  FormVoidReference = "Form (void)",
    public const string DataReadReference = "Read From";
    public const string DataWriteReference = "Write To";
}

public class IReferenceExtras
{
    public string? OoName { get; set; }
    public string? OoType { get; set; }
    public bool? FoundQualified { get; set; }
}

public class IReference
{
    public Identifier Position { get; set; }
    public Identifier? Resolved { get; set; }
    public string ReferenceType { get; set; }
    public IReferenceExtras? Extra { get; set; }
}
