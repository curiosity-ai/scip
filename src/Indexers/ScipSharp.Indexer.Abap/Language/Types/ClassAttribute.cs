// Ported from abaplint packages/core/src/abap/types/class_attribute.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;

namespace ScipSharp.Indexer.Abap.Types;

public class ClassAttribute : TypedIdentifier
{
    private readonly Visibility visibility;

    public ClassAttribute(TypedIdentifier id, Visibility visibility, List<string> meta, object? value = null)
        : base(id.GetToken(), id.GetFilename(), id.GetType(), meta, value)
    {
        this.visibility = visibility;
    }

    public Visibility GetVisibility()
    {
        return this.visibility;
    }
}
