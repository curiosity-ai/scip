// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.SyncObjs;

interface

uses System.SysUtils;

type
  TWaitResult = (wrSignaled, wrTimeout, wrAbandoned, wrError, wrIOCompletion);

  TSynchroObject = class(TObject)
  public
    procedure Acquire; virtual;
    procedure Release; virtual;
    function WaitFor(Timeout: Cardinal = INFINITE): TWaitResult; overload; virtual;
  end;

  TCriticalSection = class(TSynchroObject)
  public
    constructor Create;
    procedure Enter;
    procedure Leave;
    function TryEnter: Boolean;
  end;

  TEvent = class(TSynchroObject)
  public
    constructor Create(ManualReset: Boolean = False; InitialState: Boolean = False); overload;
    procedure SetEvent;
    procedure ResetEvent;
  end;

  TSimpleEvent = class(TEvent);

  TMutex = class(TSynchroObject)
  public
    constructor Create(UseCOMWait: Boolean = False); overload;
  end;

  TSemaphore = class(TSynchroObject)
  public
    constructor Create(UseCOMWait: Boolean = False); overload;
  end;

  TLightweightMREW = record
  public
    procedure BeginRead;
    procedure EndRead;
    procedure BeginWrite;
    procedure EndWrite;
  end;

  TMonitor = record
  public
    class procedure Enter(const AObject: TObject); overload; static;
    class procedure Exit(const AObject: TObject); overload; static;
    class function Wait(const AObject: TObject; Timeout: Cardinal): Boolean; overload; static;
    class procedure Pulse(const AObject: TObject); overload; static;
    class procedure PulseAll(const AObject: TObject); overload; static;
  end;

  TInterlocked = class sealed
  public
    class function Increment(var Target: Integer): Integer; overload; static;
    class function Decrement(var Target: Integer): Integer; overload; static;
    class function Exchange(var Target: Integer; Value: Integer): Integer; overload; static;
    class function CompareExchange(var Target: Integer; Value: Integer; Comparand: Integer): Integer; overload; static;
    class function Add(var Target: Integer; Increment: Integer): Integer; overload; static;
  end;

const
  INFINITE = Cardinal($FFFFFFFF);

implementation

end.
