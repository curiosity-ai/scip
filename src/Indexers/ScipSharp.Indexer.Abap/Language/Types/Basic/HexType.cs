// Ported from abaplint packages/core/src/abap/types/basic/hex_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class HexType : AbstractType
{
    private readonly int length;

    public HexType(int length, string? qualifiedName = null)
        : base(new AbstractTypeData { QualifiedName = qualifiedName })
    {
        if (length <= 0)
        {
            throw new Exception("Bad LENGTH, Hex");
        }
        this.length = length;
    }

    public int GetLength()
    {
        return this.length;
    }

    public override string ToText(int level)
    {
        return "```x LENGTH " + this.GetLength() + "```";
    }

    public override string ToABAP()
    {
        return "x LENGTH " + this.GetLength();
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
        return "abap.raw( " + this.GetLength() + " )";
    }
}
