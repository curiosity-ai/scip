// C# addition, not a port of an upstream file: TS calls getLength() on unions such as
// CharacterType | NumericType after instanceof checks, every member of the union has the method.
namespace ScipSharp.Indexer.Abap.Types.Basic;

public static class TypeLength
{
    public static int Of(AbstractType type)
    {
        return type switch
        {
            CharacterType c => c.GetLength(),
            NumericType n => n.GetLength(),
            PackedType p => p.GetLength(),
            HexType h => h.GetLength(),
            _ => throw new InvalidOperationException("getLength() on " + type.GetType().Name),
        };
    }
}
