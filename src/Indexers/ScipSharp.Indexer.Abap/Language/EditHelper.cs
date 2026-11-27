// Ported from abaplint packages/core/src/edit_helper.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
// TODO: only the data types are ported, EditDraft/EditHelper are not yet.
namespace ScipSharp.Indexer.Abap;

public sealed record IRange(Position Start, Position End);

public sealed record ITextEdit(IRange Range, string NewText);

/// <summary>TS <c>{[filename: string]: ITextEdit[]}</c>.</summary>
public sealed class IEdit : Dictionary<string, List<ITextEdit>>
{
}
