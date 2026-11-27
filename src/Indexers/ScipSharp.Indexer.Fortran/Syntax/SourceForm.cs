namespace ScipSharp.Indexer.Fortran.Syntax;

/// <summary>Fortran source form.</summary>
internal enum SourceForm
{
    /// <summary>FORTRAN 66/77 card layout: label in columns 1-5, continuation in column 6, statement in 7-72.</summary>
    Fixed,

    /// <summary>Fortran 90+ free form: <c>&amp;</c> continuation, <c>!</c> comments.</summary>
    Free,
}

/// <summary>File-extension and content based source form detection.</summary>
internal static class SourceForms
{
    private static readonly HashSet<string> FixedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".f", ".for", ".ftn", ".f77", ".fpp", ".f66" };

    private static readonly HashSet<string> FreeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".f90", ".f95", ".f03", ".f08", ".f18", ".f23", ".f2k", ".ff",
    };

    private static readonly HashSet<string> IncludeExtensions = new(StringComparer.OrdinalIgnoreCase) { ".inc", ".fi", ".h", ".fh", ".finc" };

    /// <summary>True for files that are compilation units on their own.</summary>
    public static bool IsSourceFile(string path)
    {
        var ext = Path.GetExtension(path);
        return FixedExtensions.Contains(ext) || FreeExtensions.Contains(ext);
    }

    /// <summary>True for typical include-file extensions (indexed only when included).</summary>
    public static bool IsIncludeFile(string path) => IncludeExtensions.Contains(Path.GetExtension(path));

    /// <summary>The source form implied by the file extension (null for unknown extensions).</summary>
    public static SourceForm? FromExtension(string path)
    {
        var ext = Path.GetExtension(path);
        if (FixedExtensions.Contains(ext))
        {
            return SourceForm.Fixed;
        }

        return FreeExtensions.Contains(ext) ? SourceForm.Free : null;
    }

    /// <summary>
    /// Heuristic (like fortls' <c>detect_fixed_format</c>): a "fixed" file is really free form when a statement starts in
    /// columns 1-5 with a letter that is not a fixed-form comment character, or a line ends with a free-form <c>&amp;</c>
    /// continuation followed by a line that does not use column 6.
    /// </summary>
    public static bool LooksLikeFreeForm(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var c = line[0];
            if (c is 'c' or 'C' or '*' or '!' or 'd' or 'D' or '#' or '\t')
            {
                continue;
            }

            for (var i = 0; i < Math.Min(5, line.Length); i++)
            {
                var ch = line[i];
                if (ch == ' ')
                {
                    continue;
                }

                if (ch == '\t' || char.IsDigit(ch) || ch == '!')
                {
                    break;
                }

                if (char.IsLetter(ch))
                {
                    return true;
                }

                break;
            }
        }

        return false;
    }
}
