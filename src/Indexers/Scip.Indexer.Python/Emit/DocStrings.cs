namespace Scip.Indexer.Python.Emit;

/// <summary>Port of pyright's <c>docStringUtils.ts</c> (PEP 257 clean-up and parameter documentation extraction).</summary>
public static class DocStrings
{
    /// <summary>Cleans a docstring the way <c>inspect.cleandoc</c> does.</summary>
    public static string Clean(string raw) => string.Join('\n', CleanAndSplit(raw));

    public static List<string> CleanAndSplit(string raw)
    {
        var unescaped = raw.Replace("\r", "").Replace("\t", "        ");
        var lines = unescaped.Split('\n');
        var leftSpacesToRemove = int.MaxValue;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines.Length <= 1 || i > 0)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.Length > 0)
                {
                    leftSpacesToRemove = Math.Min(leftSpacesToRemove, lines[i].Length - trimmed.Length);
                }
            }
        }

        if (leftSpacesToRemove == int.MaxValue)
        {
            leftSpacesToRemove = 0;
        }

        var result = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            result.Add(i == 0
                ? lines[i].Trim()
                : (leftSpacesToRemove >= lines[i].Length ? "" : lines[i][leftSpacesToRemove..]).TrimEnd());
        }

        while (result.Count > 0 && result[0].Length == 0)
        {
            result.RemoveAt(0);
        }

        while (result.Count > 0 && result[^1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    /// <summary>Epytext, reST and Google style parameter documentation.</summary>
    public static string? ExtractParameterDocumentation(string functionDocString, string paramName)
    {
        if (string.IsNullOrEmpty(functionDocString) || string.IsNullOrEmpty(paramName))
        {
            return null;
        }

        foreach (var line in CleanAndSplit(functionDocString))
        {
            var trimmed = line.Trim();
            var offset = trimmed.IndexOf("@param " + paramName, StringComparison.Ordinal);
            if (offset >= 0)
            {
                return Substr(trimmed, offset + 7);
            }

            offset = trimmed.IndexOf(":param " + paramName, StringComparison.Ordinal);
            if (offset >= 0)
            {
                return Substr(trimmed, offset + 7);
            }

            offset = trimmed.IndexOf(paramName + ": ", StringComparison.Ordinal);
            if (offset >= 0)
            {
                return Substr(trimmed, offset);
            }

            offset = trimmed.IndexOf(paramName + " (", StringComparison.Ordinal);
            if (offset >= 0)
            {
                return Substr(trimmed, offset);
            }
        }

        return null;
    }

    private static string Substr(string s, int from) => from >= s.Length ? "" : s[from..];
}
