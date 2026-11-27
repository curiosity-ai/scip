// Ported from abaplint packages/core/src/macro_references.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap;

public class MacroReferences : IMacroReferences
{
    private readonly Dictionary<string, List<(AbstractToken token, Position start, Position end)>> definitions = new Dictionary<string, List<(AbstractToken token, Position start, Position end)>>();

    private readonly Dictionary<string, List<IFilenameAndToken>> references = new Dictionary<string, List<IFilenameAndToken>>();

    public void AddDefinition(IFilenameAndToken @ref, Position start, Position end)
    {
        if (this.definitions.GetValueOrDefault(@ref.Filename) == null)
        {
            this.definitions[@ref.Filename] = new List<(AbstractToken token, Position start, Position end)>();
        }
        else if (this.definitions[@ref.Filename].Any(d => d.token.GetStart().Equals(@ref.Token.GetStart())))
        {
            return;
        }
        this.definitions.GetValueOrDefault(@ref.Filename).Add((@ref.Token, start, end));
    }

    public (Position Start, Position End)? GetDefinitionRange(string filename, AbstractToken token)
    {
        foreach (var d in this.definitions.GetValueOrDefault(filename) ?? new List<(AbstractToken token, Position start, Position end)>())
        {
            if (d.token.GetStart().Equals(token.GetStart()))
            {
                return (d.start, d.end);
            }
        }
        return null;
    }

    public void AddReference(IFilenameAndToken @ref)
    {
        if (this.references.GetValueOrDefault(@ref.Filename) == null)
        {
            this.references[@ref.Filename] = new List<IFilenameAndToken>();
        }
        this.references.GetValueOrDefault(@ref.Filename).Add(@ref);
    }

    public List<AbstractToken> ListDefinitionsByFile(string filename)
    {
        List<AbstractToken> ret = new List<AbstractToken>();
        foreach (var d in this.definitions.GetValueOrDefault(filename) ?? new List<(AbstractToken token, Position start, Position end)>())
        {
            ret.Add(d.token);
        }
        return ret;
    }

    public List<IFilenameAndToken> ListUsagesbyMacro(string filename, AbstractToken token)
    {
        List<IFilenameAndToken> ret = new List<IFilenameAndToken>();
        var tokenStr = JsUtils.ToUpper(token.GetStr());

        foreach (var @ref in this.references.GetValueOrDefault(filename) ?? new List<IFilenameAndToken>())
        {
            if (JsUtils.ToUpper(@ref.Token.GetStr()) == tokenStr)
            {
                ret.Add(@ref);
            }
        }
        return ret;
    }

    public void Clear(string filename)
    {
        this.definitions.Remove(filename);
        this.references.Remove(filename);
    }

    public IFilenameAndToken? FindDefinitionByUsage(string filename, AbstractToken token)
    {
        var tokenStr = JsUtils.ToUpper(token.GetStr());

        foreach (var @ref in this.references.GetValueOrDefault(filename) ?? new List<IFilenameAndToken>())
        {
            if (@ref.Token.GetStart().Equals(token.GetStart()))
            {
                foreach (var d in this.definitions.GetValueOrDefault(@ref.Filename) ?? new List<(AbstractToken token, Position start, Position end)>())
                {
                    if (JsUtils.ToUpper(d.token.GetStr()) == tokenStr)
                    {
                        return new IFilenameAndToken(@ref.Filename, d.token);
                    }
                }
            }
        }
        return null;
    }
}
