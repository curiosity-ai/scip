// Declaration-only stub written for scip-delphi (not Embarcadero source). Describes the well-known public surface of
// the System unit so that references resolve to stable external symbols. Intrinsic types alias themselves.
unit System;

interface

type
  Integer = Integer;
  Cardinal = Cardinal;
  ShortInt = ShortInt;
  SmallInt = SmallInt;
  LongInt = LongInt;
  Int8 = Int8;
  Int16 = Int16;
  Int32 = Int32;
  Int64 = Int64;
  UInt8 = UInt8;
  UInt16 = UInt16;
  UInt32 = UInt32;
  UInt64 = UInt64;
  Byte = Byte;
  Word = Word;
  LongWord = LongWord;
  FixedInt = FixedInt;
  FixedUInt = FixedUInt;
  NativeInt = NativeInt;
  NativeUInt = NativeUInt;
  IntPtr = IntPtr;
  UIntPtr = UIntPtr;
  Boolean = Boolean;
  ByteBool = ByteBool;
  WordBool = WordBool;
  LongBool = LongBool;
  Char = Char;
  AnsiChar = AnsiChar;
  WideChar = WideChar;
  UCS4Char = UCS4Char;
  AnsiString = AnsiString;
  WideString = WideString;
  UnicodeString = UnicodeString;
  ShortString = ShortString;
  RawByteString = RawByteString;
  UTF8String = UTF8String;
  Pointer = Pointer;
  PChar = PChar;
  PAnsiChar = PAnsiChar;
  PWideChar = PWideChar;
  PByte = PByte;
  PInteger = PInteger;
  PCardinal = PCardinal;
  PWord = PWord;
  PPointer = PPointer;
  Real = Real;
  Single = Single;
  Double = Double;
  Extended = Extended;
  Currency = Currency;
  Comp = Comp;
  Variant = Variant;
  OleVariant = OleVariant;
  Text = Text;
  TextFile = TextFile;
  HRESULT = HRESULT;
  TDateTime = Double;
  TDate = TDateTime;
  TTime = TDateTime;
  THandle = NativeUInt;
  TThreadID = Cardinal;
  PGUID = ^TGUID;
  TBytes = array of Byte;
  TArray<T> = array of T;
  TClass = class of TObject;
  PTypeInfo = Pointer;

  TGUID = record
    D1: Cardinal;
    D2: Word;
    D3: Word;
    D4: array[0..7] of Byte;
    class function Create(const S: string): TGUID; static;
    class function NewGuid: TGUID; static;
    class function Empty: TGUID; static;
    function IsEmpty: Boolean;
  end;

  TObject = class
  public
    constructor Create;
    destructor Destroy; virtual;
    procedure Free;
    procedure DisposeOf;
    class function ClassName: string;
    class function ClassNameIs(const Name: string): Boolean;
    class function ClassParent: TClass;
    class function ClassInfo: Pointer;
    function ClassType: TClass;
    class function InheritsFrom(AClass: TClass): Boolean;
    class function UnitName: string;
    class function QualifiedClassName: string;
    class function InstanceSize: Integer;
    class function NewInstance: TObject; virtual;
    procedure FreeInstance; virtual;
    function Equals(Obj: TObject): Boolean; virtual;
    function GetHashCode: Integer; virtual;
    function ToString: string; virtual;
    procedure AfterConstruction; virtual;
    procedure BeforeDestruction; virtual;
    procedure Dispatch(var Message); virtual;
    procedure DefaultHandler(var Message); virtual;
    function FieldAddress(const Name: string): Pointer;
    class function MethodAddress(const Name: string): Pointer;
    class function MethodName(Address: Pointer): string;
    function GetInterface(const IID: TGUID; out Obj): Boolean;
  end;

  IInterface = interface
    ['{00000000-0000-0000-C000-000000000046}']
    function QueryInterface(const IID: TGUID; out Obj): HRESULT; stdcall;
    function _AddRef: Integer; stdcall;
    function _Release: Integer; stdcall;
  end;

  IUnknown = IInterface;

  IInvokable = interface(IInterface)
  end;

  IDispatch = interface(IUnknown)
  end;

  IEnumerator = interface(IInterface)
    function GetCurrent: TObject;
    function MoveNext: Boolean;
    procedure Reset;
    property Current: TObject read GetCurrent;
  end;

  IEnumerable = interface(IInterface)
    function GetEnumerator: IEnumerator;
  end;

  IEnumerator<T> = interface(IEnumerator)
    function GetCurrent: T;
    property Current: T read GetCurrent;
  end;

  IEnumerable<T> = interface(IEnumerable)
    function GetEnumerator: IEnumerator<T>;
  end;

  IComparable = interface(IInterface)
    function CompareTo(Obj: TObject): Integer;
  end;

  IComparable<T> = interface(IComparable)
    function CompareTo(Value: T): Integer;
  end;

  IEquatable<T> = interface(IInterface)
    function Equals(Value: T): Boolean;
  end;

  TInterfacedObject = class(TObject, IInterface)
  protected
    FRefCount: Integer;
    function QueryInterface(const IID: TGUID; out Obj): HRESULT; stdcall;
    function _AddRef: Integer; stdcall;
    function _Release: Integer; stdcall;
  public
    procedure AfterConstruction; override;
    procedure BeforeDestruction; override;
    class function NewInstance: TObject; override;
    property RefCount: Integer read FRefCount;
  end;

  TAggregatedObject = class(TObject)
  public
    constructor Create(const Controller: IInterface);
  end;

  TContainedObject = class(TAggregatedObject, IInterface)
  end;

  TCustomAttribute = class(TObject)
  end;

  TMethod = record
    Code: Pointer;
    Data: Pointer;
  end;

  TProc = reference to procedure;
  TProc<T> = reference to procedure(Arg1: T);
  TProc<T1, T2> = reference to procedure(Arg1: T1; Arg2: T2);
  TFunc<TResult> = reference to function: TResult;
  TFunc<T, TResult> = reference to function(Arg1: T): TResult;
  TFunc<T1, T2, TResult> = reference to function(Arg1: T1; Arg2: T2): TResult;
  TPredicate<T> = reference to function(Arg1: T): Boolean;

  TVarRec = record
    VType: Byte;
  end;

  TTextLineBreakStyle = (tlbsLF, tlbsCRLF, tlbsCR);

  TMemoryManagerEx = record
  end;

const
  True = Boolean(1);
  False = Boolean(0);
  MaxInt = 2147483647;
  MaxLongint = 2147483647;
  sLineBreak = #13#10;
  CompilerVersion = 36.0;
  RTLVersion = 36.0;

var
  ExitCode: Integer;
  IsConsole: Boolean;
  IsMultiThread: Boolean;
  ReportMemoryLeaksOnShutdown: Boolean;
  DefaultTextLineBreakStyle: TTextLineBreakStyle;
  MainThreadID: TThreadID;
  Input: Text;
  Output: Text;
  ErrOutput: Text;

procedure Write;
procedure WriteLn;
procedure Read;
procedure ReadLn;
function Length(const S): Integer;
procedure SetLength(var S; NewLength: Integer);
function High(const X): Integer;
function Low(const X): Integer;
procedure Inc(var X; N: Integer = 1);
procedure Dec(var X; N: Integer = 1);
function Ord(const X): Integer;
function Chr(X: Byte): Char;
function Succ(const X): Integer;
function Pred(const X): Integer;
function Odd(X: Integer): Boolean;
function Assigned(const P): Boolean;
function Copy(const S; Index, Count: Integer): string;
procedure Delete(var S: string; Index, Count: Integer);
procedure Insert(const Source: string; var Dest: string; Index: Integer);
function Pos(const SubStr, Str: string; Offset: Integer = 1): Integer;
function Concat(const S1, S2: string): string;
function StringOfChar(Ch: Char; Count: Integer): string;
procedure New(var P);
procedure Dispose(var P);
procedure GetMem(var P: Pointer; Size: Integer);
procedure FreeMem(var P: Pointer);
procedure ReallocMem(var P: Pointer; Size: Integer);
function AllocMem(Size: NativeInt): Pointer;
procedure FillChar(var X; Count: Integer; Value: Byte);
procedure Move(const Source; var Dest; Count: NativeInt);
function SizeOf(const X): Integer;
function TypeInfo(const T): Pointer;
function TypeOf(const T): Pointer;
function Default(const T): Integer;
function IsManagedType(const T): Boolean;
function GetTypeKind(const T): Integer;
procedure Exit;
procedure Break;
procedure Continue;
procedure Halt(ExitCode: Integer = 0);
procedure RunError(ErrorCode: Byte = 0);
procedure Assert(Expr: Boolean; const Msg: string = '');
procedure Include(var S; I: Integer);
procedure Exclude(var S; I: Integer);
function Trunc(X: Extended): Int64;
function Round(X: Extended): Int64;
function Int(X: Extended): Extended;
function Frac(X: Extended): Extended;
function Abs(X: Extended): Extended;
function Sqr(X: Extended): Extended;
function Sqrt(X: Extended): Extended;
function Sin(X: Extended): Extended;
function Cos(X: Extended): Extended;
function ArcTan(X: Extended): Extended;
function Ln(X: Extended): Extended;
function Exp(X: Extended): Extended;
function Pi: Extended;
function Random(Range: Integer): Integer;
procedure Randomize;
function Hi(X: Word): Byte;
function Lo(X: Word): Byte;
function Swap(X: Word): Word;
function Ptr(Address: NativeInt): Pointer;
function Addr(var X): Pointer;
procedure Val(const S: string; var V; var Code: Integer);
procedure Str(const X; var S: string);
procedure Initialize(var V);
procedure Finalize(var V);
function ParamStr(Index: Integer): string;
function ParamCount: Integer;
procedure UniqueString(var Str: string);
function UpCase(Ch: Char): Char;
function AtomicIncrement(var Target: Integer): Integer;
function AtomicDecrement(var Target: Integer): Integer;
function AtomicExchange(var Target: Integer; Value: Integer): Integer;
function AtomicCmpExchange(var Target: Integer; NewValue: Integer; Comparand: Integer): Integer;
procedure AssignFile(var F; const FileName: string);
procedure CloseFile(var F);
procedure Reset(var F);
procedure Rewrite(var F);
procedure Append(var F);
function Eof(var F): Boolean;
function Eoln(var F): Boolean;
function IOResult: Integer;
function GetLastError: Integer;
procedure SetLastError(ErrorCode: Integer);
function ExceptObject: TObject;
function ExceptAddr: Pointer;
procedure Error(ErrorCode: Integer);
function MulDiv(Number, Numerator, Denominator: Integer): Integer;
procedure MonitorEnter(const AObject: TObject);
procedure MonitorExit(const AObject: TObject);
function MonitorTryEnter(const AObject: TObject; Timeout: Cardinal = 0): Boolean;
function MonitorWait(const AObject: TObject; Timeout: Cardinal): Boolean;
procedure MonitorPulse(const AObject: TObject);
procedure MonitorPulseAll(const AObject: TObject);
function GetMemoryManagerState: Integer;
function YieldProcessor: Boolean;
procedure SetCodePage(var S: RawByteString; CodePage: Word; Convert: Boolean = True);
function StringCodePage(const S: RawByteString): Word;
function StringElementSize(const S: RawByteString): Word;
function StringRefCount(const S: string): Longint;
function Utf8Encode(const WS: string): RawByteString;
function UTF8ToUnicodeString(const S: RawByteString): string;
procedure SetString(var S: string; Buffer: PChar; Length: Integer);
function DynArraySize(A: Pointer): NativeInt;

implementation

end.
