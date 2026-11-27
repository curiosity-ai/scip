// Ported from abaplint packages/core/src/virtual_position.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap;

/// <summary>Used for macro calls: the real position is the macro call, vrow/vcol the position inside the expansion.</summary>
public class VirtualPosition : Position
{
    public readonly int vrow;
    public readonly int vcol;

    public VirtualPosition(Position @virtual, int row, int col) : base(@virtual.GetRow(), @virtual.GetCol())
    {
        vrow = row;
        vcol = col;
    }

    public override bool Equals(Position p)
    {
        if (p is not VirtualPosition casted)
        {
            return false;
        }
        // note: faithful to upstream, which compares this with itself on the base level
        return base.Equals(this) && vrow == casted.vrow && vcol == casted.vcol;
    }

    public override bool IsAfter(Position p)
    {
        if (p is VirtualPosition vp)
        {
            if (GetRow() > vp.GetRow()) return true;
            if (GetRow() == vp.GetRow() && GetCol() > vp.GetCol()) return true;
            if (GetRow() == vp.GetRow() && GetCol() == vp.GetCol() && vrow > vp.vrow) return true;
            if (GetRow() == vp.GetRow() && GetCol() == vp.GetCol() && vrow == vp.vrow && vcol > vp.vcol) return true;
            return false;
        }
        return base.IsAfter(p);
    }
}
