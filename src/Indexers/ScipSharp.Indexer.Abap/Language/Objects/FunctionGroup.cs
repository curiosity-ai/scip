// Ported from abaplint packages/core/src/objects/function_group.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Text.RegularExpressions;
using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Objects;

public class FunctionGroup : ABAPObject
{
    private List<string>? includes = null;
    private List<FunctionModuleDefinition>? modules = null;
    private string? description = null;
    private List<DynproHeader>? dynpros = null;

    public FunctionGroup(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "FUGR";
    }

    public override string? GetDescription()
    {
        if (this.description == null)
        {
            this.ParseXML();
        }

        return this.description;
    }

    public override void SetDirty()
    {
        base.SetDirty();
        this.includes = null;
        this.modules = null;
        this.description = null;
        this.dynpros = null;
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 26, AllowNamespace = true };
    }

    public List<DynproHeader> GetDynpros()
    {
        if (this.dynpros == null)
        {
            this.ParseXML();
        }
        return this.dynpros ?? new List<DynproHeader>();
    }

    public override List<ABAPFile> GetSequencedFiles()
    {
        var main = this.GetMainABAPFile();
        if (main == null)
        {
            return new List<ABAPFile>();
        }
        var sequence = new List<ABAPFile> { main };

        foreach (var m in this.GetModules())
        {
            var search = "." + m.GetName().ToLowerInvariant().Replace("/", "#") + ".abap";
            foreach (var f in this.GetABAPFiles())
            {
                if (f.GetFilename().ToLowerInvariant().EndsWith(search, StringComparison.Ordinal))
                {
                    if (sequence.IndexOf(f) < 0)
                    {
                        sequence.Add(f);
                    }
                    break;
                }
            }
        }

        return sequence;
    }

    public List<FunctionModuleDefinition> GetModules()
    {
        if (this.modules == null)
        {
            this.ParseXML();
        }
        if (this.modules == null)
        {
            throw new Exception("getIncludes, undefined");
        }

        return this.modules;
    }

    public List<(ABAPFile file, string name)> GetIncludeFiles()
    {
        var ret = new List<(ABAPFile file, string name)>();
        var includes = this.GetIncludes();
        foreach (var f in this.GetABAPFiles())
        {
            foreach (var i in includes)
            {
                var namespaced = i.StartsWith("/", StringComparison.Ordinal) && i.Contains("/L", StringComparison.Ordinal);
                var search = i;
                if (namespaced)
                {
                    search = search.Replace("/", "#");
                }
                if ((i.StartsWith("L", StringComparison.Ordinal) || namespaced) && f.GetFilename().Contains(search.ToLowerInvariant() + ".", StringComparison.Ordinal))
                {
                    ret.Add((f, i));
                    break;
                }

                // fix for URL encoded? Uris
                if (namespaced)
                {
                    search = i.Replace("/", "%23");
                    if (f.GetFilename().Contains(search.ToLowerInvariant() + ".", StringComparison.Ordinal))
                    {
                        ret.Add((f, i));
                        break;
                    }
                }
            }
        }
        return ret;
    }

    public ABAPFile? GetInclude(string name)
    {
        var upper = JsString.ToUpper(name);
        var includes = this.GetIncludeFiles();
        foreach (var i in includes)
        {
            if (JsString.ToUpper(i.name) == upper)
            {
                return i.file;
            }
        }
        return null;
    }

    private static readonly Regex MainRegex = new Regex(@"\.fugr\.(#\w+#)?sapl", RegexOptions.ECMAScript | RegexOptions.IgnoreCase);

    public override ABAPFile? GetMainABAPFile()
    {
        foreach (var f in this.GetABAPFiles())
        {
            if (MainRegex.IsMatch(f.GetFilename()))
            {
                return f;
            }
        }
        return null;
    }

    public List<string> GetIncludes()
    {
        if (this.includes == null)
        {
            this.ParseXML();
        }
        if (this.includes == null)
        {
            throw new Exception("getIncludes, undefined");
        }

        return this.includes;
    }

    public FunctionModuleDefinition? GetModule(string name)
    {
        foreach (var mod in this.GetModules())
        {
            if (JsString.ToUpper(mod.GetName()) == JsString.ToUpper(name))
            {
                return mod;
            }
        }
        return null;
    }

    public override ITextElements GetTextSymbols()
    {
        if (this.texts == null)
        {
            var found = this.FindTextFile();
            if (found == null)
            {
                return new ITextElements();
            }

            var parsed = XmlUtils.Parse(found.GetRaw());
            if (parsed == null)
            {
                this.texts = new Dictionary<string, ITextElements>();
            }
            else
            {
                this.FindTexts(parsed);
            }
        }

        return this.texts!.GetValueOrDefault("I") ?? new ITextElements();
    }

    /////////////////////////////////

    private void ParseXML()
    {
        this.includes = new List<string>();
        this.modules = new List<FunctionModuleDefinition>();

        var parsed = this.ParseRaw2();
        if (parsed == null)
        {
            return;
        }

        var values = parsed["abapGit"]?["asx:abap"]?["asx:values"];
        this.description = values?["AREAT"]?.Text;
        this.dynpros = Dynpros.ParseDynpros(parsed);

        // INCLUDES
        var includes = values?["INCLUDES"];
        if (includes != null)
        {
            foreach (var i in includes.All("SOBJ_NAME"))
            {
                if (i.Text != null)
                {
                    this.includes.Add(i.Text);
                }
            }
        }

        // FUNCTION MODULES
        var functions = values?["FUNCTIONS"];
        foreach (var module in XmlUtils.XmlToArray(functions, "item"))
        {
            try
            {
                this.modules.Add(new FunctionModuleDefinition(module));
            }
            catch (Exception)
            {
                // Ignore malformed function module metadata.
            }
        }
    }

    private IFile? FindTextFile()
    {
        var name = this.GetName().Replace("/", "#");
        var regex = new Regex(name + "\\.fugr\\.(#\\w+#)?sapl" + JsUtils.RegexReplace(name, @"^#\w+#", "", RegexOptions.ECMAScript) + "\\.xml", RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
        foreach (var f in this.GetFiles())
        {
            if (regex.IsMatch(f.GetFilename()))
            {
                return f;
            }
        }
        return null;
    }
}
