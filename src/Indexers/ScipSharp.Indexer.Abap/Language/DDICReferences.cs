// Ported from abaplint packages/core/src/ddic_references.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap;

public class DDICReferences : IDDICReferences
{
    private readonly Dictionary<string, Dictionary<string, List<IObjectAndToken>>> nameTypeIndex = new Dictionary<string, Dictionary<string, List<IObjectAndToken>>>();
    private readonly Dictionary<string, Dictionary<int, List<IObjectAndToken>>> filenameIndex = new Dictionary<string, Dictionary<int, List<IObjectAndToken>>>();

    public void SetUsing(IObject obj, List<IObjectAndToken> @using)
    {
        this.Clear(obj);
        foreach (var u in @using)
        {
            this.AddUsing(obj, u);
        }
    }

    public void AddUsing(IObject obj, IObjectAndToken? @using)
    {
        if (@using == null)
        {
            return;
        }

        // add to name and type index
        var newName = JsUtils.ToUpper(obj.GetName());
        var newType = obj.GetType();
        if (this.nameTypeIndex.GetValueOrDefault(newName) == null)
        {
            this.nameTypeIndex[newName] = new Dictionary<string, List<IObjectAndToken>>();
        }
        if (this.nameTypeIndex.GetValueOrDefault(newName).GetValueOrDefault(newType) == null)
        {
            this.nameTypeIndex.GetValueOrDefault(newName)[newType] = new List<IObjectAndToken>();
        }
        this.nameTypeIndex.GetValueOrDefault(newName).GetValueOrDefault(newType).Add(@using);

        // add to filename index
        if (!string.IsNullOrEmpty(@using.Filename) && @using.Token != null)
        {
            if (this.filenameIndex.GetValueOrDefault(@using.Filename) == null)
            {
                this.filenameIndex[@using.Filename] = new Dictionary<int, List<IObjectAndToken>>();
            }
            if (this.filenameIndex.GetValueOrDefault(@using.Filename).GetValueOrDefault(@using.Token.GetRow()) == null)
            {
                this.filenameIndex.GetValueOrDefault(@using.Filename)[@using.Token.GetRow()] = new List<IObjectAndToken>();
            }
            this.filenameIndex.GetValueOrDefault(@using.Filename).GetValueOrDefault(@using.Token.GetRow()).Add(@using);
        }
    }

    public void Clear(IObject obj)
    {
        // remove from filenameIndex first
        foreach (var u in this.ListUsing(obj))
        {
            if (!string.IsNullOrEmpty(u.Filename) && u.Token != null)
            {
                var found = this.filenameIndex.GetValueOrDefault(u.Filename)?.GetValueOrDefault(u.Token.GetRow());
                if (found != null)
                {
                    JsUtils.Pop(found);
                    // TODODOD, this assumes there is max one reference on each line
                }
            }
        }

        // remove from name + type index
        var name = JsUtils.ToUpper(obj.GetName());
        var type = obj.GetType();
        if ((this.nameTypeIndex.GetValueOrDefault(name)?.GetValueOrDefault(type)) != null)
        {
            this.nameTypeIndex.GetValueOrDefault(name)[type] = new List<IObjectAndToken>();
        }
    }

    public List<IObjectAndToken> ListByFilename(string filename, int line)
    {
        return this.filenameIndex.GetValueOrDefault(filename)?.GetValueOrDefault(line) ?? new List<IObjectAndToken>();
    }

    public List<IObjectAndToken> ListUsing(IObject obj)
    {
        var newName = JsUtils.ToUpper(obj.GetName());
        var newType = obj.GetType();

        var found = this.nameTypeIndex.GetValueOrDefault(newName)?.GetValueOrDefault(newType);
        if (found != null)
        {
            return found;
        }
        else
        {
            return new List<IObjectAndToken>();
        }
    }

    public List<(string type, string name, AbstractToken? token, string? filename)> ListWhereUsed(IObject obj)
    {
        // todo, add reverse index, this is slow
        var ret = new List<(string type, string name, AbstractToken? token, string? filename)>();
        var searchName = JsUtils.ToUpper(obj.GetName());
        var searchType = obj.GetType();

        foreach (var name in this.nameTypeIndex.Keys.ToList())
        {
            foreach (var type in this.nameTypeIndex.GetValueOrDefault(name).Keys.ToList())
            {
                foreach (var f in this.nameTypeIndex.GetValueOrDefault(name).GetValueOrDefault(type))
                {
                    if (f.Object != null && f.Object.GetType() == searchType && f.Object.GetName() == searchName)
                    {
                        ret.Add((type, name, f.Token, f.Filename));
                    }
                }
            }
        }

        return ret;
    }
}
