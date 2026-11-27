// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit FMX.Types;

interface

uses System.Classes, System.Types, System.UITypes;

type
  TAlignLayout = (None, Top, Left, Right, Bottom, MostTop, MostBottom, MostLeft, MostRight, Client, Contents, Center,
    VertCenter, HorzCenter, Horizontal, Vertical, Scale, Fit, FitLeft, FitRight);

  TFmxObject = class(TComponent)
  public
    procedure AddObject(const AObject: TFmxObject);
    procedure RemoveObject(const AObject: TFmxObject); overload;
    property Parent: TFmxObject read FParent write SetParent;
    property ChildrenCount: Integer read GetChildrenCount;
    property StyleName: string read FStyleName write SetStyleName;
    property TagString: string read FTagString write FTagString;
  end;

  TTimer = class(TFmxObject)
  public
    property Enabled: Boolean read FEnabled write SetEnabled;
    property Interval: Cardinal read FInterval write SetInterval;
    property OnTimer: TNotifyEvent read FOnTimer write SetOnTimer;
  end;

implementation

end.
