// Ported from abaplint packages/core/src/objects/message_class.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Objects;

public class ParsedMessageClass
{
    public string? TopName { get; set; }
    public string? Description { get; set; }
    public List<Message>? ParsedMessages { get; set; }
    public List<(string? language, string? number, string? text)>? TextsTranslations { get; set; }
}

public class MessageClass : AbstractObject
{
    private ParsedMessageClass? xml = null;

    public MessageClass(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "MSAG";
    }

    public override string? GetDescription()
    {
        this.ParseXML();
        return this.xml?.Description;
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 20, AllowNamespace = true };
    }

    public ParsedMessageClass? GetParsed()
    {
        this.ParseXML();
        return this.xml;
    }

    public List<(string? language, string? number, string? text)>? GetTextsTranslations()
    {
        this.ParseXML();
        return this.xml?.TextsTranslations;
    }

    public override void SetDirty()
    {
        this.xml = null;
        base.SetDirty();
    }

    public List<Message> GetMessages()
    {
        this.ParseXML();
        var msg = this.xml?.ParsedMessages;
        return msg ?? new List<Message>();
    }

    public Message? GetByNumber(string num)
    {
        this.ParseXML();
        // todo, optimize performance,
        foreach (var message in this.GetMessages())
        {
            if (message.GetNumber() == num)
            {
                return message;
            }
        }
        return null;
    }

    /////////////////////////////////

    private void ParseXML()
    {
        if (this.xml != null)
        {
            return;
        }

        this.xml = new ParsedMessageClass
        {
            TopName = null,
            Description = null,
            ParsedMessages = new List<Message>(),
            TextsTranslations = new List<(string? language, string? number, string? text)>(),
        };

        var parsed = base.ParseRaw2();
        if (parsed == null)
        {
            return;
        }

        var values = parsed["abapGit"]?["asx:abap"]?["asx:values"];
        this.xml.TopName = values?["T100A"]?["ARBGB"]?.Text;
        this.xml.Description = values?["T100A"]?["STEXT"]?.Text;

        var t100 = values?["T100"];
        if (t100 == null)
        {
            return;
        }
        foreach (var msg in t100.All("T100"))
        {
            this.xml.ParsedMessages!.Add(new Message(msg["MSGNR"]?.Text!, XmlUtils.Unescape(msg["TEXT"]?.Text), msg["ARBGB"]?.Text!));
        }

        var t100_texts = values?["T100_TEXTS"];
        if (t100_texts == null)
        {
            return;
        }
        foreach (var item in t100_texts.All("item"))
        {
            this.xml.TextsTranslations!.Add((item["SPRSL"]?.Text, item["MSGNR"]?.Text, XmlUtils.Unescape(item["TEXT"]?.Text)));
        }
    }
}
