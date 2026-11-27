// Ported from abaplint packages/core/src/objects/_abap_object.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Abap.Syntax;
using ScipSharp.Indexer.Abap.Files;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Objects;

/// <summary>TS <c>{entry: string, maxLength: number}</c>; maxLength is NaN (null) when LENGTH is missing.</summary>
public sealed record ITextElement(string Entry, int? MaxLength);

/// <summary>TS <c>interface ITextElements {[key: string]: {entry: string, maxLength: number}}</c>.</summary>
public class ITextElements : Dictionary<string, ITextElement>
{
}

public class ITranslationTextElements
{
    public string? Language { get; set; }
    public ITextElements TextElements { get; set; }
}

public abstract class ABAPObject : AbstractObject
{
    private List<ABAPFile> parsed;
    protected Dictionary<string, ITextElements>? texts;
    private List<ITranslationTextElements>? textsTranslations;
    private XmlNode? rawXMLCache;
    private bool rawXMLCacheSet;
    private bool parsing = false;
    public ISyntaxResult? syntaxResult; // do not use this outside of SyntaxLogic class, todo: refactor

    public abstract List<ABAPFile> GetSequencedFiles();
    public abstract override string? GetDescription();

    public ABAPObject(string name)
        : base(name)
    {
        this.parsed = new List<ABAPFile>();
        this.texts = null;
        this.rawXMLCache = null;
    }

    public static bool Is(object? x)
    {
        return x != null && x is ABAPObject;
    }

    public override IParseResult Parse(ABAPRelease? release = null, IReadOnlyList<string>? globalMacros = null, IRegistry? reg = null, LanguageVersion? languageVersion = null)
    {
        // an INCLUDE that leads back to this object while it is being parsed is not followed again
        if (this.IsDirty() == false || this.parsing == true)
        {
            return new IParseResult { Updated = false, Runtime = 0 };
        }

        var abapFiles = this.GetFiles().Where(f => f.GetFilename().EndsWith(".abap", StringComparison.Ordinal)).ToList();
        IABAPParserResult result;
        this.parsing = true;
        try
        {
            result = new ABAPParser(new IABAPParserOptions { Release = release, GlobalMacros = globalMacros, Reg = reg, LanguageVersion = languageVersion ?? LanguageVersion.Normal }).Parse(abapFiles);
        }
        finally
        {
            this.parsing = false;
        }

        this.parsed = result.Output.ToList();
        this.old = result.Issues.ToList();
        this.dirty = false;

        return new IParseResult { Updated = true, Runtime = result.Runtime, RuntimeExtra = result.RuntimeExtra };
    }

    public override void SetDirty()
    {
        this.syntaxResult = null;
        this.texts = null;
        this.textsTranslations = null;
        this.rawXMLCache = null;
        this.rawXMLCacheSet = false;
        this.parsed = new List<ABAPFile>();
        base.SetDirty();
    }

    private XmlNode? GetParsedXML()
    {
        if (this.rawXMLCacheSet == false)
        {
            this.rawXMLCache = this.ParseRaw2();
            this.rawXMLCacheSet = true;
        }
        return this.rawXMLCache;
    }

    public List<ABAPFile> GetABAPFiles()
    {
        return this.parsed;
    }

    public ABAPFile? GetABAPFileByName(string filename)
    {
        foreach (var p in this.parsed)
        {
            if (p.GetFilename() == filename)
            {
                return p;
            }
        }
        return null;
    }

    public virtual ABAPFile? GetMainABAPFile()
    {
        // todo, uris, https://github.com/abaplint/abaplint/issues/673
        var search = this.GetName().Replace("/", "#").ToLowerInvariant() + "." + this.GetType().ToLowerInvariant() + ".abap";
        var encodedSearch = search.Replace("#", "%23");
        foreach (var file in this.GetABAPFiles())
        {
            if (file.GetFilename().EndsWith(search, StringComparison.Ordinal) || file.GetFilename().EndsWith(encodedSearch, StringComparison.Ordinal))
            {
                return file;
            }
        }
        // uri fallback,
        foreach (var file in this.GetABAPFiles())
        {
            if (file.GetFilename().Contains(".prog.screen_", StringComparison.Ordinal))
            {
                continue;
            }
            else if (file.GetFilename().EndsWith(".abap", StringComparison.Ordinal))
            {
                return file;
            }
        }
        return null;
    }

    public virtual ITextElements GetTextSymbols()
    {
        if (this.texts == null)
        {
            this.FindTexts(this.GetParsedXML());
        }
        return this.texts!.GetValueOrDefault("I") ?? new ITextElements();
    }

    public ITextElements GetTextElements()
    {
        if (this.texts == null)
        {
            this.FindTexts(this.GetParsedXML());
        }
        var result = new ITextElements();
        foreach (var elements in this.texts!.Values)
        {
            foreach (var e in elements)
            {
                result[e.Key] = e.Value;
            }
        }
        return result;
    }

    public List<ITranslationTextElements> GetTextElementsTranslations()
    {
        if (this.textsTranslations == null)
        {
            this.FindTextsTranslations(this.GetParsedXML());
        }
        return this.textsTranslations!;
    }

    protected static XmlNode? Values(XmlNode? parsed)
    {
        return parsed?["abapGit"]?["asx:abap"]?["asx:values"];
    }

    protected void FindTexts(XmlNode? parsed)
    {
        this.texts = new Dictionary<string, ITextElements>();

        if (Values(parsed)?["TPOOL"]?["item"] == null)
        {
            return;
        }

        foreach (var t in XmlUtils.XmlToArray(Values(parsed)?["TPOOL"], "item"))
        {
            var id = t["ID"]?.Text != null ? JsString.ToUpper(t["ID"]!.Text!) : null;
            if (id == null)
            {
                continue;
            }

            if (id != "R" && t["KEY"] == null)
            {
                continue;
            }

            var rawKey = t["KEY"] ?? t["ID"];
            var key = rawKey?.Text != null ? JsString.ToUpper(rawKey.Text) : null;
            if (key == null)
            {
                continue;
            }
            if (!this.texts.ContainsKey(id))
            {
                this.texts[id] = new ITextElements();
            }
            var entry = t["ENTRY"]?.Text;
            this.texts[id][key] = new ITextElement(!string.IsNullOrEmpty(entry) ? XmlUtils.Unescape(entry) : "", JsUtils.ParseIntOrNaN(t["LENGTH"]?.Text, 10));
        }
    }

    private void FindTextsTranslations(XmlNode? parsed)
    {
        this.textsTranslations = new List<ITranslationTextElements>();

        var values = Values(parsed)?["I18N_TPOOL"];
        if (values?["item"] == null)
        {
            return;
        }

        foreach (var langItem in XmlUtils.XmlToArray(values, "item"))
        {
            var textElements = new ITextElements();
            foreach (var item in XmlUtils.XmlToArray(langItem["TEXTPOOL"], "item"))
            {
                var rawKey = item["KEY"] ?? item["ID"];
                var key = rawKey?.Text != null ? JsString.ToUpper(rawKey.Text) : null;
                if (key != null)
                {
                    textElements[key] = new ITextElement(XmlUtils.Unescape(item["ENTRY"]?.Text), JsUtils.ParseIntOrNaN(item["LENGTH"]?.Text, 10));
                }
            }
            this.textsTranslations.Add(new ITranslationTextElements { Language = langItem["LANGUAGE"]?.Text, TextElements = textElements });
        }
    }
}
