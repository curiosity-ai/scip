using ScipSharp.Indexer.Vb6.Syntax;

namespace ScipSharp.Indexer.Vb6.Project;

internal enum ProjectItemKind
{
    Module,
    Class,
    Form,
    UserControl,
    PropertyPage,
    UserDocument,
    Designer,
}

/// <summary>A source file listed in a .vbp (<c>Module=Name; path</c>, <c>Form=path</c>, ...).</summary>
/// <param name="Name">The declared module name (Module=/Class= lines only).</param>
/// <param name="NameSpan">Position of <paramref name="Name"/> in the .vbp.</param>
/// <param name="PathSpan">Position of the path in the .vbp.</param>
internal sealed record ProjectItem(ProjectItemKind Kind, string? Name, Span? NameSpan, string RawPath, Span PathSpan);

/// <summary>A <c>Reference=</c> line: a type library (<c>*\G{guid}#ver#lcid#path#description</c>) or a project (<c>*\A path</c>).</summary>
internal sealed record ProjectReference(string Raw, int Line, string? ProjectPath, Span? ProjectPathSpan, string? Description);

/// <summary>Parsed .vbp project file.</summary>
internal sealed class VbpFile
{
    public required string Path { get; init; }

    public string? Name { get; set; }

    public Span? NameSpan { get; set; }

    public string? Type { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Startup { get; set; }

    public Span? StartupSpan { get; set; }

    public string? CondComp { get; set; }

    public int? MajorVer { get; set; }

    public int? MinorVer { get; set; }

    public int? RevisionVer { get; set; }

    public List<ProjectItem> Items { get; } = [];

    public List<ProjectReference> References { get; } = [];

    /// <summary>Raw <c>Object=</c> lines (ActiveX controls).</summary>
    public List<string> Objects { get; } = [];

    public string Version => MajorVer is null && MinorVer is null && RevisionVer is null
        ? "."
        : $"{MajorVer ?? 0}.{MinorVer ?? 0}.{RevisionVer ?? 0}";

    public static VbpFile Parse(string path, IReadOnlyList<string> lines)
    {
        var vbp = new VbpFile { Path = path };
        for (var lineNo = 0; lineNo < lines.Count; lineNo++)
        {
            var line = lines[lineNo];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('['))
            {
                break; // add-in sections such as [MS Transaction Server]
            }

            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var valueStart = eq + 1;
            var value = line[valueStart..];
            switch (key.ToLowerInvariant())
            {
                case "type":
                    vbp.Type = value.Trim();
                    break;
                case "name":
                    vbp.Name = Unquote(value, valueStart, lineNo, out var nameSpan);
                    vbp.NameSpan = nameSpan;
                    break;
                case "title":
                    vbp.Title = Unquote(value, valueStart, lineNo, out _);
                    break;
                case "description":
                    vbp.Description = Unquote(value, valueStart, lineNo, out _);
                    break;
                case "startup":
                    vbp.Startup = Unquote(value, valueStart, lineNo, out var startupSpan);
                    vbp.StartupSpan = startupSpan;
                    break;
                case "condcomp":
                    vbp.CondComp = Unquote(value, valueStart, lineNo, out _);
                    break;
                case "majorver":
                    vbp.MajorVer = int.TryParse(value.Trim(), out var major) ? major : null;
                    break;
                case "minorver":
                    vbp.MinorVer = int.TryParse(value.Trim(), out var minor) ? minor : null;
                    break;
                case "revisionver":
                    vbp.RevisionVer = int.TryParse(value.Trim(), out var rev) ? rev : null;
                    break;
                case "object":
                    vbp.Objects.Add(value.Trim());
                    break;
                case "reference":
                    vbp.References.Add(ParseReference(value, valueStart, lineNo));
                    break;
                case "module":
                    AddNamed(vbp, ProjectItemKind.Module, value, valueStart, lineNo);
                    break;
                case "class":
                    AddNamed(vbp, ProjectItemKind.Class, value, valueStart, lineNo);
                    break;
                case "form":
                    AddPath(vbp, ProjectItemKind.Form, value, valueStart, lineNo);
                    break;
                case "usercontrol":
                    AddPath(vbp, ProjectItemKind.UserControl, value, valueStart, lineNo);
                    break;
                case "propertypage":
                    AddPath(vbp, ProjectItemKind.PropertyPage, value, valueStart, lineNo);
                    break;
                case "userdocument":
                    AddPath(vbp, ProjectItemKind.UserDocument, value, valueStart, lineNo);
                    break;
                case "designer":
                    AddPath(vbp, ProjectItemKind.Designer, value, valueStart, lineNo);
                    break;
            }
        }

        return vbp;
    }

    private static ProjectReference ParseReference(string value, int valueStart, int lineNo)
    {
        var v = value.Trim();
        var offset = valueStart + (value.Length - value.TrimStart().Length);
        if (v.StartsWith(@"*\A", StringComparison.OrdinalIgnoreCase))
        {
            var p = v[3..];
            return new ProjectReference(v, lineNo, p, new Span(lineNo, offset + 3, offset + v.Length), null);
        }

        var parts = v.Split('#');
        return new ProjectReference(v, lineNo, null, null, parts.Length >= 5 ? parts[^1] : null);
    }

    private static void AddNamed(VbpFile vbp, ProjectItemKind kind, string value, int valueStart, int lineNo)
    {
        var semi = value.IndexOf(';', StringComparison.Ordinal);
        if (semi < 0)
        {
            AddPath(vbp, kind, value, valueStart, lineNo);
            return;
        }

        var namePart = value[..semi];
        var name = namePart.Trim();
        var nameCol = valueStart + (namePart.Length - namePart.TrimStart().Length);
        var pathPart = value[(semi + 1)..];
        var path = pathPart.Trim();
        var pathCol = valueStart + semi + 1 + (pathPart.Length - pathPart.TrimStart().Length);
        vbp.Items.Add(new ProjectItem(
            kind,
            name,
            new Span(lineNo, nameCol, nameCol + name.Length),
            path,
            new Span(lineNo, pathCol, pathCol + path.Length)));
    }

    private static void AddPath(VbpFile vbp, ProjectItemKind kind, string value, int valueStart, int lineNo)
    {
        var path = value.Trim();
        var col = valueStart + (value.Length - value.TrimStart().Length);
        vbp.Items.Add(new ProjectItem(kind, null, null, path, new Span(lineNo, col, col + path.Length)));
    }

    private static string Unquote(string value, int valueStart, int lineNo, out Span span)
    {
        var lead = value.Length - value.TrimStart().Length;
        var v = value.Trim();
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"')
        {
            span = new Span(lineNo, valueStart + lead + 1, valueStart + lead + v.Length - 1);
            return v[1..^1];
        }

        span = new Span(lineNo, valueStart + lead, valueStart + lead + v.Length);
        return v;
    }
}

/// <summary>A .vbg project group: <c>StartupProject=a.vbp</c> and <c>Project=b.vbp</c> lines.</summary>
internal sealed record VbgFile(string Path, List<(string RawPath, Span PathSpan)> Projects)
{
    public static VbgFile Parse(string path, IReadOnlyList<string> lines)
    {
        var projects = new List<(string, Span)>();
        for (var lineNo = 0; lineNo < lines.Count; lineNo++)
        {
            var line = lines[lineNo];
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            if (key.Equals("Project", StringComparison.OrdinalIgnoreCase) || key.Equals("StartupProject", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[(eq + 1)..];
                var p = value.Trim();
                var col = eq + 1 + (value.Length - value.TrimStart().Length);
                projects.Add((p, new Span(lineNo, col, col + p.Length)));
            }
        }

        return new VbgFile(path, projects);
    }
}
