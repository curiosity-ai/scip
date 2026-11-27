// Ported from abaplint packages/core/src/position.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap;

/// <summary>Row/column position, first position is (1,1).</summary>
public class Position
{
    private readonly int row;
    private readonly int col;

    public Position(int row, int col)
    {
        this.row = row;
        this.col = col;
    }

    public int GetCol() => col;

    public int GetRow() => row;

    public virtual bool IsAfter(Position p) => row > p.row || (row == p.row && col >= p.col);

    public virtual bool Equals(Position p) => row == p.GetRow() && col == p.GetCol();

    public bool IsBefore(Position p) => row < p.row || (row == p.row && col < p.col);

    public bool IsBetween(Position p1, Position p2) => IsAfter(p1) && IsBefore(p2);

    public override string ToString() => $"({row},{col})";
}
