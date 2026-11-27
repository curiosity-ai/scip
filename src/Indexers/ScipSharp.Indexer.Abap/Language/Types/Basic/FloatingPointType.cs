// Ported from abaplint packages/core/src/abap/types/basic/floating_point_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

// this is the DDIC floating point type
public class FloatingPointType : AbstractType
{
    private readonly int length;

    public FloatingPointType(int length, string? qualifiedName = null)
        : base(new AbstractTypeData { QualifiedName = qualifiedName })
    {
        if (length <= 0)
        {
            throw new Exception("Bad LENGTH, Floating Point");
        }
        this.length = length;
    }

    public int GetLength()
    {
        return this.length;
    }

    public override string ToText(int level)
    {
        return "```f LENGTH " + this.GetLength() + "```";
    }

    public override string ToABAP()
    {
        return "f LENGTH " + this.GetLength();
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
        return "abap.fltp";
    }
}
