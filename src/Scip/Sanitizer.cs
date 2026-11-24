namespace Scip;

/// <summary>
/// Ensures strings are well-formed Unicode so they can be encoded as UTF-8 protobuf strings (port of <c>sanitize.go</c>).
/// Lone UTF-16 surrogates are replaced with U+FFFD. Everything is mutated in place and returned for convenience.
/// </summary>
public static class Sanitizer
{
    public static Document SanitizeDocument(Document document)
    {
        document.Language = SanitizeString(document.Language);
        document.RelativePath = SanitizeString(document.RelativePath);
        SanitizeOccurrences(document.Occurrences);
        SanitizeSymbols(document.Symbols);
        return document;
    }

    public static IList<Occurrence> SanitizeOccurrences(IList<Occurrence> occurrences)
    {
        foreach (var occurrence in occurrences)
        {
            SanitizeOccurrence(occurrence);
        }

        return occurrences;
    }

    public static Occurrence SanitizeOccurrence(Occurrence occurrence)
    {
        occurrence.Symbol = SanitizeString(occurrence.Symbol);
        SanitizeStrings(occurrence.OverrideDocumentation);
        SanitizeDiagnostics(occurrence.Diagnostics);
        return occurrence;
    }

    public static IList<Diagnostic> SanitizeDiagnostics(IList<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            SanitizeDiagnostic(diagnostic);
        }

        return diagnostics;
    }

    public static Diagnostic SanitizeDiagnostic(Diagnostic diagnostic)
    {
        diagnostic.Code = SanitizeString(diagnostic.Code);
        diagnostic.Message = SanitizeString(diagnostic.Message);
        diagnostic.Source = SanitizeString(diagnostic.Source);
        return diagnostic;
    }

    public static IList<SymbolInformation> SanitizeSymbols(IList<SymbolInformation> symbols)
    {
        foreach (var symbol in symbols)
        {
            SanitizeSymbol(symbol);
        }

        return symbols;
    }

    public static SymbolInformation SanitizeSymbol(SymbolInformation symbol)
    {
        symbol.Symbol = SanitizeString(symbol.Symbol);
        SanitizeStrings(symbol.Documentation);
        foreach (var relationship in symbol.Relationships)
        {
            relationship.Symbol = SanitizeString(relationship.Symbol);
        }

        return symbol;
    }

    private static void SanitizeStrings(Google.Protobuf.Collections.RepeatedField<string> strings)
    {
        for (var i = 0; i < strings.Count; i++)
        {
            strings[i] = SanitizeString(strings[i]);
        }
    }

    /// <summary>Replaces each unpaired surrogate with U+FFFD; returns the same instance when already valid.</summary>
    public static string SanitizeString(string s)
    {
        var bad = FindInvalid(s, 0);
        if (bad < 0)
        {
            return s;
        }

        var chars = s.ToCharArray();
        while (bad >= 0)
        {
            chars[bad] = '�';
            bad = FindInvalid(s, bad + 1);
        }

        return new string(chars);
    }

    private static int FindInvalid(string s, int start)
    {
        for (var i = start; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    i++;
                    continue;
                }

                return i;
            }

            if (char.IsLowSurrogate(c))
            {
                return i;
            }
        }

        return -1;
    }
}
