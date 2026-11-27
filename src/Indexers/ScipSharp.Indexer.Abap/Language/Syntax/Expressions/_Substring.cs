// Ported from abaplint packages/core/src/abap/5_syntax/expressions/_substring.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

// The type of a substring with a static offset or length, used for reading and writing positions.
// On a character or byte field of fixed length the substring must lie inside the field, else it is
// a syntax error. Other types, a dynamic offset or length and "*" (undefined) are left alone, as is
// length 0. The offset is applied first, so the length is compared to what remains after the offset.
// A substring of a date, time or numeric text field has type n; its bounds are not checked.
public static class Substring
{

    // The type of a substring with a static offset or length, used for reading and writing positions.
    // On a character or byte field of fixed length the substring must lie inside the field, else it is
    // a syntax error. Other types, a dynamic offset or length and "*" (undefined) are left alone, as is
    // length 0. The offset is applied first, so the length is compared to what remains after the offset.
    // A substring of a date, time or numeric text field has type n; its bounds are not checked.
    public static AbstractType? SubstringType(AbstractType? context, int? offset, int? length, INode node, SyntaxInput input)
    {

        if (context is DateType || context is TimeType || context is NumericType)
        {
            return NumericSubstringType(context, offset, length);
        }

        if (!((context is CharacterType)) && !((context is HexType)))
        {
            return context;
        }
        var fieldLength = TypeLength.Of(context);

        if (offset != null && offset.Value > 0)
        {
            if (offset.Value >= fieldLength)
            {
                var message = $"Offset {offset.Value} is outside the field of length {fieldLength}";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            return context is CharacterType ? new CharacterType(fieldLength - offset.Value) : new HexType(fieldLength - offset.Value);
        }

        if (length != null && length.Value > 0)
        {
            if (length.Value > fieldLength)
            {
                var message = $"Length {length.Value} exceeds the remaining length {fieldLength} of the field";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            return context is CharacterType ? new CharacterType(length.Value) : new HexType(length.Value);
        }

        return context;
    }

    internal static AbstractType NumericSubstringType(AbstractType context, int? offset, int? length)
    {

        var hasOffset = offset != null && offset.Value > 0;
        var hasLength = length != null && length.Value > 0;
        if (hasOffset == false && hasLength == false)
        {
            return context;
        }

        int remaining = context is DateType ? 8 : context is TimeType ? 6 : ((NumericType)context).GetLength();
        if (hasOffset)
        {
            remaining = remaining - offset.Value;
        }
        if (hasLength)
        {
            remaining = Math.Min(remaining, length.Value);
        }
        if (remaining <= 0)
        {
            return context;
        }
        return new NumericType(remaining);
    }

    // A field symbol typed "c" or "x" is generic, but its type is represented with length 1, so the
    // offset or length at "index" is not checked when it directly follows the field symbol
    public static bool IsFieldSymbolItself(List<INode> children, int index)
    {
        var first = children.ElementAtOrDefault(0)?.Get();
        if (!((first is global::ScipSharp.Indexer.Abap.Statements.Expressions.SourceFieldSymbol)) && !((first is TargetFieldSymbol)))
        {
            return false;
        }
        for (var i = 1; i < index; i++)
        {
            if (!((children.ElementAtOrDefault(i).Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldOffset)))
            {
                return false;
            }
        }
        return true;
    }
}
