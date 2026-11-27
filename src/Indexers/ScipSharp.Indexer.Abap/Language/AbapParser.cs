// Ported from abaplint packages/core/src/abap/abap_parser.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Diagnostics;
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Structures;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap;

public sealed record IABAPParserRuntimeExtra(long Lexing, long Statements, long Structure);

public sealed record IABAPParserResult(
    IReadOnlyList<Issue> Issues,
    IReadOnlyList<ABAPFile> Output,
    /* runtime in milliseconds */ long Runtime,
    IABAPParserRuntimeExtra RuntimeExtra);

public sealed class IABAPParserOptions
{
    public ABAPRelease? Release { get; init; }
    public LanguageVersion? LanguageVersion { get; init; }
    public IReadOnlyList<string>? GlobalMacros { get; init; }
    public IRegistry? Reg { get; init; }
}

public sealed class ABAPParser
{
    private readonly ABAPRelease release;
    private readonly LanguageVersion languageVersion;
    private readonly IReadOnlyList<string> globalMacros;
    private readonly IRegistry? reg;

    /// <summary>Deprecated form, use the options constructor.</summary>
    public ABAPParser(ABAPRelease? release, IReadOnlyList<string>? globalMacros = null, IRegistry? reg = null)
    {
        this.release = release ?? Versions.DefaultRelease;
        languageVersion = LanguageVersion.Normal;
        this.globalMacros = globalMacros ?? [];
        this.reg = reg;
    }

    public ABAPParser(IABAPParserOptions? options = null)
    {
        release = options?.Release ?? Versions.DefaultRelease;
        languageVersion = options?.LanguageVersion ?? LanguageVersion.Normal;
        globalMacros = options?.GlobalMacros ?? [];
        reg = options?.Reg;
    }

    /// <summary>Files is input for a single object.</summary>
    public IABAPParserResult Parse(IReadOnlyList<IFile> files)
    {
        var issues = new List<Issue>();
        var output = new List<ABAPFile>();

        var start = Stopwatch.StartNew();

        // 1: lexing
        var b1 = Stopwatch.StartNew();
        var lexerResult = files.Select(f => new Lexer.Lexer().Run(f)).ToList();
        var lexingRuntime = b1.ElapsedMilliseconds;

        // 2: statements
        var b2 = Stopwatch.StartNew();
        var statementResult = new StatementParser(release, reg, languageVersion).Run(lexerResult, globalMacros);
        var statementsRuntime = b2.ElapsedMilliseconds;

        // 3: structures
        var b3 = Stopwatch.StartNew();
        foreach (var f in statementResult)
        {
            var result = StructureParser.Run(f);

            // 4: file information
            var parser = new ABAPFileInformationParser(f.File.GetFilename());
            var parsed = parser.Parse(result.Node);
            var info = new ABAPFileInformation(parsed);

            output.Add(new ABAPFile(f.File, f.Tokens, f.Statements, result.Node, info));
            issues.AddRange(result.Issues);
        }
        var structuresRuntime = b3.ElapsedMilliseconds;

        return new IABAPParserResult(issues, output, start.ElapsedMilliseconds,
            new IABAPParserRuntimeExtra(lexingRuntime, statementsRuntime, structuresRuntime));
    }
}
