// Ported from abaplint packages/core/src/abap/types/class_constant.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;

namespace ScipSharp.Indexer.Abap.Types;

public class ClassConstant : TypedIdentifier
{
    private readonly Visibility visibility;

    public ClassConstant(TypedIdentifier id, Visibility visibility, object? value)
        : base(id.GetToken(), id.GetFilename(), id.GetType(), new List<string> { IdentifierMeta.ReadOnly, IdentifierMeta.Static }, value)
    {
        this.visibility = visibility;
    }

    public Visibility GetVisibility()
    {
        return this.visibility;
    }
}
