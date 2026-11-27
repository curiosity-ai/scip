// Ported from abaplint packages/core/src/ddic.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Objects;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap;

public class ILookupResult
{
    public AbstractType Type { get; set; }
    public IObject? Object { get; set; }
}

public class DDIC
{
    private readonly IRegistry reg;

    public DDIC(IRegistry reg)
    {
        this.reg = reg;
    }

    // the class might be local with a local super class with a global exception class as super
    // todo: returns true for both local and global exception classes
    public bool IsException(InfoClassDefinition? def, ABAPObject _obj)
    {
        if (def == null)
        {
            return false;
        }
        if (JsUtils.ToUpper(def.Name) == "CX_ROOT")
        {
            return true;
        }
        string? superClassName = def.SuperClassName;
        if (superClassName == null)
        {
            return false;
        }

        int i = 0;
        // max depth, make sure not to hit cyclic super class defintions
        while (i++ < 10 && superClassName != null)
        {
            var found = ((ABAPObject?)(this.reg.GetObject("CLAS", superClassName)));
            if (found == null)
            {
                break;
            }

            InfoClassDefinition? superDef = found.GetMainABAPFile()?.GetInfo().GetClassDefinitionByName(superClassName);
            if (superDef == null)
            {
                break;
            }

            if (!string.IsNullOrEmpty(superDef.SuperClassName))
            {
                superClassName = superDef.SuperClassName;
            }
            else
            {
                break;
            }
        }

        // todo, this should check for "CX_ROOT"
        bool isException = ((JsUtils.Match(superClassName, @"^.?cx_.*$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase, false)) != null || (JsUtils.Match(superClassName, @"^\/.+\/cx_.*$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase, false)) != null) ? true : false;

        return isException;
    }

    public AbstractType? LookupBuiltinType(string name, int? length = null, int? decimals = null, string? qualifiedName = null)
    {
        switch (name)
        {
            case "STRING":
                return StringType.Get(new AbstractTypeData { QualifiedName = JsUtils.Or(qualifiedName, name) });
            case "XSTRING":
                return XStringType.Get(new AbstractTypeData { QualifiedName = JsUtils.Or(qualifiedName, name) });
            case "D":
                /*
                if (length && length > 0) {
                throw new Error("Length for type D not possible");
                } else if (decimals && decimals > 0) {
                throw new Error("Decimals for type D not possible");
                }
                 */
                return new DateType(new AbstractTypeData { QualifiedName = JsUtils.Or(qualifiedName, name) });
            case "T":
                /*
                if (length && length > 0) {
                throw new Error("Length for type T not possible");
                } else if (decimals && decimals > 0) {
                throw new Error("Decimals for type T not possible");
                }
                 */
                return new TimeType(new AbstractTypeData { QualifiedName = JsUtils.Or(qualifiedName, name) });
            case "XSEQUENCE":
                return new XSequenceType(new AbstractTypeData { QualifiedName = qualifiedName });
            case "CLIKE":
                return CLikeType.Get();
            case "DECFLOAT":
                return new DecFloatType(new AbstractTypeData { QualifiedName = qualifiedName });
            case "ANY":
                return AnyType.Get(new AbstractTypeData { QualifiedName = qualifiedName });
            case "SIMPLE":
                return SimpleType.Get();
            case "%_C_POINTER":
                return new HexType(8, qualifiedName);
            case "TABLE":
                return new global::ScipSharp.Indexer.Abap.Types.Basic.TableType(AnyType.Get(), new ITableOptions { WithHeader = false, KeyType = TableKeyType.@default });
            case "DATA":
                return new DataType(new AbstractTypeData { QualifiedName = qualifiedName });
            case "NUMERIC":
                return new NumericGenericType(new AbstractTypeData { QualifiedName = qualifiedName });
            case "UTCLONG":
                return new UTCLongType(new AbstractTypeData { QualifiedName = qualifiedName });
            case "DECFLOAT16":
                return new DecFloat16Type(new AbstractTypeData { QualifiedName = qualifiedName });
            case "DECFLOAT34":
                return new DecFloat34Type(new AbstractTypeData { QualifiedName = qualifiedName });
            case "CSEQUENCE":
                return new CSequenceType(new AbstractTypeData { QualifiedName = qualifiedName });
            case "I":
                return IntegerType.Get(new AbstractTypeData { QualifiedName = JsUtils.Or(qualifiedName, name) });
            case "INT8":
                return new Integer8Type(new AbstractTypeData { QualifiedName = JsUtils.Or(qualifiedName, name) });
            case "F":
                /*
                if (length && length > 0) {
                throw new Error("Length for type F not possible");
                } else if (decimals && decimals > 0) {
                throw new Error("Decimals for type F not possible");
                }
                 */
                return new FloatType(new AbstractTypeData { QualifiedName = JsUtils.Or(qualifiedName, name) });
            case "P":
                if ((length ?? 0) != 0 && (decimals ?? 0) != 0)
                {
                    return new PackedType(length.Value, decimals.Value, new AbstractTypeData { QualifiedName = qualifiedName });
                }
                else if ((length ?? 0) != 0)
                {
                    return new PackedType(length.Value, 0, new AbstractTypeData { QualifiedName = qualifiedName });
                }
                else
                {
                    return new PackedType(8, decimals ?? 0, new AbstractTypeData { QualifiedName = qualifiedName });
                }
                goto case "C";
            case "C":
                if ((length ?? 0) != 0)
                {
                    return new CharacterType(length.Value, new AbstractTypeData { QualifiedName = qualifiedName });
                }
                else
                {
                    return new CharacterType(1, new AbstractTypeData { QualifiedName = qualifiedName });
                }
                goto case "X";
            case "X":
                if ((length ?? 0) != 0)
                {
                    return new HexType(length.Value, qualifiedName);
                }
                else
                {
                    return new HexType(1, qualifiedName);
                }
                goto case "N";
            case "N":
                if ((length ?? 0) != 0)
                {
                    return new NumericType(length.Value, qualifiedName);
                }
                else
                {
                    return new NumericType(1, qualifiedName);
                }
                break;
        }
        return null;
    }

    public bool InErrorNamespace(string? name)
    {
        if (name == null)
        {
            return true;
        }
        return this.reg.InErrorNamespace(name);
    }

    public ILookupResult LookupObject(string name)
    {
        var clas = this.reg.GetObject("CLAS", name);
        var globalClas = clas?.GetIdentifier();
        if (globalClas != null)
        {
            return new ILookupResult { Type = new ObjectReferenceType(globalClas, new AbstractTypeData { QualifiedName = name, RTTIName = "\\CLASS=" + name }), Object = clas };
        }
        var intf = this.reg.GetObject("INTF", name);
        var globalIntf = intf?.GetIdentifier();
        if (globalIntf != null)
        {
            return new ILookupResult { Type = new ObjectReferenceType(globalIntf, new AbstractTypeData { QualifiedName = name, RTTIName = "\\INTERFACE=" + name }), Object = intf };
        }
        if (this.InErrorNamespace(name) == true)
        {
            return new ILookupResult { Type = new UnknownType(name) };
        }
        else
        {
            return new ILookupResult { Type = VoidType.Get(name) };
        }
    }

    public ILookupResult? LookupNoVoid(string name)
    {
        var foundTABL = ((Table?)(this.reg.GetObject("TABL", name)));
        if (foundTABL != null)
        {
            return new ILookupResult { Type = foundTABL.ParseType(this.reg), Object = foundTABL };
        }

        var foundVIEW = this.reg.GetObject("VIEW", name) as View; // TS casts to Table, the object is a View
        if (foundVIEW != null)
        {
            return new ILookupResult { Type = foundVIEW.ParseType(this.reg), Object = foundVIEW };
        }

        var foundTTYP = ((global::ScipSharp.Indexer.Abap.Objects.TableType?)(this.reg.GetObject("TTYP", name)));
        if (foundTTYP != null)
        {
            return new ILookupResult { Type = foundTTYP.ParseType(this.reg), Object = foundTTYP };
        }

        var foundDTEL = ((DataElement?)(this.reg.GetObject("DTEL", name)));
        if (foundDTEL != null)
        {
            return new ILookupResult { Type = foundDTEL.ParseType(this.reg), Object = foundDTEL };
        }

        var foundDDLS = this.LookupDDLS(name);
        if (foundDDLS != null)
        {
            return new ILookupResult { Type = foundDDLS.Value.type, Object = foundDDLS.Value.@object };
        }

        return null;
    }

    public (AbstractType type, DataDefinition @object)? LookupDDLS(string? name = null)
    {
        if (name == null)
        {
            return null;
        }

        var upper = JsUtils.ToUpper(name);
        foreach (var obj in this.reg.GetObjectsByType("DDLS"))
        {
            var ddls = ((DataDefinition)obj);
            if (ddls.GetSQLViewName() == upper || JsUtils.ToUpper(ddls.GetDefinitionName()) == upper)
            {
                return (ddls.ParseType(this.reg), ddls);
            }
        }

        return null;
    }

    /** lookup with voiding and unknown types */
    public ILookupResult Lookup(string name)
    {
        var found = this.LookupNoVoid(name);
        if (found != null)
        {
            return found;
        }

        if (this.reg.InErrorNamespace(name))
        {
            return new ILookupResult { Type = new UnknownType(name + " not found, lookup") };
        }
        else
        {
            return new ILookupResult { Type = VoidType.Get(name) };
        }
    }

    public ILookupResult LookupDomain(string name, string? dataElement = null, string? description = null)
    {
        var found = ((Domain?)(this.reg.GetObject("DOMA", name)));
        if (found != null)
        {
            return new ILookupResult { Type = found.ParseDomainType(this.reg, dataElement, description), Object = found };
        }
        else if (this.reg.InErrorNamespace(name))
        {
            return new ILookupResult { Type = new UnknownType(name + ", lookupDomain"), Object = null };
        }
        else
        {
            return new ILookupResult { Type = VoidType.Get(name), Object = null };
        }
    }

    public ILookupResult LookupDataElement(string? name)
    {
        if (name == null)
        {
            return new ILookupResult { Type = new UnknownType("undefined, lookupDataElement") };
        }
        var found = ((DataElement?)(this.reg.GetObject("DTEL", name)));
        if (found != null)
        {
            return new ILookupResult { Type = found.ParseType(this.reg), Object = found };
        }
        else if (this.reg.InErrorNamespace(name))
        {
            return new ILookupResult { Type = new UnknownType(name + " not found, lookupDataElement") };
        }
        else
        {
            return new ILookupResult { Type = VoidType.Get(name) };
        }
    }

    public ILookupResult LookupTableOrView(string? name)
    {
        if (name == null)
        {
            return new ILookupResult { Type = new UnknownType("undefined, lookupTableOrView") };
        }
        var foundTABL = ((Table?)(this.reg.GetObject("TABL", name)));
        if (foundTABL != null)
        {
            return new ILookupResult { Type = foundTABL.ParseType(this.reg), Object = foundTABL };
        }
        var foundDDLS = this.LookupDDLS(name);
        if (foundDDLS != null)
        {
            return new ILookupResult { Type = foundDDLS.Value.type, Object = foundDDLS.Value.@object };
        }
        return this.LookupView(name);
    }

    /** this method only looks up the object, does not parse the type */
    public IParseTypeObject? LookupTableOrView2(string? name)
    {
        if (name == null)
        {
            return null;
        }
        var foundTABL = ((Table?)(this.reg.GetObject("TABL", name)));
        if (foundTABL != null)
        {
            return foundTABL;
        }
        var foundVIEW = ((View?)(this.reg.GetObject("VIEW", name)));
        if (foundVIEW != null)
        {
            return foundVIEW;
        }
        var foundDDLS = this.LookupDDLS(name);
        if (foundDDLS != null)
        {
            return foundDDLS.Value.@object;
        }
        return null;
    }

    public AbstractType LookupTable(string? name)
    {
        if (name == null)
        {
            return new UnknownType("undefined, lookupTable");
        }
        var found = ((Table?)(this.reg.GetObject("TABL", name)));
        if (found != null)
        {
            return found.ParseType(this.reg);
        }
        else if (this.reg.InErrorNamespace(name))
        {
            return new UnknownType(name + " not found, lookupTable");
        }
        else
        {
            return VoidType.Get(name);
        }
    }

    private ILookupResult LookupView(string? name)
    {
        if (name == null)
        {
            return new ILookupResult { Type = new UnknownType("undefined, lookupView") };
        }
        var found = this.reg.GetObject("VIEW", name) as View; // TS casts to Table, the object is a View
        if (found != null)
        {
            return new ILookupResult { Type = found.ParseType(this.reg), Object = found };
        }
        else if (this.reg.InErrorNamespace(name))
        {
            return new ILookupResult { Type = new UnknownType(name + " not found, lookupView") };
        }
        else
        {
            return new ILookupResult { Type = VoidType.Get(name) };
        }
    }

    public ILookupResult LookupTableType(string? name)
    {
        if (name == null)
        {
            return new ILookupResult { Type = new UnknownType("undefined, lookupTableType") };
        }
        var found = ((global::ScipSharp.Indexer.Abap.Objects.TableType?)(this.reg.GetObject("TTYP", name)));
        if (found != null)
        {
            return new ILookupResult { Type = found.ParseType(this.reg), Object = found };
        }
        else if (this.reg.InErrorNamespace(name))
        {
            return new ILookupResult { Type = new UnknownType(name + " not found, lookupTableType") };
        }
        else
        {
            return new ILookupResult { Type = VoidType.Get(name) };
        }
    }

    public AbstractType TextToType((string? text, string? length, string? decimals, string infoText, string? qualifiedName, string? conversionExit, string? ddicName, string? description) input)
    {

        // todo: support short strings, and length of different integers, NUMC vs CHAR, min/max length
        var extra = new AbstractTypeData { QualifiedName = input.qualifiedName, ConversionExit = input.conversionExit, DdicName = input.ddicName, Description = input.description };

        switch (input.text)
        {
            case "DEC":
            case "D16F":
            case "D16D":
            case "D34D":
            case "D34F":
            case "DF16_DEC":
            case "DF34_DEC":
            case "CURR":
            case "QUAN":
            {
                if (input.length == null)
                {
                    return new UnknownType(input.text + " unknown length, " + input.infoText, input.infoText);
                }
                var packedLength = (int)Math.Ceiling((JsUtils.ParseInt(input.length, 10) + 1) / 2.0);
                int decimals = input.decimals == null ? 0 : JsUtils.ParseInt(input.decimals, 10);
                return new PackedType(packedLength, decimals, extra);
            }
            case "ACCP":
                return new CharacterType(6, extra);
            case "LANG":
                return new CharacterType(1, extra);
            case "CLNT":
                return new CharacterType(3, extra);
            case "CUKY":
                return new CharacterType(5, extra);
            case "UNIT":
                return new CharacterType(3, extra);
            case "UTCL":
            case "UTCLONG":
                return new CharacterType(27, extra);
            case "NUMC":
                if (input.length == null)
                {
                    return new UnknownType(input.text + " unknown length", input.infoText);
                }
                return new NumericType(JsUtils.ParseInt(input.length, 10), input.qualifiedName);
            case "CHAR":
            case "LCHR":
                if (input.length == null)
                {
                    return new UnknownType(input.text + " unknown length", input.infoText);
                }
                return new CharacterType(JsUtils.ParseInt(input.length, 10), extra);
            case "RAW":
            case "LRAW":
                if (input.length == null)
                {
                    return new UnknownType(input.text + " unknown length", input.infoText);
                }
                return new HexType(JsUtils.ParseInt(input.length, 10), input.qualifiedName);
            case "TIMN":
            case "TIMS":
                return new TimeType(new AbstractTypeData { QualifiedName = input.qualifiedName });
            case "DECFLOAT16":
            case "DECFLOAT34":
            case "D16R":
            case "D34R":
            case "DF16_RAW":
            case "DF34_RAW":
            case "FLTP":
                if (input.length == null)
                {
                    return new UnknownType(input.text + " unknown length", input.infoText);
                }
                return new FloatingPointType(JsUtils.ParseInt(input.length, 10), input.qualifiedName);
            case "DATN":
            case "DATS":
                return new DateType(new AbstractTypeData { QualifiedName = input.qualifiedName });
            case "INT1":
            case "INT2":
            case "INT4":
                return IntegerType.Get(new AbstractTypeData { QualifiedName = input.qualifiedName });
            case "INT8":
                return new Integer8Type(new AbstractTypeData { QualifiedName = input.qualifiedName });
            case "SSTR":
            case "SSTRING":
            case "STRG":
            case "STRING":
                return StringType.Get(new AbstractTypeData { QualifiedName = input.qualifiedName });
            case "RSTR":
            case "RAWSTRING":
            case "GEOM_EWKB":
                return XStringType.Get(new AbstractTypeData { QualifiedName = input.qualifiedName });
            case "D16S":
            case "D34S":
            case "DF16_SCL":
            case "DF34_SCL":
            case "PREC":
            case "VARC":
                return new UnknownType(input.text + " is an obsolete data type", input.infoText);
            default:
                return new UnknownType(JsUtils.Str(input.text) + " unknown basic ddic type", input.infoText);
        }
    }
}
