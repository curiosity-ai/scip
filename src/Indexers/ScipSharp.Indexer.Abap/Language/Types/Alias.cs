// Ported from abaplint packages/core/src/abap/types/alias.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.Types;

public class Alias : global::ScipSharp.Indexer.Abap.FileInformation.Identifier
{
    private readonly Visibility visibility;
    private readonly string component;

    public Alias(AbstractToken token, Visibility visibility, string component, string filename)
        : base(token, filename)
    {
        this.component = component;
        this.visibility = visibility;
    }

    public string GetComponent()
    {
        return this.component;
    }

    public Visibility GetVisibility()
    {
        return this.visibility;
    }
}
