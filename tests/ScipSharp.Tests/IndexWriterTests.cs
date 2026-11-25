using Google.Protobuf;

namespace ScipSharp.Tests;

public class IndexWriterTests
{
    [Test]
    public async Task StreamedOutputEqualsSerializedIndex()
    {
        var metadata = new Metadata { ProjectRoot = "file:///proj", ToolInfo = new ToolInfo { Name = "t" } };
        var docs = new[] { new Document { RelativePath = "a.cs" }, new Document { RelativePath = "b.cs" } };
        var ext = new SymbolInformation { Symbol = "local 1" };

        var expected = new Index { Metadata = metadata, Documents = { docs }, ExternalSymbols = { ext } };

        using var ms = new MemoryStream();
        using (var writer = new IndexWriter(ms, leaveOpen: true))
        {
            writer.WriteMetadata(metadata);
            foreach (var d in docs)
            {
                writer.WriteDocument(d);
            }

            writer.WriteExternalSymbol(ext);
        }

        await Assert.That(ms.ToArray()).IsEquivalentTo(expected.ToByteArray());
        await Assert.That(Index.Parser.ParseFrom(ms.ToArray())).IsEqualTo(expected);
    }
}
