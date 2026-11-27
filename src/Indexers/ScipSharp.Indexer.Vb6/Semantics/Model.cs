using ScipSharp.Indexer.Vb6.Project;
using ScipSharp.Indexer.Vb6.Syntax;
using ScipSharp.Indexer.Vb6.Text;

namespace ScipSharp.Indexer.Vb6.Semantics;

/// <summary>One source file (.bas/.cls/.frm/.ctl/...) and its parse.</summary>
internal sealed class ModuleInfo(string absolutePath, string? relativePath, ProjectItemKind kind)
{
    public string AbsolutePath { get; } = absolutePath;

    /// <summary>Path relative to the project root with '/' separators; null when the file lies outside the root (parsed for
    /// resolution only, its symbols become external symbols).</summary>
    public string? RelativePath { get; } = relativePath;

    public ProjectItemKind Kind { get; } = kind;

    /// <summary>Module name given by the .vbp (<c>Module=Name; file</c>), used when the file has no VB_Name.</summary>
    public string? DeclaredName { get; set; }

    public ProjectInfo Owner { get; set; } = null!;

    public SourceText Source { get; set; } = null!;

    public DesignerHeader Header { get; set; } = null!;

    public LexResult Lexed { get; set; } = null!;

    public ModuleSyntax Syntax { get; set; } = null!;

    public Sym Sym { get; set; } = null!;

    /// <summary>Syntax node → symbol for every declaration of the module.</summary>
    public Dictionary<object, Sym> Declared { get; } = new(ReferenceEqualityComparer.Instance);

    public bool Emit => RelativePath is not null;

    public bool IsDesigner => Kind is ProjectItemKind.Form or ProjectItemKind.UserControl or ProjectItemKind.UserDocument
        or ProjectItemKind.PropertyPage or ProjectItemKind.Designer;

    /// <summary>Standard modules export their Public members project-wide; class-like modules do not.</summary>
    public bool IsStandard => Kind == ProjectItemKind.Module;

    public static ProjectItemKind? KindOfExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".bas" => ProjectItemKind.Module,
        ".cls" => ProjectItemKind.Class,
        ".frm" => ProjectItemKind.Form,
        ".ctl" => ProjectItemKind.UserControl,
        ".pag" => ProjectItemKind.PropertyPage,
        ".dob" => ProjectItemKind.UserDocument,
        ".dsr" => ProjectItemKind.Designer,
        _ => null,
    };
}

/// <summary>A VB6 project (.vbp), or a synthetic project for loose module files of one directory.</summary>
internal sealed class ProjectInfo(string name, VbpFile? vbp, string? vbpRelativePath)
{
    public string Name { get; } = name;

    public VbpFile? Vbp { get; } = vbp;

    public string? VbpRelativePath { get; } = vbpRelativePath;

    /// <summary>SCIP package name (the project name, made unique within one index).</summary>
    public string PackageName { get; set; } = name;

    public string Version => Vbp?.Version ?? ".";

    public List<ModuleInfo> Modules { get; } = [];

    public List<ProjectInfo> References { get; } = [];

    public Sym Sym { get; set; } = null!;

    public Dictionary<string, object> Constants { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Modules (by name) of this project.</summary>
    public Dictionary<string, Sym> ModulesByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Project-wide values: Public members of standard modules, members of Public enums.</summary>
    public Dictionary<string, Sym> GlobalValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Project-wide types: classes/forms and Public UDTs/enums.</summary>
    public Dictionary<string, Sym> GlobalTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Types visible to other projects that reference this one (classes with VB_Exposed = True).</summary>
    public Dictionary<string, Sym> ExposedTypes { get; } = new(StringComparer.OrdinalIgnoreCase);
}
