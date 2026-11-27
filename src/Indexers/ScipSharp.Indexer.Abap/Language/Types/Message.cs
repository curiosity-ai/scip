// Ported from abaplint packages/core/src/abap/types/message.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Types;

public class Message
{
    private readonly string number;
    // the actual text,
    private readonly string message;
    private readonly string className;

    public Message(string number, string message, string className)
    {
        this.number = number;
        if (this.number == null)
        {
            this.number = "";
        }
        this.message = message;
        if (this.message == null)
        {
            this.message = "";
        }
        this.className = className;
        if (this.className == null)
        {
            this.className = "";
        }
    }

    public string GetMessageClass()
    {
        return this.className;
    }

    public string GetNumber()
    {
        return this.number;
    }

    public string GetMessage()
    {
        return this.message;
    }

    public int GetPlaceholderCount()
    {
        var escaped = (JsUtils.Match(this.GetMessage(), @"&&", RegexOptions.ECMAScript, true) ?? new List<string>()).Count;
        return (JsUtils.Match(this.GetMessage(), @"&", RegexOptions.ECMAScript, true) ?? new List<string>()).Count - escaped * 2;
    }
}
