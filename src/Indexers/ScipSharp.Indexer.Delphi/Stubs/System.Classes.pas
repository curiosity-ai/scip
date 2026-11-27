// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.Classes;

interface

uses System.SysUtils, System.Types;

type
  TAlignment = (taLeftJustify, taRightJustify, taCenter);
  TLeftRight = taLeftJustify..taRightJustify;
  TShiftState = set of (ssShift, ssAlt, ssCtrl, ssLeft, ssRight, ssMiddle, ssDouble);
  THelpContext = -MaxLongint..MaxLongint;
  TSeekOrigin = (soBeginning, soCurrent, soEnd);
  TOperation = (opInsert, opRemove);
  TComponentState = set of (csLoading, csReading, csWriting, csDestroying, csDesigning, csAncestor, csUpdating, csFixups);
  TListNotification = (lnAdded, lnExtracted, lnDeleted);
  TStringsDefined = set of (sdDelimiter, sdQuoteChar, sdNameValueSeparator, sdLineBreak, sdStrictDelimiter);

  TNotifyEvent = procedure(Sender: TObject) of object;
  TGetStrProc = procedure(const S: string) of object;
  TThreadMethod = procedure of object;
  TThreadProcedure = reference to procedure;

  EStreamError = class(Exception);
  EFCreateError = class(EStreamError);
  EFOpenError = class(EStreamError);
  EReadError = class(EStreamError);
  EWriteError = class(EStreamError);
  EStringListError = class(Exception);
  EComponentError = class(Exception);
  EThread = class(Exception);

  TPersistent = class(TObject)
  public
    procedure Assign(Source: TPersistent); virtual;
    function GetNamePath: string; virtual;
  end;
  TPersistentClass = class of TPersistent;

  TInterfacedPersistent = class(TPersistent, IInterface)
  end;

  TList = class(TObject)
  public
    function Add(Item: Pointer): Integer;
    procedure Clear; virtual;
    procedure Delete(Index: Integer);
    procedure Exchange(Index1, Index2: Integer);
    function Extract(Item: Pointer): Pointer;
    function First: Pointer;
    function IndexOf(Item: Pointer): Integer;
    procedure Insert(Index: Integer; Item: Pointer);
    function Last: Pointer;
    procedure Move(CurIndex, NewIndex: Integer);
    function Remove(Item: Pointer): Integer;
    procedure Pack;
    procedure Sort(Compare: Pointer);
    property Capacity: Integer read FCapacity write SetCapacity;
    property Count: Integer read FCount write SetCount;
    property Items[Index: Integer]: Pointer read Get write Put; default;
  end;

  TThreadList = class
  public
    procedure Add(Item: Pointer);
    procedure Clear;
    function LockList: TList;
    procedure Remove(Item: Pointer);
    procedure UnlockList;
  end;

  TStream = class(TObject)
  public
    function Read(var Buffer; Count: Longint): Longint; overload; virtual;
    function Write(const Buffer; Count: Longint): Longint; overload; virtual;
    function Seek(const Offset: Int64; Origin: TSeekOrigin): Int64; overload; virtual;
    procedure ReadBuffer(var Buffer; Count: NativeInt); overload;
    procedure WriteBuffer(const Buffer; Count: NativeInt); overload;
    function CopyFrom(const Source: TStream; Count: Int64): Int64;
    property Position: Int64 read GetPosition write SetPosition;
    property Size: Int64 read GetSize write SetSize;
  end;

  THandleStream = class(TStream)
  public
    constructor Create(AHandle: THandle);
    property Handle: THandle read FHandle;
  end;

  TFileStream = class(THandleStream)
  public
    constructor Create(const AFileName: string; Mode: Word); overload;
    constructor Create(const AFileName: string; Mode: Word; Rights: Cardinal); overload;
    property FileName: string read FFileName;
  end;

  TCustomMemoryStream = class(TStream)
  public
    procedure SaveToStream(Stream: TStream); virtual;
    procedure SaveToFile(const FileName: string);
    property Memory: Pointer read FMemory;
  end;

  TMemoryStream = class(TCustomMemoryStream)
  public
    procedure Clear;
    procedure LoadFromStream(Stream: TStream);
    procedure LoadFromFile(const FileName: string);
    procedure SetSize(const NewSize: Int64); override;
  end;

  TBytesStream = class(TMemoryStream)
  public
    constructor Create(const ABytes: TBytes); overload;
    property Bytes: TBytes read FBytes;
  end;

  TStringStream = class(TBytesStream)
  public
    constructor Create; overload;
    constructor Create(const AString: string); overload;
    constructor Create(const AString: string; AEncoding: TEncoding; AOwnsEncoding: Boolean = True); overload;
    function ReadString(Count: Integer): string;
    procedure WriteString(const AString: string);
    property DataString: string read GetDataString;
    property Encoding: TEncoding read FEncoding;
  end;

  TResourceStream = class(TCustomMemoryStream)
  public
    constructor Create(Instance: THandle; const ResName: string; ResType: PChar);
  end;

  TStrings = class(TPersistent)
  public
    function Add(const S: string): Integer; virtual;
    function AddObject(const S: string; AObject: TObject): Integer; virtual;
    function AddPair(const Name, Value: string): TStrings; overload;
    procedure AddStrings(Strings: TStrings); overload; virtual;
    procedure Append(const S: string);
    procedure Assign(Source: TPersistent); override;
    procedure BeginUpdate;
    procedure Clear; virtual; abstract;
    procedure Delete(Index: Integer); virtual; abstract;
    procedure EndUpdate;
    function Equals(Strings: TStrings): Boolean; reintroduce;
    procedure Exchange(Index1, Index2: Integer); virtual;
    function IndexOf(const S: string): Integer; virtual;
    function IndexOfName(const Name: string): Integer; virtual;
    function IndexOfObject(AObject: TObject): Integer; virtual;
    procedure Insert(Index: Integer; const S: string); virtual; abstract;
    procedure LoadFromFile(const FileName: string); overload; virtual;
    procedure LoadFromFile(const FileName: string; Encoding: TEncoding); overload; virtual;
    procedure LoadFromStream(Stream: TStream); overload; virtual;
    procedure Move(CurIndex, NewIndex: Integer); virtual;
    procedure SaveToFile(const FileName: string); overload; virtual;
    procedure SaveToFile(const FileName: string; Encoding: TEncoding); overload; virtual;
    procedure SaveToStream(Stream: TStream); overload; virtual;
    function ToStringArray: TArray<string>;
    property Capacity: Integer read GetCapacity write SetCapacity;
    property CommaText: string read GetCommaText write SetCommaText;
    property Count: Integer read GetCount;
    property Delimiter: Char read GetDelimiter write SetDelimiter;
    property DelimitedText: string read GetDelimitedText write SetDelimitedText;
    property LineBreak: string read GetLineBreak write SetLineBreak;
    property Names[Index: Integer]: string read GetName;
    property Objects[Index: Integer]: TObject read GetObject write PutObject;
    property QuoteChar: Char read GetQuoteChar write SetQuoteChar;
    property Values[const Name: string]: string read GetValue write SetValue;
    property ValueFromIndex[Index: Integer]: string read GetValueFromIndex write SetValueFromIndex;
    property NameValueSeparator: Char read GetNameValueSeparator write SetNameValueSeparator;
    property StrictDelimiter: Boolean read GetStrictDelimiter write SetStrictDelimiter;
    property Strings[Index: Integer]: string read Get write Put; default;
    property Text: string read GetTextStr write SetTextStr;
    property TrailingLineBreak: Boolean read GetTrailingLineBreak write SetTrailingLineBreak;
    property WriteBOM: Boolean read FWriteBOM write FWriteBOM;
  end;

  TStringListSortCompare = function(List: TStringList; Index1, Index2: Integer): Integer;

  TStringList = class(TStrings)
  public
    constructor Create; overload;
    constructor Create(OwnsObjects: Boolean); overload;
    destructor Destroy; override;
    function Add(const S: string): Integer; override;
    procedure Clear; override;
    procedure Delete(Index: Integer); override;
    function Find(const S: string; var Index: Integer): Boolean; virtual;
    procedure Insert(Index: Integer; const S: string); override;
    procedure Sort; virtual;
    procedure CustomSort(Compare: TStringListSortCompare); virtual;
    property CaseSensitive: Boolean read FCaseSensitive write SetCaseSensitive;
    property Duplicates: TDuplicates read FDuplicates write FDuplicates;
    property OwnsObjects: Boolean read FOwnsObject write FOwnsObject;
    property Sorted: Boolean read FSorted write SetSorted;
    property OnChange: TNotifyEvent read FOnChange write FOnChange;
    property OnChanging: TNotifyEvent read FOnChanging write FOnChanging;
  end;

  TComponent = class(TPersistent, IInterface)
  public
    constructor Create(AOwner: TComponent); virtual;
    destructor Destroy; override;
    procedure BeforeDestruction; override;
    procedure DestroyComponents;
    procedure Destroying;
    function FindComponent(const AName: string): TComponent;
    procedure FreeNotification(AComponent: TComponent);
    procedure RemoveFreeNotification(AComponent: TComponent);
    procedure InsertComponent(AComponent: TComponent);
    procedure RemoveComponent(AComponent: TComponent);
    procedure Notification(AComponent: TComponent; Operation: TOperation); virtual;
    procedure Loaded; virtual;
    property ComponentCount: Integer read GetComponentCount;
    property ComponentIndex: Integer read GetComponentIndex write SetComponentIndex;
    property Components[Index: Integer]: TComponent read GetComponent;
    property ComponentState: TComponentState read FComponentState;
    property Owner: TComponent read FOwner;
    property Name: string read FName write SetName;
    property Tag: NativeInt read FTag write FTag;
  end;
  TComponentClass = class of TComponent;

  TDataModule = class(TComponent)
  public
    constructor Create(AOwner: TComponent); override;
    property OnCreate: TNotifyEvent read FOnCreate write FOnCreate;
    property OnDestroy: TNotifyEvent read FOnDestroy write FOnDestroy;
  end;

  TBasicAction = class(TComponent)
  public
    function Execute: Boolean; dynamic;
    property OnExecute: TNotifyEvent read FOnExecute write SetOnExecute;
  end;

  TCollectionItem = class(TPersistent)
  public
    constructor Create(Collection: TCollection); virtual;
    property Collection: TCollection read FCollection write SetCollection;
    property ID: Integer read FID;
    property Index: Integer read GetIndex write SetIndex;
    property DisplayName: string read GetDisplayName write SetDisplayName;
  end;
  TCollectionItemClass = class of TCollectionItem;

  TCollection = class(TPersistent)
  public
    constructor Create(ItemClass: TCollectionItemClass);
    function Add: TCollectionItem;
    procedure BeginUpdate; virtual;
    procedure Clear;
    procedure Delete(Index: Integer);
    procedure EndUpdate; virtual;
    function FindItemID(ID: Integer): TCollectionItem;
    function Insert(Index: Integer): TCollectionItem;
    property Count: Integer read GetCount;
    property ItemClass: TCollectionItemClass read FItemClass;
    property Items[Index: Integer]: TCollectionItem read GetItem write SetItem;
  end;

  TOwnedCollection = class(TCollection)
  public
    constructor Create(AOwner: TPersistent; ItemClass: TCollectionItemClass);
  end;

  TThread = class
  public
    constructor Create; overload;
    constructor Create(CreateSuspended: Boolean); overload;
    destructor Destroy; override;
    procedure AfterConstruction; override;
    procedure Start;
    procedure Terminate;
    function WaitFor: LongWord;
    class procedure Synchronize(const AThread: TThread; AMethod: TThreadMethod); overload; static;
    class procedure Synchronize(const AThread: TThread; AThreadProc: TThreadProcedure); overload; static;
    class procedure Queue(const AThread: TThread; AMethod: TThreadMethod); overload; static;
    class procedure Queue(const AThread: TThread; AThreadProc: TThreadProcedure); overload; static;
    class function CreateAnonymousThread(const ThreadProc: TProc): TThread; static;
    class procedure Sleep(Timeout: Integer); static;
    class function CurrentThread: TThread; static;
    class property Current: TThread read GetCurrentThread;
    procedure Execute; virtual; abstract;
    property FreeOnTerminate: Boolean read FFreeOnTerminate write FFreeOnTerminate;
    property Handle: THandle read FHandle;
    property Finished: Boolean read FFinished;
    property Priority: Integer read GetPriority write SetPriority;
    property Suspended: Boolean read FSuspended write SetSuspended;
    property Terminated: Boolean read FTerminated;
    property ThreadID: TThreadID read FThreadID;
    property OnTerminate: TNotifyEvent read FOnTerminate write FOnTerminate;
  end;

  TReader = class(TObject)
  end;

  TWriter = class(TObject)
  end;

  TFiler = class(TObject)
  end;

procedure RegisterClass(AClass: TPersistentClass);
procedure RegisterClasses(const AClasses: array of TPersistentClass);
procedure UnRegisterClass(AClass: TPersistentClass);
function FindClass(const ClassName: string): TPersistentClass;
function GetClass(const AClassName: string): TPersistentClass;
procedure RegisterComponents(const Page: string; const ComponentClasses: array of TComponentClass);
function Point(AX, AY: Integer): TPoint;
function Rect(ALeft, ATop, ARight, ABottom: Integer): TRect;
procedure CheckSynchronize(Timeout: Integer = 0);

implementation

end.
