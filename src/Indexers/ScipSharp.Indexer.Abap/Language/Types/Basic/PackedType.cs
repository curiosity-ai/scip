// Ported from abaplint packages/core/src/abap/types/basic/packed_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class PackedType : AbstractType
{
    private readonly int length;
    private readonly int decimals;

    public PackedType(int length, int decimals, AbstractTypeData? extra = null)
        : base(extra)
    {
        if (length <= 0)
        {
            throw new Exception("Bad LENGTH, Packed");
        }
        else if (decimals < 0)
        {
            throw new Exception("Bad DECIMALS, Packed");
        }
        this.length = length;
        this.decimals = decimals;
    }

    public int GetLength()
    {
        return this.length;
    }

    public int GetDecimals()
    {
        return this.decimals;
    }

    public override string ToText(int level)
    {
        return "```p LENGTH " + this.GetLength() + " DECIMALS " + this.GetDecimals() + "```";
    }

    public override string ToABAP()
    {
        return "p LENGTH " + this.GetLength() + " DECIMALS " + this.GetDecimals();
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
        return "abap.TODO_PACKED";
    }
}
