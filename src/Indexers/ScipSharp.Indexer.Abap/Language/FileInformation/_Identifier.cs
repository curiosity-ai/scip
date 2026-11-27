// Ported from abaplint packages/core/src/abap/4_file_information/_identifier.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.FileInformation;

public class Identifier
{
    private readonly AbstractToken token;
    protected readonly string filename;

    public Identifier(AbstractToken token, string filename)
    {
        this.token = token;
        this.filename = filename;
    }

    public string GetName()
    {
        var name = token.GetStr();

        // todo, should this be handled in the parser instead?
        if (name.StartsWith('!'))
        {
            name = name.Substring(1);
        }

        return name;
    }

    public bool Equals(Identifier id)
    {
        return id.GetStart().Equals(GetStart()) && id.GetFilename() == GetFilename();
    }

    public AbstractToken GetToken() => token;

    public string GetFilename() => filename;

    public Position GetStart() => token.GetStart();

    public Position GetEnd() => token.GetEnd();
}
