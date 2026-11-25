using Kind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.DotNet.Tests;

[NotInParallel]
public class SymbolKindTests
{
    private static readonly Dictionary<string, Kind> ExpectedKinds = new()
    {
        ["SymbolKinds/IShape#"] = Kind.Interface,
        ["SymbolKinds/Point#"] = Kind.Struct,
        ["SymbolKinds/Color#"] = Kind.Enum,
        ["SymbolKinds/Color#Red."] = Kind.EnumMember,
        ["SymbolKinds/Handler#"] = Kind.Delegate,
        ["SymbolKinds/Kinds#"] = Kind.Class,
        ["SymbolKinds/Kinds#Constant."] = Kind.Constant,
        ["SymbolKinds/Kinds#Field."] = Kind.Field,
        ["SymbolKinds/Kinds#StaticField."] = Kind.StaticField,
        ["SymbolKinds/Kinds#`.ctor`()."] = Kind.Constructor,
        ["SymbolKinds/Kinds#Property."] = Kind.Property,
        ["SymbolKinds/Kinds#StaticProperty."] = Kind.StaticProperty,
        ["SymbolKinds/Kinds#Event#"] = Kind.Event,
        ["SymbolKinds/Kinds#StaticEvent#"] = Kind.StaticEvent,
        ["SymbolKinds/Kinds#Method()."] = Kind.Method,
        ["SymbolKinds/Kinds#StaticMethod()."] = Kind.StaticMethod,
        ["SymbolKinds/KindsExtensions#"] = Kind.Class,
        ["SymbolKinds/KindsExtensions#Extension()."] = Kind.StaticMethod,
        ["SymbolKinds/VBClass#"] = Kind.Class,
        ["SymbolKinds/VBModule#"] = Kind.Module,
    };

    [Test]
    public async Task DefinitionsCarrySymbolKindAndDocumentsDeclareUtf16()
    {
        var directory = FixtureIndexer.CopyFixture("symbol-kinds");
        try
        {
            var index = await FixtureIndexer.IndexAsync(directory);
            var infos = index.Documents.SelectMany(document => document.Symbols).ToList();
            foreach (var (suffix, expected) in ExpectedKinds)
            {
                var matches = infos.Where(info => info.Symbol.EndsWith(" " + suffix, StringComparison.Ordinal))
                    .Select(info => info.Kind).ToList();
                await Assert.That(matches.Count).IsEqualTo(1);
                await Assert.That(matches[0]).IsEqualTo(expected);
            }

            await Assert.That(index.Documents.Select(document => document.Language).Distinct().Order())
                .IsEquivalentTo(["C#", "Visual Basic"]);
            await Assert.That(index.Documents.All(d => d.PositionEncoding == PositionEncoding.Utf16CodeUnitOffsetFromLineStart))
                .IsTrue();

            // Each reference follows a non-BMP character: UTF-8 bytes and Unicode scalar offsets
            // both differ from these UTF-16 code-unit offsets.
            foreach (var (path, suffix, line, start, end) in new[]
                     {
                         ("CSharpKinds/Kinds.cs", "/Kinds#StaticMethod().", 35, 21, 33),
                         ("VBKinds/Kinds.vb", "/VBModule#ModuleMethod().", 6, 32, 44),
                     })
            {
                var document = index.Documents.Single(d => d.RelativePath.Replace('\\', '/') == path);
                var reference = document.Occurrences.Single(o => o.Symbol.EndsWith(suffix, StringComparison.Ordinal)
                    && (o.SymbolRoles & (int)SymbolRole.Definition) == 0);
                await Assert.That(reference.SingleLineRange.Line).IsEqualTo(line);
                await Assert.That(reference.SingleLineRange.StartCharacter).IsEqualTo(start);
                await Assert.That(reference.SingleLineRange.EndCharacter).IsEqualTo(end);
            }
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }
}
