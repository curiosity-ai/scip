using System.Reflection;
using Binder = ScipSharp.Indexer.Vb6.Semantics.Binder;
using ScipSharp.Indexer.Vb6.Indexing;
using ScipSharp.Indexer.Vb6.Project;
using ScipSharp.Indexer.Vb6.Semantics;
using ScipSharp.Indexer.Vb6.Syntax;
using ScipSharp.Indexer.Vb6.Text;

namespace ScipSharp.Indexer.Vb6;

/// <summary>
/// Indexes every VB6 project and loose VBA module below a root directory. Pure managed code: VB6, VBA and COM are never
/// used. Documents are handed out one at a time (<see cref="Run"/>) so a large code base is never held as one index.
/// </summary>
public sealed class Vb6ProjectIndexer(string projectRoot, Vb6Options? options = null, Action<string>? warn = null, IReadOnlyList<string>? arguments = null)
{
    public const string ToolName = "scip-vb6";

    private readonly string _root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar);
    private readonly Vb6Options _options = options ?? new Vb6Options();

    public static string ToolVersion =>
        typeof(Vb6ProjectIndexer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>Builds the whole index in memory (tests, small projects).</summary>
    public Index Build()
    {
        var index = new Index();
        var external = Run(index.Documents.Add, m => index.Metadata = m);
        index.ExternalSymbols.AddRange(external);
        return index;
    }

    /// <summary>Streams documents to <paramref name="onDocument"/>; returns the external symbols.</summary>
    public List<SymbolInformation> Run(Action<Document> onDocument, Action<Metadata>? onMetadata = null)
    {
        var metadata = new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ProjectRoot = new Uri(_root + Path.DirectorySeparatorChar).AbsoluteUri,
            TextDocumentEncoding = TextEncoding.UnspecifiedTextEncoding,
            ToolInfo = new ToolInfo { Name = ToolName, Version = ToolVersion },
        };
        metadata.ToolInfo.Arguments.AddRange(arguments ?? []);
        onMetadata?.Invoke(metadata);

        var ws = Workspace.Load(_root, _options, warn);
        var binder = new Binder(_options, warn);
        binder.Bind(ws);
        var context = new ResolverContext(binder, ws.Projects);

        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new HashSet<Sym>();

        foreach (var project in ws.Projects.Where(p => p.Vbp is not null))
        {
            var builder = ProjectDocument(project, ws.ProjectSources[project]);
            Emit(builder, ws.ProjectSources[project]);
        }

        foreach (var (rel, group, source) in ws.Groups)
        {
            var builder = new DocumentBuilder(rel, "Ini");
            var dir = Path.GetDirectoryName(group.Path)!;
            foreach (var (raw, span) in group.Projects)
            {
                var path = Workspace.ResolvePath(dir, raw);
                var project = ws.Projects.FirstOrDefault(p => p.Vbp is not null && string.Equals(p.Vbp.Path, path, StringComparison.OrdinalIgnoreCase));
                if (project is not null)
                {
                    builder.Reference(project.Sym, span);
                }
            }

            Emit(builder, source);
        }

        foreach (var module in ws.Modules.Where(m => m.Emit))
        {
            var builder = new DocumentBuilder(module.RelativePath!, "VisualBasic");
            try
            {
                new Resolver(context, module, builder).Run();
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException or KeyNotFoundException)
            {
                warn?.Invoke($"{module.RelativePath}: indexing failed: {e.Message}");
            }

            Emit(builder, module.Source);
        }

        // Symbols referenced from documents but defined nowhere in them: type-library classes and their events, and the
        // members of modules that a .vbp pulls in from outside the project root.
        var external = new List<SymbolInformation>();
        foreach (var sym in referenced.Where(s => !emitted.Contains(s.Scip)).OrderBy(s => s.Scip, StringComparer.Ordinal))
        {
            external.Add(DocumentBuilder.ToInfo(sym, includeRelationships: false));
        }

        return external;

        void Emit(DocumentBuilder builder, SourceText source)
        {
            var doc = builder.Build(_options.EmitText ? source.Text : null);
            foreach (var info in doc.Symbols)
            {
                emitted.Add(info.Symbol);
            }

            referenced.UnionWith(builder.Referenced);
            onDocument(doc);
        }
    }

    /// <summary>The .vbp document: the project name defines the project symbol; module lines and Startup reference modules.</summary>
    private static DocumentBuilder ProjectDocument(ProjectInfo project, SourceText source)
    {
        var vbp = project.Vbp!;
        var builder = new DocumentBuilder(project.VbpRelativePath!, "Ini");
        if (vbp.NameSpan is { } nameSpan)
        {
            var lines = source.Lines;
            builder.Definition(project.Sym, nameSpan, new Range(0, 0, Math.Max(0, lines.Count - 1), lines.Count == 0 ? 0 : lines[^1].Length));
        }

        builder.Define(project.Sym);
        var dir = Path.GetDirectoryName(vbp.Path)!;
        foreach (var item in vbp.Items)
        {
            var path = Workspace.ResolvePath(dir, item.RawPath);
            var module = project.Modules.FirstOrDefault(m => string.Equals(m.AbsolutePath, path, StringComparison.OrdinalIgnoreCase));
            if (module is null)
            {
                continue;
            }

            if (item.NameSpan is { } ns)
            {
                builder.Reference(module.Sym, ns);
            }
            else
            {
                builder.Reference(module.Sym, item.PathSpan);
            }
        }

        if (vbp.Startup is { } startup && vbp.StartupSpan is { } startupSpan)
        {
            var name = startup.StartsWith("Sub ", StringComparison.OrdinalIgnoreCase) ? startup[4..].Trim() : startup;
            Sym? target = project.ModulesByName.GetValueOrDefault(name);
            if (target is null || target.Kind == SymKind.StdModule)
            {
                target = project.Modules
                    .Where(m => m.IsStandard)
                    .Select(m => m.Sym.Members.GetValueOrDefault(name))
                    .FirstOrDefault(s => s is { Kind: SymKind.Sub });
            }

            if (target is not null)
            {
                builder.Reference(target, startupSpan);
            }
        }

        foreach (var r in vbp.References.Where(r => r.ProjectPathSpan is not null))
        {
            var path = Workspace.ResolvePath(dir, r.ProjectPath!);
            var referenced = project.References.FirstOrDefault(p => string.Equals(p.Vbp?.Path, path, StringComparison.OrdinalIgnoreCase));
            if (referenced is not null)
            {
                builder.Reference(referenced.Sym, r.ProjectPathSpan!.Value);
            }
        }

        return builder;
    }
}
