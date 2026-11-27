// Ported from abaplint packages/core/src/abap/3_structures/structures/_match.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

public sealed class IMatch
{
    public List<StatementNode> Matched { get; set; } = [];
    public List<StatementNode> Unmatched { get; set; } = [];
    public bool Error { get; set; }
    public string ErrorDescription { get; set; } = "";
    public int ErrorMatched { get; set; }
}

/// <summary>A match from an index: statements[index] up to statements[next] were matched.</summary>
public sealed class IMatchAt
{
    public int Next { get; set; }
    public bool Error { get; set; }
    public string ErrorDescription { get; set; } = "";
    public int ErrorMatched { get; set; }
}
