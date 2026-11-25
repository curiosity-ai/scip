namespace ScipSharp;

/// <summary>Character classes shared by the symbol parser and formatter (see <c>scip.proto</c>).</summary>
internal static class SymbolSyntax
{
    /// <summary>Matches a character of <c>&lt;simple-identifier&gt;</c>.</summary>
    public static bool IsSimpleIdentifierCharacter(char c) =>
        c is '_' or '+' or '-' or '$' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');

    public static bool IsSimpleIdentifier(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
        {
            if (!IsSimpleIdentifierCharacter(c))
            {
                return false;
            }
        }

        return true;
    }
}
