// Runs the statement test cases of abaplint (packages/core/test/abap/statements/*.ts, MIT, see
// THIRD-PARTY-NOTICES.md of the indexer), extracted into Data/statement-tests.json, with the semantics of test/abap/_utils.ts.
using System.Text.Json;
using IStatement = ScipSharp.Indexer.Abap.Statements.Statements.IStatement;
using Unknown = ScipSharp.Indexer.Abap.Statements.Statements.Unknown;
using static ScipSharp.Indexer.Abap.Tests.TestUtils;
using Type = System.Type;

namespace ScipSharp.Indexer.Abap.Tests;

public class StatementSuiteTests
{
    private sealed class Case
    {
        public string abap { get; set; } = "";
        public string? release { get; set; }
        public string? version { get; set; }
        public string? langVer { get; set; }
    }

    private sealed class Group
    {
        public string file { get; set; } = "";
        public string kind { get; set; } = "";
        public string description { get; set; } = "";
        public string? type { get; set; }
        public List<Case> cases { get; set; } = [];
    }

    private static readonly Lazy<List<Group>> groups = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "statement-tests.json");
        return JsonSerializer.Deserialize<List<Group>>(File.ReadAllText(path))!;
    });

    public static IEnumerable<string> Files() =>
        groups.Value.Select(g => g.file).Distinct();

    // releases that have a Version mapping, deduplicated, OpenABAP excluded (test/abap/_utils.ts)
    private static readonly List<ABAPRelease> legacyReleases = Enum.GetValues<Version>()
        .Where(v => v != Version.OpenABAP)
        .Select(Versions.VersionToABAPRelease)
        .DistinctBy(r => r.Ordinal)
        .OrderBy(r => r.Ordinal)
        .ToList();

    private static ABAPRelease GetPreviousRelease(ABAPRelease r)
    {
        var idx = legacyReleases.FindIndex(x => x.Name == r.Name);
        if (idx <= 0) throw new InvalidOperationException("No previous release for: " + r.Name);
        return legacyReleases[idx - 1];
    }

    private static Type StatementType(string name) =>
        typeof(IStatement).Assembly.GetType("ScipSharp.Indexer.Abap.Statements.Statements." + name)
        ?? throw new InvalidOperationException("statement class not found: " + name);

    private static LanguageVersion? Lang(Case c) => c.langVer == null ? null : Enum.Parse<LanguageVersion>(c.langVer);

    /// <summary>The release and language a Registry with Config.getDefault(version, langVer) parses with.</summary>
    private static (ABAPRelease, LanguageVersion) ConfigFor(string? release, string? version, LanguageVersion? langVer)
    {
        if (release != null)
        {
            return (Release.Get(release), langVer ?? LanguageVersion.Normal);
        }
        if (version != null)
        {
            var v = VersionExtensions.ParseVersion(version);
            if (langVer != null)
            {
                return (v == Version.Cloud ? Release.Newest : Versions.VersionToABAPRelease(v), langVer.Value);
            }
            return v == Version.Cloud ? (Release.Newest, LanguageVersion.Cloud) : (Versions.VersionToABAPRelease(v), LanguageVersion.Normal);
        }
        return (Release.Newest, langVer ?? LanguageVersion.Normal);
    }

    /// <summary>_utils.ts run(): first statement has the type and covers all tokens.</summary>
    private static string? Run(string abap, Type type, ABAPRelease release, LanguageVersion lang)
    {
        var statements = GetStatements(abap, release, lang);
        var tokens = GetTokens(abap).Count(t => t is not ScipSharp.Indexer.Abap.Lexer.Tokens.Pragma);
        if (statements.Count == 0) return "no statements";
        if (!type.IsInstanceOfType(statements[0].Get()))
        {
            return $"expected {type.Name}, got {statements[0].Get().GetType().Name}";
        }
        if (statements[0].GetTokens().Count != tokens)
        {
            return $"token count {statements[0].GetTokens().Count} != {tokens}";
        }
        return null;
    }

    /// <summary>_utils.ts runExpectFail(): uses the statement parser directly with the release.</summary>
    private static string? RunExpectFail(string abap, Case c)
    {
        ABAPRelease release = c.release != null ? Release.Get(c.release)
            : c.version != null ? Versions.VersionToABAPRelease(VersionExtensions.ParseVersion(c.version))
            : Versions.DefaultRelease;
        var statements = GetStatements(abap, release, Lang(c) ?? LanguageVersion.Normal);
        return statements[0].Get() is Unknown ? null : "expected Unknown, got " + statements[0].Get().GetType().Name;
    }

    [Test]
    [MethodDataSource(nameof(Files))]
    public async Task Statements(string file)
    {
        var failures = new List<string>();
        var count = 0;
        foreach (var g in groups.Value.Where(g => g.file == file))
        {
            foreach (var c in g.cases)
            {
                count++;
                string? error;
                switch (g.kind)
                {
                    case "type":
                    {
                        var (rel, lang) = ConfigFor(null, null, null);
                        error = Run(c.abap, StatementType(g.type!), rel, lang);
                        break;
                    }
                    case "versionOk":
                    {
                        var (rel, lang) = ConfigFor(c.release, c.version, Lang(c));
                        error = Run(c.abap, StatementType(g.type!), rel, lang);
                        break;
                    }
                    case "version":
                    {
                        var (rel, lang) = ConfigFor(c.release, c.version, Lang(c));
                        error = Run(c.abap, StatementType(g.type!), rel, lang);
                        if (error == null)
                        {
                            // should fail in the previous version
                            if (c.version != null)
                            {
                                var lower = Versions.GetPreviousVersion(VersionExtensions.ParseVersion(c.version));
                                var (lrel, llang) = ConfigFor(null, lower.ToStr(), Lang(c));
                                error = Run(c.abap, typeof(Unknown), lrel, llang);
                            }
                            else
                            {
                                var lower = GetPreviousRelease(Release.Get(c.release!));
                                error = Run(c.abap, typeof(Unknown), lower, Lang(c) ?? LanguageVersion.Normal);
                            }
                            if (error != null) error = "lower version: " + error;
                        }
                        break;
                    }
                    case "versionFail":
                    case "expectFail":
                        error = RunExpectFail(c.abap, c);
                        break;
                    default:
                        throw new InvalidOperationException("unknown kind " + g.kind);
                }
                if (error != null)
                {
                    failures.Add($"[{g.kind}] {g.description}: \"{c.abap}\" ({c.release ?? c.version} {c.langVer}): {error}");
                }
            }
        }
        await Assert.That(count).IsGreaterThan(0);
        await Assert.That(failures).IsEmpty().Because($"{failures.Count} of {count} cases failed:\n" + string.Join("\n", failures.Take(50)));
    }
}
