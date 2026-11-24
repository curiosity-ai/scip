using Google.Protobuf;

namespace Scip;

/// <summary>
/// Streams a SCIP <see cref="Index"/> to a stream one message at a time, so indexers never need to hold every
/// <see cref="Document"/> in memory. The output is wire-identical to serialising a fully built <see cref="Index"/>.
/// </summary>
public sealed class IndexWriter : IDisposable
{
    private const int MetadataField = 1;
    private const int DocumentsField = 2;
    private const int ExternalSymbolsField = 3;

    private readonly CodedOutputStream _out;

    public IndexWriter(Stream stream, bool leaveOpen = false) => _out = new CodedOutputStream(stream, leaveOpen);

    public void WriteMetadata(Metadata metadata) => WriteField(MetadataField, metadata);

    public void WriteDocument(Document document) => WriteField(DocumentsField, document);

    public void WriteExternalSymbol(SymbolInformation symbol) => WriteField(ExternalSymbolsField, symbol);

    private void WriteField(int field, IMessage message)
    {
        _out.WriteTag(field, WireFormat.WireType.LengthDelimited);
        _out.WriteMessage(message);
    }

    public void Dispose() => _out.Dispose();
}
