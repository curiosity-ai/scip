// Ported from abaplint packages/core/src/_config.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
// The rule settings (IConfig.rules) are kept as raw JSON, the rules are not ported.
using System.Text.Json;

namespace ScipSharp.Indexer.Abap;

/// <summary>TS <c>VersionOldOrNew = Version | {release: ReleaseName, language: LanguageVersion}</c>.</summary>
public sealed class VersionOldOrNew
{
    /// <summary>The old string form, "v750", "open-abap", ...; null for the object form.</summary>
    public Version? Version { get; set; }
    /// <summary>The object form: release name, e.g. "v740sp08", "Newest".</summary>
    public string? Release { get; set; }
    public LanguageVersion Language { get; set; } = LanguageVersion.Normal;

    public bool IsString => this.Version != null;

    public static VersionOldOrNew FromVersion(Version v) => new VersionOldOrNew { Version = v };

    public static VersionOldOrNew FromRelease(string release, LanguageVersion language) => new VersionOldOrNew { Release = release, Language = language };
}

public class IGlobalConfig
{
    /// <summary>input files, glob format (string or list)</summary>
    public List<string> Files { get; set; } = new();
    public bool? SkipGeneratedBOPFInterfaces { get; set; }
    /// <summary>Skips generated table maintenances, determined via TOBJ object</summary>
    public bool? SkipGeneratedFunctionGroups { get; set; }
    public bool? SkipGeneratedGatewayClasses { get; set; }
    public bool? SkipGeneratedPersistentClasses { get; set; }
    public bool? SkipGeneratedProxyClasses { get; set; }
    public bool? SkipGeneratedProxyInterfaces { get; set; }
    /// <summary>Clone and parse dependencies specified in .apack-manifest.xml if it is present</summary>
    public bool? UseApackDependencies { get; set; }
    /// <summary>Do not report any issues for includes without main programs</summary>
    public bool? SkipIncludesWithoutMain { get; set; }
    /// <summary>Throw an error if multiple files with the same filename are added to the registry, ignores package.devc.xml</summary>
    public bool? ErrorOnDuplicateFilenames { get; set; }
    /// <summary>list of files to exclude, these files are not added when running syntax check or any other rules, case insensitive regex</summary>
    public List<string>? Exclude { get; set; }
    /// <summary>list of files to not report any issues for, case insensitive regex</summary>
    public List<string>? NoIssues { get; set; }
}

public class IDependency
{
    public string? Url { get; set; }
    public string? Branch { get; set; }
    public string? Folder { get; set; }
    public string Files { get; set; } = "";
}

public class ISyntaxSettings
{
    /// <summary>ABAP language version</summary>
    public VersionOldOrNew? Version { get; set; }
    /// <summary>Report error for objects in this regex namespace. Types not in namespace will be void. Case insensitive</summary>
    public string ErrorNamespace { get; set; } = "";
    /// <summary>List of full named global constants (regex not possible)</summary>
    public List<string>? GlobalConstants { get; set; }
    /// <summary>List of names to void in ambigious statements (regex not possible)</summary>
    public List<string>? AmbigiousVoids { get; set; }
    /// <summary>List of full named global macros (regex not possible)</summary>
    public List<string>? GlobalMacros { get; set; }
    /// <summary>
    /// Not in abaplint: a class whose super class is not in the sources is checked as far as it can be (what it inherits is
    /// unknown) instead of being given up at its first statement. For sources that are a part of a landscape.
    /// </summary>
    public bool TolerateMissingSuperClass { get; set; }
}

public class IRenameSettings
{
    public string? Output { get; set; }
    public List<string>? Skip { get; set; }
    public List<(string type, string oldName, string newName)> Patterns { get; set; } = new();
}

public class IAppSettings
{
    public bool? Observations { get; set; }
}

public class IConfig
{
    public IGlobalConfig Global { get; set; } = new();
    public List<IDependency>? Dependencies { get; set; }
    public ISyntaxSettings Syntax { get; set; } = new();
    public IRenameSettings? Rename { get; set; }
    public IAppSettings? App { get; set; }
    /// <summary>Settings for each rule, raw JSON (the rules are not ported).</summary>
    public Dictionary<string, JsonElement> Rules { get; set; } = new();
}

public interface IConfiguration
{
    /// <summary>The rules are not ported, always empty.</summary>
    List<object> GetEnabledRules();
    IConfig Get();
    IGlobalConfig GetGlobal();
    /// <summary>Deprecated, use GetRelease() instead.</summary>
    Version GetVersion();
    ABAPRelease GetRelease();
    LanguageVersion GetLanguageVersion();
    bool IsOpenABAP();
    ISyntaxSettings GetSyntaxSetttings();
    JsonElement? ReadByRule(string rule);
    JsonElement? ReadByKey(string rule, string key);
}
