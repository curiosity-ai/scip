// Ported from abaplint packages/core/src/msag_references.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap;

public class MSAGReferences : IMSAGReferences
{
    private readonly Dictionary<string, Dictionary<string, List<(string filename, AbstractToken token)>>> nameNumberIndex = new Dictionary<string, Dictionary<string, List<(string filename, AbstractToken token)>>>();
    private readonly Dictionary<string, List<(AbstractToken token, string messageClass, string number)>> filenameIndex = new Dictionary<string, List<(AbstractToken token, string messageClass, string number)>>();

    public void AddUsing(string filename, AbstractToken token, string messageClass, string number)
    {
        if (number.Length != 3)
        {
            return;
        }

        if (this.filenameIndex.GetValueOrDefault(filename) == null)
        {
            this.filenameIndex[filename] = new List<(AbstractToken token, string messageClass, string number)>();
        }
        this.filenameIndex.GetValueOrDefault(filename).Add((token, messageClass, number));

        if (this.nameNumberIndex.GetValueOrDefault(messageClass) == null)
        {
            this.nameNumberIndex[messageClass] = new Dictionary<string, List<(string filename, AbstractToken token)>>();
        }
        if (this.nameNumberIndex.GetValueOrDefault(messageClass).GetValueOrDefault(number) == null)
        {
            this.nameNumberIndex.GetValueOrDefault(messageClass)[number] = new List<(string filename, AbstractToken token)>();
        }
        this.nameNumberIndex.GetValueOrDefault(messageClass).GetValueOrDefault(number).Add((filename, token));
    }

    public void Clear(IObject obj)
    {
        foreach (var file in obj.GetFiles())
        {
            var filename = file.GetFilename();
            foreach (var fIndex in this.filenameIndex.GetValueOrDefault(filename) ?? new List<(AbstractToken token, string messageClass, string number)>())
            {
                // this should be okay for performance, each message should be referenced less than 10 times typically
                this.nameNumberIndex.GetValueOrDefault(fIndex.messageClass)[fIndex.number] = this.nameNumberIndex.GetValueOrDefault(fIndex.messageClass).GetValueOrDefault(fIndex.number).Where(i => i.filename != filename).ToList();
            }
            this.filenameIndex.Remove(filename);
        }
    }

    public List<(AbstractToken token, string messageClass, string number)> ListByFilename(string filename)
    {
        return this.filenameIndex.GetValueOrDefault(filename) ?? new List<(AbstractToken token, string messageClass, string number)>();
    }

    public List<(string filename, AbstractToken token)> ListByMessage(string messageClass, string number)
    {
        return this.nameNumberIndex.GetValueOrDefault(messageClass)?.GetValueOrDefault(number) ?? new List<(string filename, AbstractToken token)>();
    }
}
