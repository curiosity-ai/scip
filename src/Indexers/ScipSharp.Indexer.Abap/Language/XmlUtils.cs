// Ported from abaplint packages/core/src/xml_utils.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
// XmlNode/XmlUtils.Parse replace the fast-xml-parser package used upstream (objects/_abstract_object.ts parseRaw2,
// options parseTagValue: false, ignoreAttributes: true, trimValues: false).
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ScipSharp.Indexer.Abap;

/// <summary>
/// An element as fast-xml-parser returns it: a text-only element is its text (an empty element is ""),
/// an element with child elements is an object keyed by child name, repeated children become an array.
/// Attributes are ignored. <c>node["NAME"]</c> is TS <c>node.NAME</c> (the first one when repeated, JS would
/// give the array), <c>All("NAME")</c> is TS <c>xmlToArray(node.NAME)</c> and <c>Text</c> is the string value.
/// </summary>
public sealed class XmlNode
{
    private readonly Dictionary<string, List<XmlNode>>? children;
    private readonly string? text;

    private XmlNode(Dictionary<string, List<XmlNode>>? children, string? text)
    {
        this.children = children;
        this.text = text;
    }

    public static XmlNode FromElement(XElement element)
    {
        if (!element.HasElements)
        {
            return new XmlNode(null, element.Value);
        }
        var children = new Dictionary<string, List<XmlNode>>();
        foreach (var c in element.Elements())
        {
            var name = QualifiedName(c);
            if (!children.TryGetValue(name, out var list))
            {
                list = new List<XmlNode>();
                children[name] = list;
            }
            list.Add(FromElement(c));
        }
        return new XmlNode(children, null);
    }

    private static string QualifiedName(XElement e)
    {
        var prefix = e.GetPrefixOfNamespace(e.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? e.Name.LocalName : prefix + ":" + e.Name.LocalName;
    }

    /// <summary>The string value, null when the element has child elements (an object in JS).</summary>
    public string? Text => this.text;

    public bool IsText => this.children == null;

    /// <summary>Child elements are present and repeated, JS would see an array.</summary>
    public bool IsArray(string name) => this.children != null && this.children.TryGetValue(name, out var l) && l.Count > 1;

    public XmlNode? this[string name]
    {
        get
        {
            if (this.children != null && this.children.TryGetValue(name, out var list))
            {
                return list[0];
            }
            return null;
        }
    }

    /// <summary>TS <c>xmlToArray(node.name)</c>.</summary>
    public List<XmlNode> All(string name)
    {
        if (this.children != null && this.children.TryGetValue(name, out var list))
        {
            return list;
        }
        return new List<XmlNode>();
    }

    public IEnumerable<string> Keys => this.children?.Keys ?? Enumerable.Empty<string>();
}

public static class XmlUtils
{
    /// <summary>fast-xml-parser parse(): the document as an object keyed by the root element name, null on invalid XML.</summary>
    public static XmlNode? Parse(string xml)
    {
        try
        {
            // fast-xml-parser accepts whitespace in front of the XML declaration, XDocument does not
            var doc = XDocument.Parse(xml.TrimStart('\uFEFF', ' ', '\t', '\r', '\n'), LoadOptions.PreserveWhitespace);
            if (doc.Root == null)
            {
                return null;
            }
            var wrapper = new XElement("wrapper", doc.Root);
            return XmlNode.FromElement(wrapper);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static List<XmlNode> XmlToArray(XmlNode? parent, string name)
    {
        return parent == null ? new List<XmlNode>() : parent.All(name);
    }

    public static string Unescape(string? str)
    {
        if (str == null)
        {
            return "";
        }
        var result = str.Replace("&amp;", "&");
        result = result.Replace("&gt;", ">");
        result = result.Replace("&lt;", "<");
        result = result.Replace("&quot;", "\"");
        result = result.Replace("&apos;", "'");
        return result;
    }
}

/// <summary>Helpers for reading AFF JSON files (JSON.parse upstream), absent values are null.</summary>
public static class Json
{
    public static System.Text.Json.JsonElement? Get(System.Text.Json.JsonElement e, params string[] path)
    {
        var cur = e;
        foreach (var p in path)
        {
            if (cur.ValueKind != System.Text.Json.JsonValueKind.Object || !cur.TryGetProperty(p, out var next))
            {
                return null;
            }
            cur = next;
        }
        return cur;
    }

    /// <summary>A string value; a number is converted like JS toString().</summary>
    public static string? Str(System.Text.Json.JsonElement e, params string[] path)
    {
        var v = Get(e, path);
        if (v == null)
        {
            return null;
        }
        return v.Value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => v.Value.GetString(),
            System.Text.Json.JsonValueKind.Number => v.Value.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            _ => null,
        };
    }

    public static IEnumerable<System.Text.Json.JsonElement> Array(System.Text.Json.JsonElement e, params string[] path)
    {
        var v = Get(e, path);
        if (v == null || v.Value.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return Enumerable.Empty<System.Text.Json.JsonElement>();
        }
        return v.Value.EnumerateArray().ToList();
    }
}
