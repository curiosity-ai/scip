// Ported from abaplint packages/core/src/registry.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Abap.Syntax.GlobalDefinitions;
using ScipSharp.Indexer.Abap.Files;
using ScipSharp.Indexer.Abap.Objects;
using ScipSharp.Indexer.Abap.Utils;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap;

// todo, this should really be an instance in case there are multiple Registry'ies
public static class ParsingPerformance
{
    private static List<(long runtime, string name, string extra)> results = new();
    private static long lexing;
    private static long statements;
    private static long structure;
    // static as upstream, the C# registries may parse on several threads
    private static readonly object sync = new object();

    public static void Clear()
    {
        lock (sync) ClearPrivate();
    }

    private static void ClearPrivate()
    {
        results = new List<(long runtime, string name, string extra)>();
        lexing = 0;
        statements = 0;
        structure = 0;
    }

    public static void Push(IObject obj, IParseResult result)
    {
        lock (sync) PushPrivate(obj, result);
    }

    private static void PushPrivate(IObject obj, IParseResult result)
    {
        if (result.RuntimeExtra != null)
        {
            lexing += result.RuntimeExtra.Lexing;
            statements += result.RuntimeExtra.Statements;
            structure += result.RuntimeExtra.Structure;
        }
        if (result.Runtime < 100)
        {
            return;
        }

        var extra = "";
        if (result.RuntimeExtra != null)
        {
            extra = $"\t(lexing: {result.RuntimeExtra.Lexing}ms, statements: {result.RuntimeExtra.Statements}ms, structure: {result.RuntimeExtra.Structure}ms)";
        }

        results.Add((result.Runtime, obj.GetType() + " " + obj.GetName(), extra));
    }

    public static void Output()
    {
        var MAX = 10;

        List<(long runtime, string name, string extra)> sorted;
        lock (sync) sorted = results.OrderByDescending(r => r.runtime).ToList();

        for (var i = 0; i < MAX && i < sorted.Count; i++)
        {
            var row = sorted[i];
            Console.Error.Write($"\t{row.runtime}ms\t{row.name} {row.extra}\n");
        }
        Console.Error.Write($"\tTotal lexing:     {lexing}ms\n");
        Console.Error.Write($"\tTotal statements: {statements}ms\n");
        Console.Error.Write($"\tTotal structure:  {structure}ms\n");
    }
}

///////////////////////////////////////////////////////////////////////////////////////////////
public class Registry : IRegistry
{
    private Dictionary<string, Dictionary<string, IObject>> objects = new Dictionary<string, Dictionary<string, IObject>>();
    private Dictionary<string, Dictionary<string, IObject>> objectsByType = new Dictionary<string, Dictionary<string, IObject>>();
    private Dictionary<string, Dictionary<string, bool>> dependencies = new Dictionary<string, Dictionary<string, bool>>();
    private IDDICReferences ddicReferences;
    private IMSAGReferences msagReferences;
    private IMacroReferences macroReferences;
    private Regex errorNamespace;
    private IConfiguration conf;

    public Registry(IConfiguration? conf = null)
    {
        this.SetConfig(conf != null ? conf : Config.GetDefault());
        this.Clear();
    }

    /** clears all objects, keeping the configuration */
    public void Clear()
    {
        this.objects = new Dictionary<string, Dictionary<string, IObject>>();
        this.objectsByType = new Dictionary<string, Dictionary<string, IObject>>();
        this.dependencies = new Dictionary<string, Dictionary<string, bool>>();
        this.ddicReferences = new DDICReferences();
        this.msagReferences = new MSAGReferences();
        this.macroReferences = new MacroReferences();
    }

    public static string ParserVersion()
    {
        // magic, see build script "version.js"
        return "{{ VERSION }}";
    }

    public IDDICReferences GetDDICReferences()
    {
        return this.ddicReferences;
    }

    public IMSAGReferences GetMSAGReferences()
    {
        return this.msagReferences;
    }

    public IMacroReferences GetMacroReferences()
    {
        return this.macroReferences;
    }

    public IEnumerable<IObject> GetObjects()
    {
        foreach (var name in this.objects.Keys.ToList())
        {
            foreach (var type in this.objects.GetValueOrDefault(name).Keys.ToList())
            {
                yield return this.objects.GetValueOrDefault(name).GetValueOrDefault(type);
            }
        }
    }

    public IEnumerable<IObject> GetObjectsByType(string type)
    {
        foreach (var name in (this.objectsByType.GetValueOrDefault(type) ?? new Dictionary<string, IObject>()).Keys.ToList())
        {
            yield return this.objectsByType.GetValueOrDefault(type).GetValueOrDefault(name);
        }
    }

    public IEnumerable<IFile> GetFiles()
    {
        foreach (var obj in this.GetObjects())
        {
            foreach (var file in obj.GetFiles())
            {
                yield return file;
            }
        }
    }

    public IObject? GetFirstObject()
    {
        foreach (var name in this.objects.Keys.ToList())
        {
            foreach (var type in this.objects.GetValueOrDefault(name).Keys.ToList())
            {
                return this.objects.GetValueOrDefault(name).GetValueOrDefault(type);
            }
        }
        return null;
    }

    public (int total, int normal, int dependencies) GetObjectCount()
    {
        (int total, int normal, int dependencies) res = (0, 0, 0);
        foreach (var o in this.GetObjects())
        {
            res.total++;
            if (this.IsDependency(o))
            {
                res.dependencies++;
            }
            else
            {
                res.normal++;
            }
        }
        return res;
    }

    public IFile? GetFileByName(string filename)
    {
        var upper = JsUtils.ToUpper(filename);
        foreach (var o in this.GetObjects())
        {
            foreach (var f in o.GetFiles())
            {
                if (JsUtils.ToUpper(f.GetFilename()) == upper)
                {
                    return f;
                }
            }
        }
        return null;
    }

    public IObject? GetObject(string? type, string? name)
    {
        if (type == null || name == null)
        {
            return null;
        }

        var searchName = JsUtils.ToUpper(name);
        if (this.objects.GetValueOrDefault(searchName) != null)
        {
            return this.objects.GetValueOrDefault(searchName).GetValueOrDefault(type);
        }

        return null;
    }

    public IConfiguration GetConfig()
    {
        return this.conf;
    }

    // assumption: Config is immutable, and can only be changed via this method
    public IRegistry SetConfig(IConfiguration conf)
    {
        foreach (var obj in this.GetObjects())
        {
            obj.SetDirty();
        }
        this.conf = conf;
        this.errorNamespace = new Regex(this.GetConfig().GetSyntaxSetttings().ErrorNamespace, RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
        return this;
    }

    public bool InErrorNamespace(string name)
    {
        return this.errorNamespace.IsMatch(name);
    }

    public IRegistry AddFile(IFile file)
    {
        return this.AddFiles(new List<IFile> { file });
    }

    public IRegistry UpdateFile(IFile file)
    {
        var obj = this.Find(file.GetObjectName(), file.GetObjectType());
        obj.UpdateFile(file);
        return this;
    }

    public IRegistry RemoveFile(IFile file)
    {
        var obj = this.Find(file.GetObjectName(), file.GetObjectType());
        obj.RemoveFile(file);
        if (obj.GetFiles().Count == 0)
        {
            this.ddicReferences.Clear(obj);
            this.msagReferences.Clear(obj);
            this.RemoveObject(obj);
        }
        return this;
    }

    private IRegistry _addFiles(List<IFile> files, bool dependency)
    {
        var globalExclude = (this.conf.GetGlobal().Exclude ?? new List<string>()).Select(pattern => new Regex(pattern, RegexOptions.ECMAScript | RegexOptions.IgnoreCase)).ToList();

        foreach (var f in files)
        {
            var filename = f.GetFilename();
            var isNotAbapgitFile = filename.Split(".").ToList().Count <= 2;
            if (isNotAbapgitFile || ExcludeHelper.IsExcluded(filename, globalExclude))
            {
                continue;
            }
            IObject found = this.FindOrCreate(f.GetObjectName(), f.GetObjectType());

            if (this.conf.GetGlobal().ErrorOnDuplicateFilenames == true)
            {
                this.CheckDuplicateFilename(filename, found.GetFiles());
            }

            if (dependency == false && found != null && this.IsDependency(found))
            {
                this.RemoveDependency(found);
                found = this.FindOrCreate(f.GetObjectName(), f.GetObjectType());
            }

            found.AddFile(f);
        }
        return this;
    }

    private void CheckDuplicateFilename(string filename, List<IFile> files)
    {
        var basename = Registry.FilenameBasename(filename);
        if (basename == "PACKAGE.DEVC.XML")
        {
            return;
        }

        foreach (var existingFile in files)
        {
            if (Registry.FilenameBasename(existingFile.GetFilename()) == basename)
            {
                throw new Exception("Duplicate filename: " + filename + " already exists as " + existingFile.GetFilename());
            }
        }
    }

    private static string FilenameBasename(string filename)
    {
        var normalized = JsUtils.RegexReplace(filename, @"\\", "/", RegexOptions.ECMAScript);
        var last = JsUtils.Pop(normalized.Split("/").ToList());
        return JsUtils.ToUpper((last ?? normalized));
    }

    public IRegistry AddFiles(List<IFile> files)
    {
        this._addFiles(files, false);
        return this;
    }

    public IRegistry AddDependencies(List<IFile> files)
    {
        foreach (var f in files)
        {
            this.AddDependency(f);
        }
        return this;
    }

    public IRegistry AddDependency(IFile file)
    {
        var type = JsUtils.ToUpper(file.GetObjectType());
        if (type == null)
        {
            return this;
        }
        var name = JsUtils.ToUpper(file.GetObjectName());

        if (this.dependencies.GetValueOrDefault(type) == null)
        {
            this.dependencies[type] = new Dictionary<string, bool>();
        }
        this.dependencies.GetValueOrDefault(type)[name] = true;
        this._addFiles(new List<IFile> { file }, true);
        return this;
    }

    public void RemoveDependency(IObject obj)
    {
        this.dependencies.GetValueOrDefault(obj.GetType()).Remove(obj.GetName());
        this.RemoveObject(obj);
    }

    public bool IsDependency(IObject obj)
    {
        return this.dependencies.GetValueOrDefault(obj.GetType())?.GetValueOrDefault(obj.GetName()) == true;
    }

    public bool IsFileDependency(string filename)
    {
        var f = this.GetFileByName(filename);
        if (f == null)
        {
            return false;
        }
        var type = JsUtils.ToUpper(f.GetObjectType());
        if (type == null)
        {
            return false;
        }
        var name = JsUtils.ToUpper(f.GetObjectName());
        return this.dependencies.GetValueOrDefault(type)?.GetValueOrDefault(name) == true;
    }

    // assumption: the file is already in the registry
    public IObject? FindObjectForFile(IFile file)
    {
        var filename = file.GetFilename();
        foreach (var obj in this.GetObjects())
        {
            foreach (var ofile in obj.GetFiles())
            {
                if (ofile.GetFilename() == filename)
                {
                    return obj;
                }
            }
        }
        return null;
    }

    // todo, this will be changed to async sometime
    public List<Issue> FindIssues(IRunInput? input = null)
    {
        if (this.IsDirty() == true)
        {
            this.Parse();
        }
        // TODO: the rules are not ported, RulesRunner is replaced by the parsing issues and the check_syntax rule
        return SyntaxRulesRunner.Run(this, this.GetObjects());
    }

    // todo, this will be changed to async sometime
    public List<Issue> FindIssuesObject(IObject iobj)
    {
        if (this.IsDirty() == true)
        {
            this.Parse();
        }
        return SyntaxRulesRunner.Run(this, new List<IObject> { iobj });
    }

    // todo, this will be changed to async sometime
    public IRegistry Parse()
    {
        if (this.IsDirty() == false)
        {
            return this;
        }

        ParsingPerformance.Clear();

        // not in abaplint: where Parse is, readable from another thread while it runs (see ParseStep)
        ParseTotal = this.GetObjectCount().total;
        ParseIndex = 0;
        foreach (var o in this.GetObjects())
        {
            ParseIndex++;
            ParseStep = "parsing a " + o.GetType();
            ParseSize = o.GetFiles().Sum(f => (long)f.GetRaw().Length);
            this.ParsePrivate(o);
        }
        ParseStep = "macros across includes";
        new CrossIncludeMacros(this).Run();
        ParseStep = "global class and interface definitions";
        new FindGlobalDefinitions(this).Run();
        ParseStep = "parsed";

        return this;
    }

    /// <summary>
    /// Not in abaplint. Where <see cref="Parse"/> is: the step, the position of the object it is on among all of them and
    /// that object's size in characters. Written by Parse, read by whoever watches a long run from another thread.
    /// </summary>
    public volatile string ParseStep = "";
    public volatile int ParseIndex;
    public volatile int ParseTotal;
    public long ParseSize;

    public async Task<IRegistry> ParseAsync(IRunInput? input = null)
    {
        if (this.IsDirty() == false)
        {
            return this;
        }

        ParsingPerformance.Clear();
        input?.Progress?.Set(this.GetObjectCount().total, "Lexing and parsing");

        foreach (var o in this.GetObjects())
        {
            if (input?.Progress != null)
            {
                await input.Progress.Tick("Lexing and parsing(" + this.conf.GetRelease().Name + ") - " + o.GetType() + " " + o.GetName());
            }
            this.ParsePrivate(o);
        }
        if (input?.OutputPerformance == true)
        {
            ParsingPerformance.Output();
        }
        new CrossIncludeMacros(this).Run();
        new FindGlobalDefinitions(this).Run(input?.Progress);

        return this;
    }

    //////////////////////////////////////////
    // todo, refactor, this is a mess, see where-used, a lot of the code should be in this method instead
    private void ParsePrivate(IObject input)
    {
        var config = this.GetConfig();
        var result = input.Parse(config.GetRelease(), config.GetSyntaxSetttings().GlobalMacros, this, config.GetLanguageVersion());
        ParsingPerformance.Push(input, result);
    }

    private bool IsDirty()
    {
        foreach (var o in this.GetObjects())
        {
            var dirty = o.IsDirty();
            if (dirty == true)
            {
                return true;
            }
        }
        return false;
    }

    private IObject FindOrCreate(string name, string? type = null)
    {
        try
        {
            return this.Find(name, type);
        }
        catch
        {
            var newName = JsUtils.ToUpper(name);
            string newType = !string.IsNullOrEmpty(type) ? type : "UNKNOWN";
            var add = ArtifactsObjects.NewObject(newName, newType);

            if (this.objects.GetValueOrDefault(newName) == null)
            {
                this.objects[newName] = new Dictionary<string, IObject>();
            }
            this.objects.GetValueOrDefault(newName)[newType] = add;

            if (this.objectsByType.GetValueOrDefault(newType) == null)
            {
                this.objectsByType[newType] = new Dictionary<string, IObject>();
            }
            this.objectsByType.GetValueOrDefault(newType)[newName] = add;

            return add;
        }
    }

    private void RemoveObject(IObject? remove)
    {
        if (remove == null)
        {
            return;
        }

        if (this.objects.GetValueOrDefault(remove.GetName()).GetValueOrDefault(remove.GetType()) == null)
        {
            throw new Exception("removeObject: object not found");
        }

        if (this.objects.GetValueOrDefault(remove.GetName()).Keys.ToList().Count == 1)
        {
            this.objects.Remove(remove.GetName());
        }
        else
        {
            this.objects.GetValueOrDefault(remove.GetName()).Remove(remove.GetType());
        }

        if (this.objectsByType.GetValueOrDefault(remove.GetType()).Keys.ToList().Count == 1)
        {
            this.objectsByType.Remove(remove.GetType());
        }
        else
        {
            this.objectsByType.GetValueOrDefault(remove.GetType()).Remove(remove.GetName());
        }
    }

    private IObject Find(string name, string? type = null)
    {
        string searchType = !string.IsNullOrEmpty(type) ? type : "UNKNOWN";
        var searchName = JsUtils.ToUpper(name);

        if (this.objects.GetValueOrDefault(searchName) != null && this.objects.GetValueOrDefault(searchName).GetValueOrDefault(searchType) != null)
        {
            return this.objects.GetValueOrDefault(searchName).GetValueOrDefault(searchType);
        }

        throw new Exception("find: object not found, " + JsUtils.Str(type) + " " + name);
    }
}
