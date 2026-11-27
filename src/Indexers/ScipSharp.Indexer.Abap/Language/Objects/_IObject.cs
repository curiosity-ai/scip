// Ported from abaplint packages/core/src/objects/_iobject.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Abap.Files;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Objects;

public class IAllowedNaming
{
    public int MaxLength { get; set; }
    public bool AllowNamespace { get; set; }
    public Regex? CustomRegex { get; set; }
}

public interface IArtifact
{
    new string GetType();
    IAllowedNaming GetAllowedNaming();
}

public class IParseResult
{
    public bool Updated { get; set; }
    public long Runtime { get; set; }
    public IABAPParserRuntimeExtra? RuntimeExtra { get; set; }
}

public interface IObject : IArtifact
{
    /** the main place identifying the object, used for go-to */
    Identifier? GetIdentifier();
    string GetName();
    string? GetDescription();

    void SetDirty();
    bool IsDirty();

    /** returns true if the object was parsed, false if no changes since last parse
     * registry for global cross object macros
     */
    IParseResult Parse(ABAPRelease? release = null, IReadOnlyList<string>? globalMacros = null, IRegistry? reg = null, LanguageVersion? languageVersion = null);
    List<Issue> GetParsingIssues();

    List<IFile> GetFiles();
    void AddFile(IFile file);
    void UpdateFile(IFile file);
    void RemoveFile(IFile file);

    IFile? GetXMLFile();
    string? GetXML();
}
