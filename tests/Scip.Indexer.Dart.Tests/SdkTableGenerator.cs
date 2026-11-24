using System.IO.Compression;
using System.Text;

namespace Scip.Indexer.Dart.Tests;

/// <summary>
/// Regenerates <c>src/Indexers/Scip.Indexer.Dart/sdk-symbols.tsv.gz</c> (the SDK symbol table the indexer embeds so it
/// never needs a Dart toolchain at run time). Skipped unless <c>SCIP_DART_SDK_LIB</c> points at a Dart SDK's
/// <c>lib</c> directory; <c>SCIP_DART_SDK_OUT</c> is the output path.
/// </summary>
public class SdkTableGenerator
{
    private static readonly string[] Libraries = ["core", "async", "collection", "convert", "io", "math", "typed_data", "isolate", "developer", "_http", "_internal"];

    [Test]
    public async Task GenerateSdkTable()
    {
        var lib = Environment.GetEnvironmentVariable("SCIP_DART_SDK_LIB");
        var output = Environment.GetEnvironmentVariable("SCIP_DART_SDK_OUT");
        if (string.IsNullOrEmpty(lib) || string.IsNullOrEmpty(output))
        {
            return;
        }

        var sb = new StringBuilder();
        foreach (var name in Libraries)
        {
            var dirName = name == "_internal" ? "internal" : name;
            var dir = Path.Combine(lib, dirName);
            var mainName = File.Exists(Path.Combine(dir, dirName + ".dart")) ? dirName + ".dart" : dirName.TrimStart('_') + ".dart";
            var mainPath = Path.Combine(dir, mainName);
            if (!File.Exists(mainPath))
            {
                continue;
            }

            var id = "dart:" + name;
            sb.Append("L\t").Append(id).Append('\n');
            var main = DartParser.Parse(mainName, File.ReadAllText(mainPath));
            foreach (var d in main.Directives.Where(d => d.Text is "export" or "import"))
            {
                var uri = ExternalLibraries.StringValue(d.Uri.Text);
                if (!uri.StartsWith("dart:", StringComparison.Ordinal))
                {
                    continue;
                }

                sb.Append(d.Text == "export" ? "X\t" : "I\t").Append(uri).Append('\t')
                    .Append(string.Join(',', d.Show ?? [])).Append('\t').Append(string.Join(',', d.Hide ?? [])).Append('\t')
                    .Append(d.Prefix ?? "").Append('\n');
            }

            EmitFile(sb, id, null, main);
            foreach (var part in main.Directives.Where(d => d.Text == "part"))
            {
                var partName = ExternalLibraries.StringValue(part.Uri.Text);
                var partPath = Path.Combine(dir, partName);
                if (File.Exists(partPath))
                {
                    EmitFile(sb, id, partName, DartParser.Parse(partName, File.ReadAllText(partPath)));
                }
            }
        }

        await using var fs = File.Create(output);
        await using var gz = new GZipStream(fs, CompressionLevel.SmallestSize);
        await gz.WriteAsync(Encoding.UTF8.GetBytes(sb.ToString()));
        await Assert.That(sb.Length).IsGreaterThan(1000);
    }

    private static void EmitFile(StringBuilder sb, string lib, string? file, FileModel model)
    {
        // declarations of a library's main file are identified by the library uri alone (`dart:core/`), parts by `dart:core/part.dart`
        sb.Append("F\t").Append(file is null ? lib : lib + "/" + file).Append('\n');
        foreach (var d in model.TopLevel.Where(d => d.Kind != DeclKind.ImportPrefix && (!d.Name.StartsWith('_') || d.IsTypeLike)))
        {
            if (d.IsTypeLike)
            {
                var kind = d.Kind switch
                {
                    DeclKind.Class => "class",
                    DeclKind.Mixin => "mixin",
                    DeclKind.Enum => "enum",
                    DeclKind.TypeAlias => "typedef",
                    _ => "extension",
                };
                sb.Append("T\t").Append(kind).Append('\t').Append(d.Name).Append('\t').Append(string.Join(',', d.Supers)).Append("\t\n");
                foreach (var m in d.Members.Where(m => (!m.Name.StartsWith('_') || m.Kind == DeclKind.Constructor && m.Name.Length == 0) && m.Kind != DeclKind.Parameter))
                {
                    var mk = m.Kind switch
                    {
                        DeclKind.Method => "M",
                        DeclKind.Property => m.IsGetter ? "G" : "S",
                        DeclKind.Constructor => "K",
                        _ => "D",
                    };
                    sb.Append("m\t").Append(mk).Append('\t').Append(m.Name).Append('\t').Append(m.IsStatic ? '1' : '0').Append('\t').Append(m.TypeName ?? "").Append('\n');
                }
            }
            else
            {
                var k = d.Kind switch
                {
                    DeclKind.Function => "f",
                    DeclKind.Variable => "v",
                    DeclKind.Property => d.IsGetter ? "g" : "s",
                    _ => null,
                };
                if (k is not null)
                {
                    sb.Append("t\t").Append(k).Append('\t').Append(d.Name).Append('\t').Append(d.TypeName ?? "").Append('\n');
                }
            }
        }
    }
}
