using System.Collections;
using System.Globalization;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace ScipSharp.Cli;

/// <summary>
/// Reflection-based pretty printer for protobuf messages, modelled on the Go <c>pp</c> output
/// (<c>&amp;scip.Index{ Field: value, ... }</c>), optionally colored with ANSI escapes.
/// </summary>
internal sealed class PrettyPrinter(bool color)
{
    private const string Reset = "\u001b[0m";
    private const string FieldColor = "\u001b[36m";
    private const string StringColor = "\u001b[32m";
    private const string NumberColor = "\u001b[35m";
    private const string TypeColor = "\u001b[33m";

    private readonly StringBuilder _b = new();

    public string Print(IMessage message)
    {
        WriteMessage(message, 0);
        return _b.ToString();
    }

    private void Paint(string code, string text)
    {
        if (color)
        {
            _b.Append(code).Append(text).Append(Reset);
        }
        else
        {
            _b.Append(text);
        }
    }

    private void Indent(int level) => _b.Append(' ', level * 2);

    private void WriteMessage(IMessage message, int level)
    {
        Paint(TypeColor, "&scip." + message.Descriptor.Name);
        var fields = message.Descriptor.Fields.InFieldNumberOrder()
            .Where(f => f.ContainingOneof is null || f.Accessor.HasValue(message))
            .ToList();
        if (fields.Count == 0)
        {
            _b.Append("{}");
            return;
        }

        _b.Append("{\n");
        var width = fields.Max(static f => GoName(f).Length);
        foreach (var field in fields)
        {
            Indent(level + 1);
            Paint(FieldColor, GoName(field));
            _b.Append(':').Append(' ', width - GoName(field).Length + 1);
            WriteValue(field, field.Accessor.GetValue(message), level + 1);
            _b.Append(",\n");
        }

        Indent(level);
        _b.Append('}');
    }

    private void WriteValue(FieldDescriptor field, object? value, int level)
    {
        if (field.IsMap)
        {
            var map = (IDictionary)value!;
            _b.Append("map{");
            if (map.Count > 0)
            {
                _b.Append('\n');
                foreach (DictionaryEntry entry in map)
                {
                    Indent(level + 1);
                    WriteScalar(entry.Key);
                    _b.Append(": ");
                    WriteScalar(entry.Value);
                    _b.Append(",\n");
                }

                Indent(level);
            }

            _b.Append('}');
            return;
        }

        if (field.IsRepeated)
        {
            var list = (IList)value!;
            _b.Append("[]");
            if (list.Count == 0)
            {
                _b.Append("{}");
                return;
            }

            _b.Append("{\n");
            foreach (var item in list)
            {
                Indent(level + 1);
                WriteScalarOrMessage(item, level + 1);
                _b.Append(",\n");
            }

            Indent(level);
            _b.Append('}');
            return;
        }

        WriteScalarOrMessage(value, level);
    }

    private void WriteScalarOrMessage(object? value, int level)
    {
        if (value is IMessage message)
        {
            WriteMessage(message, level);
        }
        else
        {
            WriteScalar(value);
        }
    }

    private void WriteScalar(object? value)
    {
        switch (value)
        {
            case null:
                Paint(TypeColor, "nil");
                break;
            case string s:
                Paint(StringColor, QuoteString(s));
                break;
            case ByteString bytes:
                Paint(StringColor, $"[]byte{{{string.Join(", ", bytes.Select(static x => x.ToString(CultureInfo.InvariantCulture)))}}}");
                break;
            case bool flag:
                Paint(NumberColor, flag ? "true" : "false");
                break;
            case Enum e:
                Paint(NumberColor, e.ToString());
                break;
            case IFormattable f:
                Paint(NumberColor, f.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                _b.Append(value);
                break;
        }
    }

    private static string QuoteString(string s)
    {
        var b = new StringBuilder("\"");
        foreach (var c in s)
        {
            b.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        }

        return b.Append('"').ToString();
    }

    /// <summary>The Go struct field name of a proto field (<c>project_root</c> becomes <c>ProjectRoot</c>).</summary>
    private static string GoName(FieldDescriptor field) =>
        string.Concat(field.Name.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(static p => char.ToUpperInvariant(p[0]) + p[1..]));
}
