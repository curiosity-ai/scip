using ScipSharp.Indexer.Fortran.Syntax;

namespace ScipSharp.Indexer.Fortran;

/// <summary>Options of the Fortran indexer (see <see cref="FortranIndexer"/> for the command line syntax).</summary>
public sealed record FortranOptions
{
    /// <summary>Force a source form for every file (null: decide by extension and content).</summary>
    public string? Form { get; init; }

    /// <summary>Fixed-form statement field end column (72 by default; 0 for unlimited).</summary>
    public int FixedLineLength { get; init; } = 72;

    /// <summary>Extra directories searched for INCLUDE / #include files.</summary>
    public IReadOnlyList<string> IncludeDirs { get; init; } = [];

    /// <summary>Preprocessor macros (<c>-D</c>). Setting any switches #if evaluation on.</summary>
    public IReadOnlyDictionary<string, string> Defines { get; init; } = new Dictionary<string, string>();

    /// <summary>Evaluate #if/#ifdef (true) or index every branch (false, the default unless defines are given).</summary>
    public bool? EvaluateConditionals { get; init; }

    public string? PackageName { get; init; }

    public string? PackageVersion { get; init; }

    public string? PackageManager { get; init; }

    /// <summary>Directory names or relative paths that are not indexed (in addition to hidden directories and fpm's <c>build</c>).</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    internal SourceForm? ForcedForm => Form?.ToLowerInvariant() switch
    {
        "fixed" => SourceForm.Fixed,
        "free" => SourceForm.Free,
        _ => null,
    };

    internal bool Evaluate => EvaluateConditionals ?? Defines.Count > 0;
}
