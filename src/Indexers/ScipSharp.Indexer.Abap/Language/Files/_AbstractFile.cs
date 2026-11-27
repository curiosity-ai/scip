// Ported from abaplint packages/core/src/files/_abstract_file.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Files;

public abstract class AbstractFile : IFile
{
    private readonly string filename;

    protected AbstractFile(string filename)
    {
        this.filename = filename;
    }

    public string GetFilename() => filename;

    private string BaseName()
    {
        var name = GetFilename();

        // faithful to upstream: "if (index)" is false only for index 0, -1 becomes 0
        var index = name.LastIndexOf('\\');
        if (index != 0)
        {
            index = index + 1;
        }
        name = name.Substring(index);

        index = name.LastIndexOf('/');
        if (index != 0)
        {
            index = index + 1;
        }
        return name.Substring(index);
    }

    public string? GetObjectType()
    {
        var split = BaseName().Split('.');
        return split.Length > 1 ? split[1].ToUpperInvariant() : null;
    }

    public string GetObjectName()
    {
        var split = BaseName().Split('.');
        // handle url escaped namespace
        split[0] = split[0].Replace("%23", "#");
        // handle additional escaping
        split[0] = split[0].Replace("%3e", ">");
        split[0] = split[0].Replace("%3c", "<");
        // handle abapGit namespace
        split[0] = split[0].ToUpperInvariant().Replace('#', '/');
        // handle AFF namespace, first occurrence only
        split[0] = ReplaceFirst(split[0], "(", "/");
        split[0] = ReplaceFirst(split[0], ")", "/");
        return split[0];
    }

    private static string ReplaceFirst(string s, string search, string replace)
    {
        var i = s.IndexOf(search, StringComparison.Ordinal);
        return i < 0 ? s : s.Substring(0, i) + replace + s.Substring(i + search.Length);
    }

    public abstract string GetRaw();
    public abstract string[] GetRawRows();
}
