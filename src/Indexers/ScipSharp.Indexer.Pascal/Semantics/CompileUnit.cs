namespace ScipSharp.Indexer.Pascal.Semantics;

internal enum UnitState
{
    NotStarted,
    InInterface,
    InterfaceDone,
    InImplementation,
    Done,
}

internal enum UnitFileKind
{
    Unit,
    Program,
    Library,
    Module,
}

/// <summary>One unit/program/library/module (a source file), or a built-in unit from the RTL table.</summary>
internal sealed class CompileUnit(string name, UnitFileKind kind, PackageInfo package)
{
    /// <summary>Name as declared (or the file name of a headerless program).</summary>
    public string Name { get; set; } = name;

    public UnitFileKind Kind { get; set; } = kind;

    public PackageInfo Package { get; set; } = package;

    /// <summary>Absolute path, or null for built-in units.</summary>
    public string? Path { get; init; }

    /// <summary>Under the project root: produces a document.</summary>
    public bool InProject { get; init; }

    public bool IsBuiltin => Path is null;

    /// <summary>Exhaustively described built-in unit (unknown names are not attributed to it).</summary>
    public bool HasTable { get; set; }

    public Sym? Sym { get; set; }

    public Scope Interface { get; set; } = new(null);

    public Scope Implementation { get; set; } = new(null);

    public UnitState State { get; set; }

    public override string ToString() => $"{Kind} {Name} ({Path ?? "builtin"})";
}
