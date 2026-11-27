// Ported from abaplint packages/core/src/abap/types/basic/unknown_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class UnknownType : AbstractType
{
    private readonly string error;

    public UnknownType(string error, string? qualifiedName = null)
        : base(new AbstractTypeData { QualifiedName = qualifiedName })
    {
        this.error = error;
    }

    public string GetError()
    {
        return this.error;
    }

    public override string ToText(int level)
    {
        return "Unknown type: " + this.error;
    }

    public override string ToABAP()
    {
        throw new Exception("unknown, generic: " + this.error);
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_UNKNOWN";
    }
}
