// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.Generics.Defaults;

interface

uses System.SysUtils;

type
  IComparer<T> = interface
    function Compare(const Left, Right: T): Integer;
  end;

  IEqualityComparer<T> = interface
    function Equals(const Left, Right: T): Boolean;
    function GetHashCode(const Value: T): Integer;
  end;

  TComparison<T> = reference to function(const Left, Right: T): Integer;
  TEqualityComparison<T> = reference to function(const Left, Right: T): Boolean;
  THasher<T> = reference to function(const Value: T): Integer;

  TComparer<T> = class(TInterfacedObject, IComparer<T>)
  public
    class function Default: IComparer<T>;
    class function Construct(const Comparison: TComparison<T>): IComparer<T>;
    function Compare(const Left, Right: T): Integer; virtual; abstract;
  end;

  TEqualityComparer<T> = class(TInterfacedObject, IEqualityComparer<T>)
  public
    class function Default: IEqualityComparer<T>; static;
    class function Construct(const EqualityComparison: TEqualityComparison<T>; const Hasher: THasher<T>): IEqualityComparer<T>;
    function Equals(const Left, Right: T): Boolean; reintroduce; overload; virtual; abstract;
    function GetHashCode(const Value: T): Integer; reintroduce; overload; virtual; abstract;
  end;

  TStringComparer = class(TComparer<string>)
  public
    class function Ordinal: TStringComparer;
  end;

  TDelegatedComparer<T> = class(TComparer<T>)
  public
    constructor Create(const AComparison: TComparison<T>);
  end;

  TSingletonImplementation = class(TObject, IInterface)
  end;

implementation

end.
