// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.Generics.Collections;

interface

uses System.SysUtils, System.Generics.Defaults;

type
  TCollectionNotification = (cnAdded, cnRemoved, cnExtracted);
  TCollectionNotifyEvent<T> = procedure(Sender: TObject; const Item: T; Action: TCollectionNotification) of object;

  TPair<TKey, TValue> = record
    Key: TKey;
    Value: TValue;
    constructor Create(const AKey: TKey; const AValue: TValue);
  end;

  TArray = class
  public
    class procedure Sort<T>(var Values: array of T); overload; static;
    class procedure Sort<T>(var Values: array of T; const Comparer: IComparer<T>); overload; static;
    class function BinarySearch<T>(const Values: array of T; const Item: T; out FoundIndex: Integer): Boolean; overload; static;
    class procedure Copy<T>(const Source: array of T; var Destination: array of T; Count: NativeInt); overload; static;
    class function Concat<T>(const Args: array of TArray<T>): TArray<T>; static;
    class function IndexOf<T>(const Values: array of T; const Item: T): NativeInt; overload; static;
    class function Contains<T>(const Values: array of T; const Item: T): Boolean; overload; static;
  end;

  TEnumerator<T> = class abstract
  public
    function MoveNext: Boolean;
    property Current: T read DoGetCurrent;
  end;

  TEnumerable<T> = class abstract
  public
    function GetEnumerator: TEnumerator<T>;
    function ToArray: TArray<T>; virtual;
  end;

  TList<T> = class(TEnumerable<T>)
  public
    type
      TEnumerator = class(TEnumerator<T>)
      public
        property Current: T read GetCurrent;
        function MoveNext: Boolean;
      end;
    constructor Create; overload;
    constructor Create(const AComparer: IComparer<T>); overload;
    constructor Create(const Collection: TEnumerable<T>); overload;
    destructor Destroy; override;
    function Add(const Value: T): Integer;
    procedure AddRange(const Values: array of T); overload;
    procedure AddRange(const Collection: TEnumerable<T>); overload;
    procedure Insert(Index: Integer; const Value: T);
    procedure InsertRange(Index: Integer; const Values: array of T); overload;
    function Remove(const Value: T): Integer;
    function RemoveItem(const Value: T; Direction: TDirection): Integer;
    procedure Delete(Index: Integer);
    procedure DeleteRange(AIndex, ACount: Integer);
    function ExtractItem(const Value: T; Direction: TDirection): T;
    function Extract(const Value: T): T;
    procedure Exchange(Index1, Index2: Integer);
    procedure Move(CurIndex, NewIndex: Integer);
    function First: T;
    function Last: T;
    procedure Clear;
    function Expand: TList<T>;
    function Contains(const Value: T): Boolean;
    function IndexOf(const Value: T): Integer;
    function IndexOfItem(const Value: T; Direction: TDirection): Integer;
    function LastIndexOf(const Value: T): Integer;
    procedure Reverse;
    procedure Sort; overload;
    procedure Sort(const AComparer: IComparer<T>); overload;
    function BinarySearch(const Item: T; out Index: Integer): Boolean; overload;
    procedure TrimExcess;
    function ToArray: TArray<T>; override;
    function GetEnumerator: TEnumerator; reintroduce;
    property Capacity: Integer read GetCapacity write SetCapacity;
    property Count: Integer read FCount write SetCount;
    property Items[Index: Integer]: T read GetItem write SetItem; default;
    property List: TArray<T> read FItems;
    property OnNotify: TCollectionNotifyEvent<T> read FOnNotify write FOnNotify;
  end;

  TObjectList<T: class> = class(TList<T>)
  public
    constructor Create(AOwnsObjects: Boolean = True); overload;
    constructor Create(const AComparer: IComparer<T>; AOwnsObjects: Boolean = True); overload;
    property OwnsObjects: Boolean read FOwnsObjects write FOwnsObjects;
  end;

  TThreadList<T> = class
  public
    constructor Create;
    procedure Add(const Item: T);
    procedure Clear;
    function LockList: TList<T>;
    procedure Remove(const Item: T); inline;
    procedure UnlockList; inline;
  end;

  TQueue<T> = class(TEnumerable<T>)
  public
    constructor Create; overload;
    procedure Enqueue(const Value: T);
    function Dequeue: T;
    function Extract: T;
    function Peek: T;
    procedure Clear;
    procedure TrimExcess;
    property Count: Integer read GetCount;
    property Capacity: Integer read GetCapacity write SetCapacity;
  end;

  TObjectQueue<T: class> = class(TQueue<T>)
  public
    constructor Create(AOwnsObjects: Boolean = True); overload;
    procedure Dequeue; reintroduce;
    property OwnsObjects: Boolean read FOwnsObjects write FOwnsObjects;
  end;

  TStack<T> = class(TEnumerable<T>)
  public
    constructor Create; overload;
    procedure Clear;
    procedure Push(const Value: T);
    function Pop: T;
    function Peek: T;
    function Extract: T;
    procedure TrimExcess;
    property Count: Integer read GetCount;
    property Capacity: Integer read GetCapacity write SetCapacity;
  end;

  TObjectStack<T: class> = class(TStack<T>)
  public
    constructor Create(AOwnsObjects: Boolean = True); overload;
    procedure Pop; reintroduce;
    property OwnsObjects: Boolean read FOwnsObjects write FOwnsObjects;
  end;

  TDictionaryOwnerships = set of (doOwnsKeys, doOwnsValues);

  TDictionary<TKey, TValue> = class(TEnumerable<TPair<TKey, TValue>>)
  public
    type
      TKeyCollection = class(TEnumerable<TKey>)
      public
        property Count: Integer read GetCount;
        function ToArray: TArray<TKey>; override;
      end;
      TValueCollection = class(TEnumerable<TValue>)
      public
        property Count: Integer read GetCount;
        function ToArray: TArray<TValue>; override;
      end;
    constructor Create(ACapacity: Integer = 0); overload;
    constructor Create(const AComparer: IEqualityComparer<TKey>); overload;
    constructor Create(ACapacity: Integer; const AComparer: IEqualityComparer<TKey>); overload;
    destructor Destroy; override;
    procedure Add(const Key: TKey; const Value: TValue);
    procedure Remove(const Key: TKey);
    function ExtractPair(const Key: TKey): TPair<TKey, TValue>;
    procedure Clear;
    procedure TrimExcess;
    function TryGetValue(const Key: TKey; out Value: TValue): Boolean;
    procedure AddOrSetValue(const Key: TKey; const Value: TValue);
    function TryAdd(const Key: TKey; const Value: TValue): Boolean;
    function ContainsKey(const Key: TKey): Boolean;
    function ContainsValue(const Value: TValue): Boolean;
    function ToArray: TArray<TPair<TKey, TValue>>; override;
    property Items[const Key: TKey]: TValue read GetItem write SetItem; default;
    property Count: Integer read FCount;
    property Keys: TKeyCollection read GetKeys;
    property Values: TValueCollection read GetValues;
    property Capacity: Integer read GetCapacity write SetCapacity;
  end;

  TObjectDictionary<TKey, TValue> = class(TDictionary<TKey, TValue>)
  public
    constructor Create(Ownerships: TDictionaryOwnerships; ACapacity: Integer = 0); overload;
    constructor Create(Ownerships: TDictionaryOwnerships; const AComparer: IEqualityComparer<TKey>); overload;
  end;

  TThreadedQueue<T> = class
  public
    constructor Create(AQueueDepth: Integer = 10; PushTimeout: Cardinal = $FFFFFFFF; PopTimeout: Cardinal = $FFFFFFFF);
    function PopItem: T; overload;
    function PushItem(const AItem: T): Integer; overload;
    procedure DoShutDown;
    property QueueSize: Integer read FQueueSize;
    property ShutDown: Boolean read FShutDown;
  end;

implementation

end.
