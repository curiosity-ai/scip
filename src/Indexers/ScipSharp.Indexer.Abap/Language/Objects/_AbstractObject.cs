// Ported from abaplint packages/core/src/objects/_abstract_object.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Files;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Objects;

public abstract class AbstractObject : IObject
{
    protected List<Issue> old;
    protected bool dirty;
    private List<IFile> files;
    private readonly string name;

    public abstract new string GetType();
    public abstract IAllowedNaming GetAllowedNaming();

    public AbstractObject(string name)
    {
        this.name = name;
        this.files = new List<IFile>();
        this.old = new List<Issue>();
        this.dirty = false;
    }

    public virtual string? GetDescription()
    {
        return null;
    }

    public virtual List<Issue> GetParsingIssues()
    {
        return this.old;
    }

    public virtual IParseResult Parse(ABAPRelease? _release = null, IReadOnlyList<string>? _globalMacros = null, IRegistry? _reg = null, LanguageVersion? _languageVersion = null)
    {
        return new IParseResult { Updated = false, Runtime = 0 };
    }

    public string GetName()
    {
        return this.name;
    }

    public virtual void SetDirty()
    {
        this.old = new List<Issue>();
        this.dirty = true;
    }

    public void AddFile(IFile file)
    {
        this.SetDirty();

        // update if it already exists
        for (var i = 0; i < this.files.Count; i++)
        {
            if (this.files[i].GetFilename() == file.GetFilename())
            {
                this.files[i] = file;
                return;
            }
        }

        this.files.Add(file);
    }

    public List<IFile> GetFiles()
    {
        return this.files;
    }

    public bool ContainsFile(string filename)
    {
        foreach (var f in this.files)
        {
            if (f.GetFilename() == filename)
            {
                return true;
            }
        }
        return false;
    }

    public void RemoveFile(IFile file)
    {
        this.SetDirty();
        for (var i = 0; i < this.files.Count; i++)
        {
            if (this.files[i].GetFilename() == file.GetFilename())
            {
                JsUtils.Splice(this.files, i, 1);
                return;
            }
        }
        throw new Exception("removeFile: file not found");
    }

    public bool IsDirty()
    {
        return this.dirty;
    }

    public virtual global::ScipSharp.Indexer.Abap.FileInformation.Identifier? GetIdentifier()
    {
        // this method can be redefined in each object type to give a better result
        var file = this.GetXMLFile();
        if (file == null)
        {
            return null;
        }
        return new global::ScipSharp.Indexer.Abap.FileInformation.Identifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(1, 1), this.GetName()), file.GetFilename());
    }

    public IFile? GetXMLFile()
    {
        // todo, https://github.com/abaplint/abaplint/issues/673 uris
        var expected1 = JsUtils.RegexReplace(this.GetName().ToLowerInvariant(), @"\/", "#", RegexOptions.ECMAScript) + "." + this.GetType().ToLowerInvariant() + ".xml";
        var expected2 = JsUtils.RegexReplace(this.GetName().ToLowerInvariant(), @"\/", "%23", RegexOptions.ECMAScript) + "." + this.GetType().ToLowerInvariant() + ".xml";
        foreach (var file in this.GetFiles())
        {
            if (file.GetFilename().EndsWith(expected1, StringComparison.Ordinal) || file.GetFilename().EndsWith(expected2, StringComparison.Ordinal))
            {
                return file;
            }
        }
        // uri fallback, assume there is only one xml file
        foreach (var file in this.GetFiles())
        {
            if (file.GetFilename().EndsWith(".xml", StringComparison.Ordinal))
            {
                return file;
            }
        }
        return null;
    }

    public string? GetXML()
    {
        var file = this.GetXMLFile();
        if (file != null)
        {
            return file.GetRaw();
        }
        return null;
    }

    public void UpdateFile(IFile file)
    {
        this.SetDirty();
        for (var i = 0; i < this.files.Count; i++)
        {
            if (this.files[i].GetFilename() == file.GetFilename())
            {
                this.files[i] = file;
                return;
            }
        }
        throw new Exception("updateFile: file not found");
    }

    protected XmlNode? ParseRaw2()
    {
        var xml = this.GetXML();
        if (xml == null)
        {
            return null;
        }
        // fast-xml-parser({parseTagValue: false, ignoreAttributes: true, trimValues: false}).parse(xml)
        return XmlUtils.Parse(xml);
    }
}
