// Ported from abaplint packages/core/src/abap/5_syntax/_builtin.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax;

public class IBuiltinMethod
{
    public int Counter { get; set; }
    public Dictionary<string, AbstractType>? Mandatory { get; set; }
    public Dictionary<string, AbstractType>? Optional { get; set; }
    public ABAPRelease? Release { get; set; }
    public bool? Predicate { get; set; }
    public AbstractType Return { get; set; }
    public BuiltInMethod? Cache { get; set; }
}

public class BuiltInMethod : global::ScipSharp.Indexer.Abap.FileInformation.Identifier, IMethodDefinition, IMethodParameters
{
    private readonly IBuiltinMethod method;
    private readonly int row;

    public BuiltInMethod(AbstractToken token, string filename, IBuiltinMethod method, int row)
        : base(token, filename)
    {
        this.method = method;
        this.row = row;
    }

    public List<TypedIdentifier> GetRequiredParameters()
    {
        List<TypedIdentifier> ret = new List<TypedIdentifier>();
        foreach (var i in (this.method.Mandatory ?? new Dictionary<string, AbstractType>()).Keys.ToList())
        {
            var id = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row, 1), i);
            ret.Add(new TypedIdentifier(id, BuiltIn.filename, this.method.Mandatory!.GetValueOrDefault(i)));
        }
        return ret;
    }

    public List<string> GetOptional()
    {
        throw new Exception("BuiltInMethod->Method not implemented");
    }

    public List<TypedIdentifier> GetAll()
    {
        throw new Exception("BuiltInMethod->getAll, Method not implemented");
    }

    public string? GetEventName()
    {
        return null;
    }

    public string? GetEventClass()
    {
        return null;
    }

    public List<TypedIdentifier> GetImporting()
    {
        List<TypedIdentifier> ret = new List<TypedIdentifier>();
        foreach (var i in (this.method.Mandatory ?? new Dictionary<string, AbstractType>()).Keys.ToList())
        {
            var id = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row, 1), i);
            ret.Add(new TypedIdentifier(id, BuiltIn.filename, this.method.Mandatory!.GetValueOrDefault(i)));
        }
        foreach (var i in (this.method.Optional ?? new Dictionary<string, AbstractType>()).Keys.ToList())
        {
            var id = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row, 1), i);
            ret.Add(new TypedIdentifier(id, BuiltIn.filename, this.method.Optional!.GetValueOrDefault(i)));
        }
        return ret;
    }

    public string? GetDefaultImporting()
    {
        if (this.method.Mandatory == null)
        {
            return null;
        }
        var keys = (this.method.Mandatory ?? new Dictionary<string, AbstractType>()).Keys.ToList();
        if (keys.Count == 1)
        {
            return JsUtils.ToUpper(keys.ElementAtOrDefault(0));
        }
        return null;
    }

    public List<TypedIdentifier> GetExporting()
    {
        return new List<TypedIdentifier>();
    }

    public List<string> GetRaising()
    {
        return new List<string>();
    }

    public List<TypedIdentifier> GetChanging()
    {
        return new List<TypedIdentifier>();
    }

    public TypedIdentifier? GetReturning()
    {
        var id = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row, 1), "ret");
        return new TypedIdentifier(id, BuiltIn.filename, this.method.Return);
    }

    public List<string> GetExceptions()
    {
        return new List<string>();
    }

    public Visibility GetVisibility()
    {
        return Visibility.Public;
    }

    public bool IsRedefinition()
    {
        return false;
    }

    public bool IsAbstract()
    {
        return false;
    }

    public bool IsStatic()
    {
        return false;
    }

    public bool IsEventHandler()
    {
        return false;
    }

    public IMethodParameters GetParameters()
    {
        return this;
    }

    public ExpressionNode? GetParameterDefault(string _parameter)
    {
        return null;
    }
}

public class BuiltIn
{
    public static readonly string filename = "_builtin.prog.abap";
    private static int counter = 1;
    // the caches are static, as upstream; the port can run syntax checks on several threads
    private static readonly object cacheLock = new object();
    private static readonly List<TypedIdentifier> getCache = new List<TypedIdentifier>();
    private static List<TypedIdentifier>? typesCache = null;
    private static string? extrasCacheKey = null;
    private static List<TypedIdentifier> extrasCache = new List<TypedIdentifier>();

    // todo: "pcre" vs "regex", only one of these parameters are allowed
    // todo: "pcre", only possible from 755
    public static readonly Dictionary<string, IBuiltinMethod> methods = new Dictionary<string, IBuiltinMethod> { ["ABS"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = IntegerType.Get() }, ["ACOS"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["ASIN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["ATAN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["BIT-SET"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = IntegerType.Get() }, Return = XStringType.Get(), Release = Release.v702 }, ["BOOLC"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["BOOLX"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["bool"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["bit"] = IntegerType.Get() }, Return = XStringType.Get(), Release = Release.v702 }, ["CEIL"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = IntegerType.Get() }, ["CHAR_OFF"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get(), ["add"] = IntegerType.Get() }, Optional = new Dictionary<string, AbstractType> { ["off"] = IntegerType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["CHARLEN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Return = IntegerType.Get() }, ["CMAX"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val1"] = CLikeType.Get(), ["val2"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["val3"] = CLikeType.Get(), ["val4"] = CLikeType.Get(), ["val5"] = CLikeType.Get(), ["val6"] = CLikeType.Get(), ["val7"] = CLikeType.Get(), ["val9"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["CMIN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val1"] = CLikeType.Get(), ["val2"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["val3"] = CLikeType.Get(), ["val4"] = CLikeType.Get(), ["val5"] = CLikeType.Get(), ["val6"] = CLikeType.Get(), ["val7"] = CLikeType.Get(), ["val9"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["CONCAT_LINES_OF"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["table"] = new TableType(AnyType.Get(), new ITableOptions { WithHeader = false, KeyType = TableKeyType.user }) }, Optional = new Dictionary<string, AbstractType> { ["sep"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["CONDENSE"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["del"] = CLikeType.Get(), ["from"] = CLikeType.Get(), ["to"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["CONTAINS"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["start"] = CLikeType.Get(), ["end"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = new CharacterType(1), Predicate = true, Release = Release.v702 }, ["CONTAINS_ANY_NOT_OF"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["start"] = CLikeType.Get(), ["end"] = CLikeType.Get(), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Predicate = true, Return = new CharacterType(1), Release = Release.v702 }, ["CONTAINS_ANY_OF"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["start"] = CLikeType.Get(), ["end"] = CLikeType.Get(), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = new CharacterType(1), Predicate = true, Release = Release.v702 }, ["COS"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["COSH"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["COUNT"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["COUNT_ANY_NOT_OF"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["COUNT_ANY_OF"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["DBMAXLEN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Return = IntegerType.Get() }, ["DISTANCE"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val1"] = CLikeType.Get(), ["val2"] = CLikeType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["ESCAPE"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get(), ["format"] = SimpleType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["EXP"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["FIND"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["FIND_ANY_NOT_OF"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["FIND_ANY_OF"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["FIND_END"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["FLOOR"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = IntegerType.Get() }, ["FRAC"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = IntegerType.Get() }, ["FROM_MIXED"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["case"] = new CharacterType(1), ["sep"] = IntegerType.Get(), ["min"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["INSERT"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get(), ["sub"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["off"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["IPOW"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["base"] = new NumericGenericType(), ["exp"] = new NumericGenericType() }, Return = IntegerType.Get(), Release = Release.v740sp02 }, ["LINE_EXISTS"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = AnyType.Get() }, Return = new CharacterType(1), Predicate = true, Release = Release.v740sp02 }, ["LINE_INDEX"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = AnyType.Get() }, Return = IntegerType.Get(), Release = Release.v740sp02 }, ["LINES"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new TableType(AnyType.Get(), new ITableOptions { WithHeader = false, KeyType = TableKeyType.user }) }, Return = IntegerType.Get() }, ["LOG"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["LOG10"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["MATCH"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["case"] = new CharacterType(1), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["occ"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["MATCHES"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["case"] = new CharacterType(1), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get() }, Return = new CharacterType(1), Predicate = true, Release = Release.v702 }, ["NMAX"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val1"] = SimpleType.Get(), ["val2"] = SimpleType.Get() }, Optional = new Dictionary<string, AbstractType> { ["val3"] = SimpleType.Get(), ["val4"] = SimpleType.Get(), ["val5"] = SimpleType.Get(), ["val6"] = SimpleType.Get(), ["val7"] = SimpleType.Get(), ["val8"] = SimpleType.Get(), ["val9"] = SimpleType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["NMIN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val1"] = SimpleType.Get(), ["val2"] = SimpleType.Get() }, Optional = new Dictionary<string, AbstractType> { ["val3"] = SimpleType.Get(), ["val4"] = SimpleType.Get(), ["val5"] = SimpleType.Get(), ["val6"] = SimpleType.Get(), ["val7"] = SimpleType.Get(), ["val8"] = SimpleType.Get(), ["val9"] = SimpleType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["NUMOFCHAR"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Return = IntegerType.Get() }, ["REPEAT"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get(), ["occ"] = SimpleType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["REPLACE"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get(), ["with"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["RESCALE"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Optional = new Dictionary<string, AbstractType> { ["dec"] = IntegerType.Get(), ["prec"] = IntegerType.Get(), ["mode"] = IntegerType.Get() }, Return = new FloatType(), Release = Release.v702 }, ["REVERSE"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["ROUND"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Optional = new Dictionary<string, AbstractType> { ["dec"] = IntegerType.Get(), ["prec"] = IntegerType.Get(), ["mode"] = IntegerType.Get() }, Return = IntegerType.Get(), Release = Release.v702 }, ["SEGMENT"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get(), ["index"] = IntegerType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sep"] = CLikeType.Get(), ["space"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["SHIFT_LEFT"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["places"] = IntegerType.Get(), ["circular"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["SHIFT_RIGHT"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["places"] = IntegerType.Get(), ["circular"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["SIGN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = IntegerType.Get() }, ["SIN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["SINH"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["SQRT"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["STRLEN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Return = IntegerType.Get() }, ["SUBSTRING"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["off"] = IntegerType.Get(), ["len"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["SUBSTRING_AFTER"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["SUBSTRING_BEFORE"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["SUBSTRING_FROM"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["SUBSTRING_TO"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["sub"] = CLikeType.Get(), ["regex"] = CLikeType.Get(), ["pcre"] = CLikeType.Get(), ["case"] = new CharacterType(1), ["len"] = IntegerType.Get(), ["occ"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["TAN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["TANH"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = new FloatType() }, ["TO_LOWER"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["TO_MIXED"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Optional = new Dictionary<string, AbstractType> { ["case"] = new CharacterType(1), ["sep"] = CLikeType.Get(), ["min"] = IntegerType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["TO_UPPER"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["TRANSLATE"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get(), ["from"] = CLikeType.Get(), ["to"] = CLikeType.Get() }, Return = StringType.Get(), Release = Release.v702 }, ["TRUNC"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new NumericGenericType() }, Return = IntegerType.Get() }, ["UTCLONG_ADD"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new UTCLongType() }, Optional = new Dictionary<string, AbstractType> { ["days"] = IntegerType.Get(), ["hours"] = IntegerType.Get(), ["minutes"] = new Integer8Type(), ["seconds"] = new DecFloat34Type() }, Return = new UTCLongType(), Release = Release.v754 }, ["UTCLONG_CURRENT"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Return = new UTCLongType(), Release = Release.v754 }, ["UTCLONG_DIFF"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["high"] = new UTCLongType(), ["low"] = new UTCLongType() }, Return = new DecFloat34Type(), Release = Release.v754 }, ["XSDBOOL"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = CLikeType.Get() }, Return = new CharacterType(1), Release = Release.v740sp08 }, ["XSTRLEN"] = new IBuiltinMethod { Counter = BuiltIn.counter++, Mandatory = new Dictionary<string, AbstractType> { ["val"] = new XSequenceType() }, Return = IntegerType.Get() } };

    private int row = 1;

    private static IMethodDefinition BuildDefinition(IBuiltinMethod method, string name)
    {
        if (method.Cache != null)
        {
            return method.Cache;
        }

        var token = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(1, 1), name);
        var result = new BuiltInMethod(token, BuiltIn.filename, method, method.Counter);
        method.Cache = result;
        return result;
    }

    public static IMethodDefinition? SearchBuiltin(string? name)
    {
        if (name == null)
        {
            return null;
        }

        var def = BuiltIn.methods.GetValueOrDefault(JsUtils.ToUpper(name));
        if (def == null)
        {
            return null;
        }

        return BuiltIn.BuildDefinition(def, name);
    }

    // functions called with exactly one unnamed argument, all others take named arguments like "val"
    private static readonly HashSet<string> unnamedArgument = new HashSet<string>(new List<string> { "ABS", "ACOS", "ASIN", "ATAN", "BOOLC", "CEIL", "CHARLEN", "COS", "COSH", "DBMAXLEN", "EXP", "FLOOR", "FRAC", "LINES", "LOG", "LOG10", "NUMOFCHAR", "SIGN", "SIN", "SINH", "SQRT", "STRLEN", "TAN", "TANH", "TRUNC", "XSTRLEN" });

    public static bool HasUnnamedArgument(string? name)
    {
        if (name == null)
        {
            return false;
        }
        return BuiltIn.unnamedArgument.Contains(JsUtils.ToUpper(name));
    }

    // numeric functions returning a value of the type of the argument
    private static readonly HashSet<string> argumentTypedReturn = new HashSet<string>(new List<string> { "ABS", "CEIL", "FLOOR", "FRAC", "SIGN", "TRUNC" });

    public static bool ReturnsArgumentType(string? name)
    {
        if (name == null)
        {
            return false;
        }
        return BuiltIn.argumentTypedReturn.Contains(JsUtils.ToUpper(name));
    }

    public static bool? IsPredicate(string? name)
    {
        if (name == null)
        {
            return null;
        }

        var def = BuiltIn.methods.GetValueOrDefault(JsUtils.ToUpper(name));
        if (def == null)
        {
            return null;
        }

        return def.Predicate;
    }

    public List<TypedIdentifier> GetTypes()
    {
        lock (cacheLock)
        {
            // the identifiers are identical for every object, share them to reduce memory usage
            if (BuiltIn.typesCache == null)
            {
                List<TypedIdentifier> ret = this.BuildSY();

                {
                    var id = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(1, 1), "abap_bool");
                    ret.Add(new TypedIdentifier(id, BuiltIn.filename, new CharacterType(1, new AbstractTypeData { QualifiedName = "ABAP_BOOL", DdicName = "ABAP_BOOL" })));
                }

                {
                    var id = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(1, 1), "cursor");
                    ret.Add(new TypedIdentifier(id, BuiltIn.filename, IntegerType.Get(new AbstractTypeData { QualifiedName = "CURSOR", DdicName = "CURSOR" })));
                }

                BuiltIn.typesCache = ret;
            }

            return BuiltIn.typesCache;
        }
    }

    public List<TypedIdentifier> Get(List<string> extras)
    {
        lock (cacheLock)
        {
            List<TypedIdentifier> ret = new List<TypedIdentifier>();

            if (BuiltIn.getCache.Count == 0)
            {
                BuiltIn.getCache.AddRange(this.BuildSY());

                BuiltIn.getCache.Add(this.BuildVariable("screen"));

                BuiltIn.getCache.Add(this.BuildConstant("%_ENDIAN"));
                BuiltIn.getCache.Add(this.BuildConstant("%_CHARSIZE"));

                BuiltIn.getCache.Add(this.BuildConstant("%_BACKSPACE", new CharacterType(1), "\u0008"));
                BuiltIn.getCache.Add(this.BuildConstant("%_CR_LF", new CharacterType(2), "\r\n"));
                BuiltIn.getCache.Add(this.BuildConstant("%_FORMFEED", new CharacterType(1), "\u000c"));
                BuiltIn.getCache.Add(this.BuildConstant("%_HORIZONTAL_TAB", new CharacterType(1), "\t"));
                BuiltIn.getCache.Add(this.BuildConstant("%_MAXCHAR", new CharacterType(1), "\uFFFD\uFFFD" /* Buffer.from("FDFF", "hex").toString() */));
                BuiltIn.getCache.Add(this.BuildConstant("%_MINCHAR", new CharacterType(1), "\u0000\u0000" /* Buffer.from("0000", "hex").toString() */));
                BuiltIn.getCache.Add(this.BuildConstant("%_NEWLINE", new CharacterType(1), "\n"));
                BuiltIn.getCache.Add(this.BuildConstant("%_VERTICAL_TAB", new CharacterType(1), "\u000b"));

                BuiltIn.getCache.Add(this.BuildConstant("abap_false", new CharacterType(1, new AbstractTypeData { QualifiedName = "ABAP_BOOL", DdicName = "ABAP_BOOL" }), "' '"));
                BuiltIn.getCache.Add(this.BuildConstant("abap_true", new CharacterType(1, new AbstractTypeData { QualifiedName = "ABAP_BOOL", DdicName = "ABAP_BOOL" }), "'X'"));
                BuiltIn.getCache.Add(this.BuildConstant("abap_undefined", new CharacterType(1, new AbstractTypeData { QualifiedName = "ABAP_BOOL", DdicName = "ABAP_BOOL" }), "'-'"));
                BuiltIn.getCache.Add(this.BuildConstant("abap_off", new CharacterType(1, new AbstractTypeData { QualifiedName = "ABAP_BOOL", DdicName = "ABAP_BOOL" }), "' '"));
                BuiltIn.getCache.Add(this.BuildConstant("abap_on", new CharacterType(1, new AbstractTypeData { QualifiedName = "ABAP_BOOL", DdicName = "ABAP_BOOL" }), "'X'"));

                BuiltIn.getCache.Add(this.BuildConstant("col_background", IntegerType.Get(), "0"));
                BuiltIn.getCache.Add(this.BuildConstant("col_heading", IntegerType.Get(), "1"));
                BuiltIn.getCache.Add(this.BuildConstant("col_key", IntegerType.Get(), "4"));
                BuiltIn.getCache.Add(this.BuildConstant("col_negative", IntegerType.Get(), "6"));
                BuiltIn.getCache.Add(this.BuildConstant("col_group", IntegerType.Get(), "7"));
                BuiltIn.getCache.Add(this.BuildConstant("col_normal", IntegerType.Get(), "2"));
                BuiltIn.getCache.Add(this.BuildConstant("col_positive", IntegerType.Get(), "5"));
                BuiltIn.getCache.Add(this.BuildConstant("col_total", IntegerType.Get(), "3"));

                BuiltIn.getCache.Add(this.BuildConstant("space", new CharacterType(1, new AbstractTypeData { DerivedFromConstant = true }), "' '"));
            }

            ret.AddRange(BuiltIn.getCache);

            // the extras are identical for every object, share them to reduce memory usage
            var key = string.Join("|", extras);
            if (key != BuiltIn.extrasCacheKey)
            {
                BuiltIn.extrasCacheKey = key;
                BuiltIn.extrasCache = new List<TypedIdentifier>();
                foreach (var e in extras)
                {
                    var id = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row++, 1), e);
                    BuiltIn.extrasCache.Add(new TypedIdentifier(id, BuiltIn.filename, VoidType.Get(e), new List<string> { IdentifierMeta.ReadOnly, IdentifierMeta.BuiltIn }, "'?'"));
                }
            }
            ret.AddRange(BuiltIn.extrasCache);

            return ret;
        }
    }

    /////////////////////////////
    private TypedIdentifier BuildVariable(string name)
    {
        var id = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row++, 1), name);
        return new TypedIdentifier(id, BuiltIn.filename, VoidType.Get(name), new List<string> { IdentifierMeta.BuiltIn });
    }

    private List<TypedIdentifier> BuildSY()
    {
        List<IStructureComponent> components = new List<IStructureComponent>();
        // NOTE: fields must be in correct sequence for the syntax check
        components.Add(new IStructureComponent { Name = "index", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "pagno", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "tabix", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "tfill", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "tlopc", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "tmaxl", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "toccu", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "ttabc", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "tstis", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "ttabi", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "dbcnt", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "fdpos", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "colno", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "linct", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "linno", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "linsz", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "pagct", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "macol", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "marow", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "tleng", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "sfoff", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "willi", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "lilli", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "subrc", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "fleng", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "cucol", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "curow", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "lsind", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "listi", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "stepl", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "tpagi", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "winx1", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "winy1", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "winx2", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "winy2", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "winco", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "winro", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "windi", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "srows", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "scols", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "loopc", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "folen", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "fodec", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "tzone", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "dayst", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "ftype", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "appli", Type = new HexType(2) });
        components.Add(new IStructureComponent { Name = "fdayw", Type = AnyType.Get() });
        components.Add(new IStructureComponent { Name = "ccurs", Type = new PackedType(5, 0) });
        components.Add(new IStructureComponent { Name = "ccurt", Type = new PackedType(5, 0) });
        components.Add(new IStructureComponent { Name = "debug", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "ctype", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "input", Type = new CharacterType(1, new AbstractTypeData { QualifiedName = "sy-input" }) });
        components.Add(new IStructureComponent { Name = "langu", Type = new CharacterType(1, new AbstractTypeData { QualifiedName = "sy-langu", ConversionExit = "ISOLA" }) });
        components.Add(new IStructureComponent { Name = "modno", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "batch", Type = new CharacterType(1, new AbstractTypeData { QualifiedName = "sy-batch" }) });
        components.Add(new IStructureComponent { Name = "binpt", Type = new CharacterType(1, new AbstractTypeData { QualifiedName = "sy-binpt" }) });
        components.Add(new IStructureComponent { Name = "calld", Type = new CharacterType(1, new AbstractTypeData { QualifiedName = "sy-calld" }) });
        components.Add(new IStructureComponent { Name = "dynnr", Type = new CharacterType(4, new AbstractTypeData { QualifiedName = "sy-dynnr" }) });
        components.Add(new IStructureComponent { Name = "dyngr", Type = new CharacterType(4, new AbstractTypeData { QualifiedName = "sy-dyngr" }) });
        components.Add(new IStructureComponent { Name = "newpa", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "pri40", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "rstrt", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "wtitl", Type = new CharacterType(1, new AbstractTypeData { QualifiedName = "sy-wtitl" }) });
        components.Add(new IStructureComponent { Name = "cpage", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "dbnam", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-dbnam" }) });
        components.Add(new IStructureComponent { Name = "mandt", Type = new CharacterType(3, new AbstractTypeData { QualifiedName = "sy-mandt" }) });
        components.Add(new IStructureComponent { Name = "prefx", Type = new CharacterType(3) });
        components.Add(new IStructureComponent { Name = "fmkey", Type = new CharacterType(3) });
        components.Add(new IStructureComponent { Name = "pexpi", Type = new NumericType(1) });
        components.Add(new IStructureComponent { Name = "prini", Type = new NumericType(1) });
        components.Add(new IStructureComponent { Name = "primm", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "prrel", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "playo", Type = new CharacterType(5) });
        components.Add(new IStructureComponent { Name = "prbig", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "playp", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "prnew", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "prlog", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "pdest", Type = new CharacterType(4, new AbstractTypeData { QualifiedName = "sy-pdest" }) });
        components.Add(new IStructureComponent { Name = "plist", Type = new CharacterType(12) });
        components.Add(new IStructureComponent { Name = "pauth", Type = new NumericType(2) });
        components.Add(new IStructureComponent { Name = "prdsn", Type = new CharacterType(6) });
        components.Add(new IStructureComponent { Name = "pnwpa", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "callr", Type = new CharacterType(8, new AbstractTypeData { QualifiedName = "sy-callr" }) });
        components.Add(new IStructureComponent { Name = "repi2", Type = new CharacterType(40) });
        components.Add(new IStructureComponent { Name = "rtitl", Type = new CharacterType(70) });
        components.Add(new IStructureComponent { Name = "prrec", Type = new CharacterType(12) });
        components.Add(new IStructureComponent { Name = "prtxt", Type = new CharacterType(68) });
        components.Add(new IStructureComponent { Name = "prabt", Type = new CharacterType(12) });
        components.Add(new IStructureComponent { Name = "lpass", Type = new CharacterType(4) });
        components.Add(new IStructureComponent { Name = "nrpag", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "paart", Type = new CharacterType(16) });
        components.Add(new IStructureComponent { Name = "prcop", Type = new NumericType(3) });
        components.Add(new IStructureComponent { Name = "batzs", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "bspld", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "brep4", Type = new CharacterType(4) });
        components.Add(new IStructureComponent { Name = "batzo", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "batzd", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "batzw", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "batzm", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "ctabl", Type = new CharacterType(4) });
        components.Add(new IStructureComponent { Name = "dbsys", Type = new CharacterType(10, new AbstractTypeData { QualifiedName = "sy-dbsys" }) });
        components.Add(new IStructureComponent { Name = "dcsys", Type = new CharacterType(4) });
        components.Add(new IStructureComponent { Name = "macdb", Type = new CharacterType(4) });
        components.Add(new IStructureComponent { Name = "sysid", Type = new CharacterType(8, new AbstractTypeData { QualifiedName = "sy-sysid" }) });
        components.Add(new IStructureComponent { Name = "opsys", Type = new CharacterType(10, new AbstractTypeData { QualifiedName = "sy-opsys" }) });
        components.Add(new IStructureComponent { Name = "pfkey", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-pfkey" }) });
        components.Add(new IStructureComponent { Name = "saprl", Type = new CharacterType(4, new AbstractTypeData { QualifiedName = "sy-saprl" }) });
        components.Add(new IStructureComponent { Name = "tcode", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tcode" }) });
        components.Add(new IStructureComponent { Name = "ucomm", Type = new CharacterType(70, new AbstractTypeData { QualifiedName = "sy-ucomm" }) });
        components.Add(new IStructureComponent { Name = "cfwae", Type = new CharacterType(5) });
        components.Add(new IStructureComponent { Name = "chwae", Type = new CharacterType(5) });
        components.Add(new IStructureComponent { Name = "spono", Type = new CharacterType(10, new AbstractTypeData { QualifiedName = "sy-spono" }) });
        components.Add(new IStructureComponent { Name = "sponr", Type = new NumericType(10) });
        components.Add(new IStructureComponent { Name = "waers", Type = new CharacterType(5) });
        components.Add(new IStructureComponent { Name = "cdate", Type = new DateType() });
        components.Add(new IStructureComponent { Name = "datum", Type = new DateType() });
        components.Add(new IStructureComponent { Name = "slset", Type = new CharacterType(14, new AbstractTypeData { QualifiedName = "sy-slset" }) });
        components.Add(new IStructureComponent { Name = "subty", Type = new HexType(1) });
        components.Add(new IStructureComponent { Name = "subcs", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "group", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "ffile", Type = new CharacterType(8) });
        components.Add(new IStructureComponent { Name = "uzeit", Type = new TimeType() });
        components.Add(new IStructureComponent { Name = "dsnam", Type = new CharacterType(8) });
        components.Add(new IStructureComponent { Name = "tabid", Type = new CharacterType(8) });
        components.Add(new IStructureComponent { Name = "tfdsn", Type = new CharacterType(8) });
        components.Add(new IStructureComponent { Name = "uname", Type = new CharacterType(12, new AbstractTypeData { QualifiedName = "sy-uname" }) });
        components.Add(new IStructureComponent { Name = "lstat", Type = new CharacterType(16) });
        components.Add(new IStructureComponent { Name = "abcde", Type = new CharacterType(26, new AbstractTypeData { QualifiedName = "sy-abcde" }) });
        components.Add(new IStructureComponent { Name = "marky", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "sfnam", Type = new CharacterType(30) });
        components.Add(new IStructureComponent { Name = "tname", Type = new CharacterType(30) });
        components.Add(new IStructureComponent { Name = "msgli", Type = new CharacterType(60, new AbstractTypeData { QualifiedName = "sy-msgli" }) });
        components.Add(new IStructureComponent { Name = "title", Type = new CharacterType(70, new AbstractTypeData { QualifiedName = "sy-title" }) });
        components.Add(new IStructureComponent { Name = "entry", Type = new CharacterType(72) });
        components.Add(new IStructureComponent { Name = "lisel", Type = new CharacterType(255, new AbstractTypeData { QualifiedName = "sy-lisel" }) });
        components.Add(new IStructureComponent { Name = "uline", Type = new CharacterType(255, new AbstractTypeData { QualifiedName = "sy-uline" }) });
        components.Add(new IStructureComponent { Name = "xcode", Type = new CharacterType(70) });
        components.Add(new IStructureComponent { Name = "cprog", Type = new CharacterType(40, new AbstractTypeData { QualifiedName = "sy-cprog" }) });
        components.Add(new IStructureComponent { Name = "xprog", Type = new CharacterType(40) });
        components.Add(new IStructureComponent { Name = "xform", Type = new CharacterType(30) });
        components.Add(new IStructureComponent { Name = "ldbpg", Type = new CharacterType(40, new AbstractTypeData { QualifiedName = "sy-ldbpg" }) });
        components.Add(new IStructureComponent { Name = "tvar0", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar0" }) });
        components.Add(new IStructureComponent { Name = "tvar1", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar1" }) });
        components.Add(new IStructureComponent { Name = "tvar2", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar2" }) });
        components.Add(new IStructureComponent { Name = "tvar3", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar3" }) });
        components.Add(new IStructureComponent { Name = "tvar4", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar4" }) });
        components.Add(new IStructureComponent { Name = "tvar5", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar5" }) });
        components.Add(new IStructureComponent { Name = "tvar6", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar6" }) });
        components.Add(new IStructureComponent { Name = "tvar7", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar7" }) });
        components.Add(new IStructureComponent { Name = "tvar8", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar8" }) });
        components.Add(new IStructureComponent { Name = "tvar9", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-tvar9" }) });
        components.Add(new IStructureComponent { Name = "msgid", Type = new CharacterType(20, new AbstractTypeData { QualifiedName = "sy-msgid" }) });
        components.Add(new IStructureComponent { Name = "msgty", Type = new CharacterType(1, new AbstractTypeData { QualifiedName = "sy-msgty" }) });
        components.Add(new IStructureComponent { Name = "msgno", Type = new NumericType(3, "sy-msgno") });
        components.Add(new IStructureComponent { Name = "msgv1", Type = new CharacterType(50, new AbstractTypeData { QualifiedName = "sy-msgv1" }) });
        components.Add(new IStructureComponent { Name = "msgv2", Type = new CharacterType(50, new AbstractTypeData { QualifiedName = "sy-msgv2" }) });
        components.Add(new IStructureComponent { Name = "msgv3", Type = new CharacterType(50, new AbstractTypeData { QualifiedName = "sy-msgv3" }) });
        components.Add(new IStructureComponent { Name = "msgv4", Type = new CharacterType(50, new AbstractTypeData { QualifiedName = "sy-msgv4" }) });
        components.Add(new IStructureComponent { Name = "oncom", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "vline", Type = new CharacterType(1, new AbstractTypeData { QualifiedName = "sy-vline" }) });
        components.Add(new IStructureComponent { Name = "winsl", Type = new CharacterType(79) });
        components.Add(new IStructureComponent { Name = "staco", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "staro", Type = IntegerType.Get() });
        components.Add(new IStructureComponent { Name = "datar", Type = new CharacterType(1, new AbstractTypeData { QualifiedName = "sy-datar" }) });
        components.Add(new IStructureComponent { Name = "host", Type = new CharacterType(32, new AbstractTypeData { QualifiedName = "sy-host" }) });
        components.Add(new IStructureComponent { Name = "locdb", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "locop", Type = new CharacterType(1) });
        components.Add(new IStructureComponent { Name = "datlo", Type = new DateType() });
        components.Add(new IStructureComponent { Name = "timlo", Type = new TimeType() });
        components.Add(new IStructureComponent { Name = "zonlo", Type = new CharacterType(6, new AbstractTypeData { QualifiedName = "sy-zonlo" }) });

        var type = new StructureType(components);

        var id1 = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row++, 1), "sy");
        var sy = new TypedIdentifier(id1, BuiltIn.filename, type, new List<string> { IdentifierMeta.ReadOnly, IdentifierMeta.BuiltIn });

        var id2 = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row++, 1), "syst");
        var syst = new TypedIdentifier(id2, BuiltIn.filename, type, new List<string> { IdentifierMeta.ReadOnly, IdentifierMeta.BuiltIn });

        // https://help.sap.com/doc/abapdocu_latest_index_htm/latest/en-us/abennews-610-system.html
        var id3 = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row++, 1), "sy-repid");
        var syrepid = new TypedIdentifier(id3, BuiltIn.filename, new CharacterType(40, new AbstractTypeData { QualifiedName = "sy-repid" }), new List<string> { IdentifierMeta.ReadOnly, IdentifierMeta.BuiltIn });
        var id4 = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row++, 1), "syst-repid");
        var systrepid = new TypedIdentifier(id4, BuiltIn.filename, new CharacterType(40, new AbstractTypeData { QualifiedName = "syst-repid" }), new List<string> { IdentifierMeta.ReadOnly, IdentifierMeta.BuiltIn });

        return new List<TypedIdentifier> { sy, syst, syrepid, systrepid };
    }

    private TypedIdentifier BuildConstant(string name, AbstractType? type = null, string? value = null)
    {
        var id = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(this.row++, 1), name);
        if (type == null)
        {
            type = VoidType.Get(name);
        }
        if (value == null)
        {
            value = "'?'";
        }
        return new TypedIdentifier(id, BuiltIn.filename, type, new List<string> { IdentifierMeta.ReadOnly, IdentifierMeta.BuiltIn }, value);
    }
}
