// Ported from abaplint packages/core/src/_iregistry.ts and _imacro_references.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Files;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap;

public class IRunInput
{
    public IProgress? Progress { get; set; }
    public bool? OutputPerformance { get; set; }
}

public interface IRegistry
{
    IRegistry Parse();
    Task<IRegistry> ParseAsync(IRunInput? input = null);

    void Clear();

    // dependency operations
    IRegistry AddDependencies(List<IFile> files);
    IRegistry AddDependency(IFile file);
    void RemoveDependency(IObject obj);
    bool IsDependency(IObject obj);
    bool IsFileDependency(string filename);

    List<Issue> FindIssues(IRunInput? input = null);
    List<Issue> FindIssuesObject(IObject iobj);

    // miscellaneous
    bool InErrorNamespace(string name);
    IDDICReferences GetDDICReferences();
    IMSAGReferences GetMSAGReferences();
    IMacroReferences GetMacroReferences();

    // config operations
    IConfiguration GetConfig();
    IRegistry SetConfig(IConfiguration conf);

    // object operations
    /** Get all objects, including dependencies */
    IEnumerable<IObject> GetObjects();
    /** Get objects by type, including dependencies */
    IEnumerable<IObject> GetObjectsByType(string type);
    /** Get number of objects in the registry */
    (int total, int normal, int dependencies) GetObjectCount();
    IObject? GetFirstObject();
    IObject? GetObject(string? type, string? name);

    // file operations
    IObject? FindObjectForFile(IFile file);
    IRegistry AddFile(IFile file);
    IRegistry UpdateFile(IFile file);
    IRegistry RemoveFile(IFile file);
    IRegistry AddFiles(List<IFile> files);
    IFile? GetFileByName(string filename);
    IEnumerable<IFile> GetFiles();
}

public sealed record IFilenameAndToken(string Filename, AbstractToken Token);

public interface IMacroReferences
{
    void AddDefinition(IFilenameAndToken @ref, Position start, Position end);
    void AddReference(IFilenameAndToken @ref);
    List<AbstractToken> ListDefinitionsByFile(string filename);
    List<IFilenameAndToken> ListUsagesbyMacro(string filename, AbstractToken token);
    (Position Start, Position End)? GetDefinitionRange(string filename, AbstractToken token);
    IFilenameAndToken? FindDefinitionByUsage(string filename, AbstractToken token);
    void Clear(string filename);
}

/// <summary>
/// What ExpandMacros needs from a PROG object to follow INCLUDEs (objects/program.ts), implemented by Program.
/// </summary>
public interface IIncludeProgram : IObject
{
    ABAPFile? GetMainABAPFile();
}
