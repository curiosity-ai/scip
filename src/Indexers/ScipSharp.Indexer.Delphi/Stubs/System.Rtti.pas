// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.Rtti;

interface

uses System.SysUtils, System.TypInfo;

type
  TMemberVisibility = (mvPrivate, mvProtected, mvPublic, mvPublished);

  TValue = record
  public
    class function From<T>(const Value: T): TValue; static;
    class function FromVariant(const Value: Variant): TValue; static;
    class property Empty: TValue read GetEmpty;
    function AsType<T>: T;
    function TryAsType<T>(out AResult: T): Boolean;
    function IsType<T>: Boolean; overload;
    function AsObject: TObject;
    function AsInteger: Integer;
    function AsInt64: Int64;
    function AsBoolean: Boolean;
    function AsExtended: Extended;
    function AsString: string;
    function AsVariant: Variant;
    function AsInterface: IInterface;
    function AsClass: TClass;
    function AsOrdinal: Int64;
    function IsEmpty: Boolean;
    function IsObject: Boolean;
    function IsArray: Boolean;
    function GetArrayLength: Integer;
    function GetArrayElement(Index: Integer): TValue;
    function ToString: string;
    property Kind: TTypeKind read GetTypeKind;
    property TypeInfo: PTypeInfo read GetTypeInfo;
  end;

  TRttiObject = class abstract
  public
    function GetAttributes: TArray<TCustomAttribute>; virtual;
    function HasAttribute<T: TCustomAttribute>: Boolean; overload;
    function GetAttribute<T: TCustomAttribute>: T; overload;
    property Handle: Pointer read GetHandle;
  end;

  TRttiNamedObject = class(TRttiObject)
  public
    property Name: string read GetName;
  end;

  TRttiType = class;

  TRttiMember = class(TRttiNamedObject)
  public
    property Parent: TRttiType read GetParent;
    property Visibility: TMemberVisibility read GetVisibility;
  end;

  TRttiField = class(TRttiMember)
  public
    function GetValue(Instance: Pointer): TValue;
    procedure SetValue(Instance: Pointer; const AValue: TValue);
    property FieldType: TRttiType read GetFieldType;
    property Offset: Integer read GetOffset;
  end;

  TRttiProperty = class(TRttiMember)
  public
    function GetValue(Instance: Pointer): TValue;
    procedure SetValue(Instance: Pointer; const AValue: TValue);
    property PropertyType: TRttiType read GetPropertyType;
    property IsReadable: Boolean read GetIsReadable;
    property IsWritable: Boolean read GetIsWritable;
  end;

  TRttiParameter = class(TRttiNamedObject)
  public
    property ParamType: TRttiType read GetParamType;
  end;

  TRttiMethod = class(TRttiMember)
  public
    function Invoke(Instance: TObject; const Args: array of TValue): TValue; overload;
    function GetParameters: TArray<TRttiParameter>; virtual; abstract;
    property ReturnType: TRttiType read GetReturnType;
    property IsConstructor: Boolean read GetIsConstructor;
    property IsDestructor: Boolean read GetIsDestructor;
    property IsClassMethod: Boolean read GetIsClassMethod;
    property IsStatic: Boolean read GetIsStatic;
    property VirtualIndex: SmallInt read GetVirtualIndex;
  end;

  TRttiType = class(TRttiNamedObject)
  public
    function GetMethods: TArray<TRttiMethod>; overload; virtual;
    function GetMethod(const AName: string): TRttiMethod; virtual;
    function GetFields: TArray<TRttiField>; virtual;
    function GetField(const AName: string): TRttiField; virtual;
    function GetProperties: TArray<TRttiProperty>; virtual;
    function GetProperty(const AName: string): TRttiProperty; virtual;
    property TypeKind: TTypeKind read GetTypeKind;
    property QualifiedName: string read GetQualifiedName;
    property IsInstance: Boolean read GetIsInstance;
    property IsRecord: Boolean read GetIsRecord;
    property Handle: PTypeInfo read GetHandle;
    property TypeSize: Integer read GetTypeSize;
  end;

  TRttiInstanceType = class(TRttiType)
  public
    property MetaclassType: TClass read GetMetaclassType;
    property BaseType: TRttiInstanceType read GetBaseType;
  end;

  TRttiInterfaceType = class(TRttiType)
  public
    property GUID: TGUID read GetGUID;
  end;

  TRttiContext = record
  public
    class function Create: TRttiContext; static;
    procedure Free;
    function GetType(ATypeInfo: Pointer): TRttiType; overload;
    function GetType(AClass: TClass): TRttiType; overload;
    function GetTypes: TArray<TRttiType>;
    function FindType(const AQualifiedName: string): TRttiType;
  end;

  TVirtualInterface = class(TInterfacedObject, IInterface)
  public
    constructor Create(PIID: PTypeInfo); overload;
  end;

implementation

end.
