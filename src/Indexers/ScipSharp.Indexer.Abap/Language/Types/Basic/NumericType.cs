// Ported from abaplint packages/core/src/abap/types/basic/numeric_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class NumericType : AbstractType
{
    private readonly int length;

    public NumericType(int length, string? qualifiedName = null)
        : base(new AbstractTypeData { QualifiedName = qualifiedName })
    {
        if (length <= 0)
        {
            throw new Exception("Bad LENGTH, Numeric");
        }
        this.length = length;
    }

    public int GetLength()
    {
        return this.length;
    }

    public override string ToText(int level)
    {
        return "```n LENGTH " + this.GetLength() + "```";
    }

    public override string ToABAP()
    {
        return "n LENGTH " + this.GetLength();
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
        return "abap.numc( " + this.GetLength() + " )";
    }
}
