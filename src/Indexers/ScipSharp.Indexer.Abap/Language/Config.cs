// Ported from abaplint packages/core/src/config.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
// The rules (artifacts_rules.ts) are not ported: GetDefault() has no rule settings and GetEnabledRules() is empty.
// The JSON5 reader upstream is replaced by System.Text.Json with comments and trailing commas allowed.
using System.Text.Json;

namespace ScipSharp.Indexer.Abap;

// assumption: this class is immutable
public class Config : IConfiguration
{
    private readonly IConfig config;

    public static Config GetDefault(VersionOldOrNew? ver = null, LanguageVersion? langVer = null)
    {
        VersionOldOrNew version;
        if (langVer != null)
        {
            string release;
            if (ver == null || ver.Version == ScipSharp.Indexer.Abap.Version.Cloud)
            {
                release = ScipSharp.Indexer.Abap.Release.Newest.Name;
            }
            else if (ver.IsString)
            {
                release = Versions.VersionToABAPRelease(ver.Version!.Value).Name;
            }
            else
            {
                release = ver.Release!;
            }
            version = VersionOldOrNew.FromRelease(release, langVer.Value);
        }
        else
        {
            version = ver ?? VersionOldOrNew.FromRelease(ScipSharp.Indexer.Abap.Release.Newest.Name, LanguageVersion.Normal);
        }

        // defaults: dont skip anything, report everything. The user can decide to skip stuff
        // its difficult to debug errors not being reported
        var config = new IConfig
        {
            Global = new IGlobalConfig
            {
                Files = new List<string> { "/src/**/*.*" },
                Exclude = new List<string>(),
                NoIssues = new List<string>(),
                SkipGeneratedBOPFInterfaces = false,
                SkipGeneratedFunctionGroups = false,
                SkipGeneratedGatewayClasses = false,
                SkipGeneratedPersistentClasses = false,
                SkipGeneratedProxyClasses = false,
                SkipGeneratedProxyInterfaces = false,
                UseApackDependencies = false,
                SkipIncludesWithoutMain = false,
                ErrorOnDuplicateFilenames = false,
            },
            Dependencies = new List<IDependency>
            {
                new IDependency { Url = "https://github.com/abaplint/deps", Folder = "/deps", Files = "/src/**/*.*" },
            },
            Syntax = new ISyntaxSettings
            {
                Version = version,
                ErrorNamespace = "^(Z|Y|LCL_|TY_|LIF_)",
                GlobalConstants = new List<string>(),
                AmbigiousVoids = new List<string>(),
                GlobalMacros = new List<string>(),
            },
            Rules = new Dictionary<string, JsonElement>(),
        };

        return new Config(config);
    }

    public List<object> GetEnabledRules()
    {
        // TODO: rules are not ported
        return new List<object>();
    }

    public Config(string json)
        : this(Parse(json))
    {
    }

    /// <summary>C# addition: build from an IConfig (TS: new Config(JSON.stringify(config))), the object is copied.</summary>
    public Config(IConfig input)
    {
        this.config = Clone(input);

        if (this.config.Global == null)
        {
            this.config.Global = Config.GetDefault().GetGlobal();
        }
        if (this.config.Syntax == null)
        {
            this.config.Syntax = Config.GetDefault().GetSyntaxSetttings();
        }
        if (this.config.Syntax.GlobalMacros == null)
        {
            this.config.Syntax.GlobalMacros = new List<string>();
        }
        if (this.config.Syntax.GlobalConstants == null)
        {
            this.config.Syntax.GlobalConstants = new List<string>();
        }
        else
        {
            // remove duplicates,
            this.config.Syntax.GlobalConstants = this.config.Syntax.GlobalConstants.Distinct().ToList();
        }
        if (this.config.Syntax.AmbigiousVoids == null)
        {
            this.config.Syntax.AmbigiousVoids = new List<string>();
        }
        else
        {
            this.config.Syntax.AmbigiousVoids = this.config.Syntax.AmbigiousVoids.Distinct().ToList();
        }
        if (this.config.Global.SkipIncludesWithoutMain == null)
        {
            this.config.Global.SkipIncludesWithoutMain = false;
        }
        if (this.config.Global.ErrorOnDuplicateFilenames == null)
        {
            this.config.Global.ErrorOnDuplicateFilenames = false;
        }
        this.CheckVersion();
    }

    public IConfig Get()
    {
        return this.config;
    }

    public JsonElement? ReadByKey(string rule, string key)
    {
        if (this.config.Rules.TryGetValue(rule, out var r) && r.ValueKind == JsonValueKind.Object && r.TryGetProperty(key, out var v))
        {
            return v;
        }
        return null;
    }

    public JsonElement? ReadByRule(string rule)
    {
        return this.config.Rules.TryGetValue(rule, out var r) ? r : null;
    }

    public IGlobalConfig GetGlobal()
    {
        return this.config.Global;
    }

    public ISyntaxSettings GetSyntaxSetttings()
    {
        return this.config.Syntax;
    }

    public ABAPRelease GetRelease()
    {
        var v = this.config.Syntax.Version;
        if (v != null && !v.IsString)
        {
            return ScipSharp.Indexer.Abap.Release.Get(v.Release!);
        }
        if (this.config.Global == null || v == null)
        {
            return Versions.DefaultRelease;
        }
        return Versions.VersionToABAPRelease(v.Version!.Value);
    }

    public bool IsOpenABAP()
    {
        return this.GetRelease() == ScipSharp.Indexer.Abap.Release.OpenABAP;
    }

    public Version GetVersion()
    {
        var v = this.config.Syntax.Version;
        if (this.config.Global == null || v == null || !v.IsString)
        {
            return ScipSharp.Indexer.Abap.Version.v816;
        }
        return v.Version!.Value;
    }

    public LanguageVersion GetLanguageVersion()
    {
        var v = this.config.Syntax.Version;
        if (v != null && !v.IsString)
        {
            return v.Language;
        }
        return LanguageVersion.Normal;
    }

    private void CheckVersion()
    {
        var version = this.config.Syntax.Version;
        if (version == null || !version.IsString)
        {
            return; // undefined handled in getVersion, object form is already explicit
        }
        if (version.Version == ScipSharp.Indexer.Abap.Version.Cloud)
        {
            this.config.Syntax.Version = VersionOldOrNew.FromRelease(ScipSharp.Indexer.Abap.Release.Newest.Name, LanguageVersion.Cloud);
        }
        // OpenABAP keeps its own version identity; open-abap-ness is derived from the
        // release in getOpenABAP() rather than a separate stored flag.
    }

    //////////////////////////////////////////////
    // JSON reading, replaces JSON5.parse

    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    private static IConfig Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, JsonOptions);
        var root = doc.RootElement;
        var ret = new IConfig { Global = null!, Syntax = null! };

        if (root.TryGetProperty("global", out var g) && g.ValueKind == JsonValueKind.Object)
        {
            ret.Global = new IGlobalConfig
            {
                Files = StringOrList(g, "files") ?? new List<string>(),
                SkipGeneratedBOPFInterfaces = Bool(g, "skipGeneratedBOPFInterfaces"),
                SkipGeneratedFunctionGroups = Bool(g, "skipGeneratedFunctionGroups"),
                SkipGeneratedGatewayClasses = Bool(g, "skipGeneratedGatewayClasses"),
                SkipGeneratedPersistentClasses = Bool(g, "skipGeneratedPersistentClasses"),
                SkipGeneratedProxyClasses = Bool(g, "skipGeneratedProxyClasses"),
                SkipGeneratedProxyInterfaces = Bool(g, "skipGeneratedProxyInterfaces"),
                UseApackDependencies = Bool(g, "useApackDependencies"),
                SkipIncludesWithoutMain = Bool(g, "skipIncludesWithoutMain"),
                ErrorOnDuplicateFilenames = Bool(g, "errorOnDuplicateFilenames"),
                Exclude = StringOrList(g, "exclude"),
                NoIssues = StringOrList(g, "noIssues"),
            };
        }

        if (root.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
        {
            ret.Dependencies = deps.EnumerateArray().Select(d => new IDependency
            {
                Url = Json.Str(d, "url"),
                Branch = Json.Str(d, "branch"),
                Folder = Json.Str(d, "folder"),
                Files = Json.Str(d, "files") ?? "",
            }).ToList();
        }

        if (root.TryGetProperty("syntax", out var s) && s.ValueKind == JsonValueKind.Object)
        {
            ret.Syntax = new ISyntaxSettings
            {
                ErrorNamespace = Json.Str(s, "errorNamespace") ?? "",
                GlobalConstants = StringOrList(s, "globalConstants"),
                AmbigiousVoids = StringOrList(s, "ambigiousVoids"),
                GlobalMacros = StringOrList(s, "globalMacros"),
            };
            if (s.TryGetProperty("version", out var v))
            {
                if (v.ValueKind == JsonValueKind.String)
                {
                    var str = v.GetString()!;
                    // checkVersion(): an unknown version string is removed
                    ret.Syntax.Version = TryParseVersion(str, out var parsed) ? VersionOldOrNew.FromVersion(parsed) : null;
                }
                else if (v.ValueKind == JsonValueKind.Object)
                {
                    var language = Json.Str(v, "language") switch
                    {
                        "Cloud" => LanguageVersion.Cloud,
                        "KeyUser" => LanguageVersion.KeyUser,
                        _ => LanguageVersion.Normal,
                    };
                    ret.Syntax.Version = VersionOldOrNew.FromRelease(Json.Str(v, "release") ?? ScipSharp.Indexer.Abap.Release.Newest.Name, language);
                }
            }
        }

        if (root.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Object)
        {
            foreach (var r in rules.EnumerateObject())
            {
                ret.Rules[r.Name] = r.Value.Clone();
            }
        }

        return ret;
    }

    private static bool TryParseVersion(string s, out Version version)
    {
        foreach (var v in Enum.GetValues<Version>())
        {
            if (v.ToStr() == s)
            {
                version = v;
                return true;
            }
        }
        version = default;
        return false;
    }

    private static bool? Bool(JsonElement e, string name)
    {
        if (e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
        {
            return v.GetBoolean();
        }
        return null;
    }

    private static List<string>? StringOrList(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
        {
            return null;
        }
        if (v.ValueKind == JsonValueKind.String)
        {
            return new List<string> { v.GetString()! };
        }
        if (v.ValueKind == JsonValueKind.Array)
        {
            return v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList();
        }
        return null;
    }

    private static IConfig Clone(IConfig c)
    {
        return new IConfig
        {
            Global = c.Global == null ? null! : new IGlobalConfig
            {
                Files = c.Global.Files?.ToList() ?? new List<string>(),
                SkipGeneratedBOPFInterfaces = c.Global.SkipGeneratedBOPFInterfaces,
                SkipGeneratedFunctionGroups = c.Global.SkipGeneratedFunctionGroups,
                SkipGeneratedGatewayClasses = c.Global.SkipGeneratedGatewayClasses,
                SkipGeneratedPersistentClasses = c.Global.SkipGeneratedPersistentClasses,
                SkipGeneratedProxyClasses = c.Global.SkipGeneratedProxyClasses,
                SkipGeneratedProxyInterfaces = c.Global.SkipGeneratedProxyInterfaces,
                UseApackDependencies = c.Global.UseApackDependencies,
                SkipIncludesWithoutMain = c.Global.SkipIncludesWithoutMain,
                ErrorOnDuplicateFilenames = c.Global.ErrorOnDuplicateFilenames,
                Exclude = c.Global.Exclude?.ToList(),
                NoIssues = c.Global.NoIssues?.ToList(),
            },
            Dependencies = c.Dependencies?.ToList(),
            Syntax = c.Syntax == null ? null! : new ISyntaxSettings
            {
                Version = c.Syntax.Version == null ? null : new VersionOldOrNew { Version = c.Syntax.Version.Version, Release = c.Syntax.Version.Release, Language = c.Syntax.Version.Language },
                ErrorNamespace = c.Syntax.ErrorNamespace,
                TolerateMissingSuperClass = c.Syntax.TolerateMissingSuperClass,
                GlobalConstants = c.Syntax.GlobalConstants?.ToList(),
                AmbigiousVoids = c.Syntax.AmbigiousVoids?.ToList(),
                GlobalMacros = c.Syntax.GlobalMacros?.ToList(),
            },
            Rename = c.Rename,
            App = c.App,
            Rules = new Dictionary<string, JsonElement>(c.Rules ?? new Dictionary<string, JsonElement>()),
        };
    }
}
