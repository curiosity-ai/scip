// Ported from abaplint packages/core/src/utils/include_graph.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Files;
using ScipSharp.Indexer.Abap.Objects;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Utils;

public class IVertex
{
    public string Filename { get; set; }
    public string? IncludeName { get; set; }
    public bool Include { get; set; }
}

public class Graph
{
    public readonly Dictionary<string, IVertex> verticesIncludenameIndex;
    public readonly Dictionary<string, IVertex> verticesFilenameIndex;
    public readonly Dictionary<string, List<string>> edges;

    public Graph()
    {
        this.verticesIncludenameIndex = new Dictionary<string, IVertex>();
        this.verticesFilenameIndex = new Dictionary<string, IVertex>();
        this.edges = new Dictionary<string, List<string>>();
    }

    public void AddVertex(IVertex vertex)
    {
        if (vertex.IncludeName != null)
        {
            this.verticesIncludenameIndex[JsUtils.ToUpper(vertex.IncludeName)] = vertex;
        }
        this.verticesFilenameIndex[JsUtils.ToUpper(vertex.Filename)] = vertex;
    }

    public IVertex? FindVertexViaIncludename(string includeName)
    {
        return this.verticesIncludenameIndex.GetValueOrDefault(JsUtils.ToUpper(includeName));
    }

    public IVertex? FindVertexByFilename(string filename)
    {
        return this.verticesFilenameIndex.GetValueOrDefault(JsUtils.ToUpper(filename));
    }

    public void AddEdge(IVertex from, string toFilename)
    {
        if (this.edges.GetValueOrDefault(from.Filename) == null)
        {
            this.edges[from.Filename] = new List<string>();
        }
        this.edges.GetValueOrDefault(from.Filename).Add(toFilename);
    }

    // true if "to" is reachable from "from" via the edges, "from" itself counts
    public bool IsReachable(string from, string to)
    {
        var seen = new HashSet<string>();
        List<string> stack = new List<string> { from };
        while (stack.Count > 0)
        {
            var current = JsUtils.Pop(stack)!;
            if (current == to)
            {
                return true;
            }
            if (seen.Contains(current) == false)
            {
                seen.Add(current);
                stack.AddRange(this.edges.GetValueOrDefault(current) ?? new List<string>());
            }
        }
        return false;
    }

    public List<IVertex> FindTop(string filename, HashSet<string>? path = null)
    {
        path ??= new HashSet<string>();
        List<IVertex> ret = new List<IVertex>();
        path.Add(filename);
        foreach (var to in this.edges.GetValueOrDefault(filename) ?? new List<string>())
        {
            // an INCLUDE cycle, the file is already on the current path
            if (path.Contains(to))
            {
                continue;
            }
            ret.AddRange(this.FindTop(to, path));
        }
        path.Remove(filename);
        if (ret.Count == 0)
        {
            var found = this.FindVertexByFilename(filename);
            if (found != null)
            {
                ret.Add(found);
            }
        }
        return ret;
    }
}

public class IncludeGraph : IIncludeGraph
{

    // todo, add configurable error for multiple use includes
    internal static readonly Regex FMXXINCLUDE = new Regex(@"^(\/\w+\/)?L.+XX$", RegexOptions.ECMAScript);

    internal static List<ABAPObject> GetABAPObjects(IRegistry reg)
    {
        List<ABAPObject> ret = new List<ABAPObject>();
        foreach (var o in reg.GetObjects())
        {
            if (o is ABAPObject)
            {
                ret.Add(((ABAPObject)o));
            }
        }
        return ret;
    }
    private readonly IRegistry reg;
    private readonly List<Issue> issues;
    private readonly Graph graph;
    private readonly Severity severity;
    private readonly bool allowUnused;

    public IncludeGraph(IRegistry reg, Severity severity = Severity.Error, bool allowUnused = false)
    {
        this.reg = reg;
        this.issues = new List<Issue>();
        this.graph = new Graph();
        this.severity = severity;
        this.allowUnused = allowUnused;
        this.Build();
    }

    public List<string> ListMainForInclude(string filename)
    {
        List<string> ret = new List<string>();
        if (filename == null)
        {
            return new List<string>();
        }
        foreach (var f in this.graph.FindTop(filename))
        {
            if (f.Include == false)
            {
                ret.Add(f.Filename);
            }
        }
        return ret;
    }

    public List<Issue> GetIssuesFile(IFile file)
    {
        List<Issue> ret = new List<Issue>();
        foreach (var i in this.issues)
        {
            if (i.GetFilename() == file.GetFilename())
            {
                ret.Add(i);
            }
        }

        var v = this.graph.FindVertexByFilename(file.GetFilename());
        if (v != null && this.allowUnused == false && v.Include == true && this.ListMainForInclude(v.Filename).Count == 0)
        {
            var f = this.reg.GetFileByName(v.Filename);
            if (f == null)
            {
                throw new Exception("findUnusedIncludes internal error");
            }
            var issue = Issue.AtPosition(f, new Position(1, 1), "INCLUDE not used anywhere", "check_include", this.severity);
            ret.Add(issue);
        }

        return ret;
    }

    ///////////////////////////////
    private void Build()
    {
        this.AddVertices();
        List<(IFile file, StatementNode statement, IVertex vertex, string name)> included = new List<(IFile file, StatementNode statement, IVertex vertex, string name)>();

        foreach (var o in GetABAPObjects(this.reg))
        {
            foreach (var f in o.GetABAPFiles())
            {
                if (f.GetFilename().Contains(".prog.screen_", StringComparison.Ordinal) || f.GetFilename().Contains(".fugr.screen_", StringComparison.Ordinal))
                {
                    // skip dynpro files
                    continue;
                }

                foreach (var s in f.GetStatements())
                {
                    if (s.Get() is Include)
                    {
                        var iexp = s.FindFirstExpression<IncludeName>();
                        if (iexp == null)
                        {
                            throw new Exception("unexpected Include node");
                        }
                        var name = JsUtils.ToUpper(iexp.GetFirstToken().GetStr());
                        if ((JsUtils.Match(name, FMXXINCLUDE)) != null)
                        {
                            continue;
                        }
                        var found = this.graph.FindVertexViaIncludename(name);
                        if (found == null)
                        {
                            var ifFound = JsUtils.ToUpper(s.ConcatTokens()).Contains("IF FOUND", StringComparison.Ordinal);
                            if (ifFound == false)
                            {
                                var issue = Issue.AtStatement(f, s, "Include " + name + " not found", "check_include", this.severity);
                                this.issues.Add(issue);
                            }
                        }
                        else if (found.Include == false)
                        {
                            var issue = Issue.AtStatement(f, s, "Not possible to INCLUDE a main program, " + name, "check_include", this.severity);
                            this.issues.Add(issue);
                        }
                        else
                        {
                            this.graph.AddEdge(found, f.GetFilename());
                            included.Add((f, s, found, name));
                        }
                    }
                }
            }
        }

        // cycles are not followed, but ABAP rejects them, so report them
        foreach (var i in included)
        {
            if (this.graph.IsReachable(i.file.GetFilename(), i.vertex.Filename))
            {
                var issue = Issue.AtStatement(i.file, i.statement, "Cyclic INCLUDE, " + i.name, "check_include", this.severity);
                this.issues.Add(issue);
            }
        }
    }

    private void AddVertices()
    {
        foreach (var o in GetABAPObjects(this.reg))
        {
            if (o is global::ScipSharp.Indexer.Abap.Objects.Program)
            {
                var file = ((global::ScipSharp.Indexer.Abap.Objects.Program)o).GetMainABAPFile();
                if (file != null)
                {
                    this.graph.AddVertex(new IVertex { Filename = file.GetFilename(), IncludeName = ((global::ScipSharp.Indexer.Abap.Objects.Program)o).GetName(), Include = ((global::ScipSharp.Indexer.Abap.Objects.Program)o).IsInclude() });
                }
            }
            else if (o is global::ScipSharp.Indexer.Abap.Objects.TypePool)
            {
                var file = ((global::ScipSharp.Indexer.Abap.Objects.TypePool)o).GetMainABAPFile();
                if (file != null)
                {
                    this.graph.AddVertex(new IVertex { Filename = file.GetFilename(), IncludeName = null, Include = false });
                }
            }
            else if (o is Class)
            {
                foreach (var f in ((Class)o).GetSequencedFiles())
                {
                    this.graph.AddVertex(new IVertex { Filename = f.GetFilename(), IncludeName = null, Include = false });
                }
            }
            else if (o is FunctionGroup)
            {
                foreach (var i in ((FunctionGroup)o).GetIncludeFiles())
                {
                    this.graph.AddVertex(new IVertex { Filename = i.file.GetFilename(), IncludeName = i.name, Include = true });
                }
                var file = ((FunctionGroup)o).GetMainABAPFile();
                if (file != null)
                {
                    this.graph.AddVertex(new IVertex { Filename = file.GetFilename(), IncludeName = null, Include = false });
                }
            }
        }
    }
}
