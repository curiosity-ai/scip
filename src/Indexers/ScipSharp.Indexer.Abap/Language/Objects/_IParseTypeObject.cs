// C# addition, not a port of an upstream file: TS calls parseType(reg) on unions of DDIC object classes
// (DatabaseTableSource = Table | DataDefinition | View, and the DDIC objects in find_global_definitions.ts),
// in C# those classes implement this interface.
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Objects;

public interface IParseTypeObject : IObject
{
    AbstractType ParseType(IRegistry reg);
}
