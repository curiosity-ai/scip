// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Vcl.Controls;

interface

uses System.Classes, System.Types, System.UITypes, Winapi.Messages, Vcl.Graphics, Vcl.Menus;

type
  TAlign = (alNone, alTop, alBottom, alLeft, alRight, alClient, alCustom);
  TAnchorKind = (akLeft, akTop, akRight, akBottom);
  TAnchors = set of TAnchorKind;
  TMouseButton = System.UITypes.TMouseButton;
  TModalResult = System.UITypes.TModalResult;
  TCursor = System.UITypes.TCursor;
  TBorderWidth = 0..MaxInt;
  TBevelCut = (bvNone, bvLowered, bvRaised, bvSpace);
  TBorderStyle = (bsNone, bsSingle);

  TMouseEvent = procedure(Sender: TObject; Button: TMouseButton; Shift: TShiftState; X, Y: Integer) of object;
  TMouseMoveEvent = procedure(Sender: TObject; Shift: TShiftState; X, Y: Integer) of object;
  TKeyEvent = procedure(Sender: TObject; var Key: Word; Shift: TShiftState) of object;
  TKeyPressEvent = procedure(Sender: TObject; var Key: Char) of object;
  TDragDropEvent = procedure(Sender, Source: TObject; X, Y: Integer) of object;
  TDragOverEvent = procedure(Sender, Source: TObject; X, Y: Integer; State: Integer; var Accept: Boolean) of object;

  TSizeConstraints = class(TPersistent)
  public
    property MaxHeight: Integer read FMaxHeight write SetMaxHeight;
    property MaxWidth: Integer read FMaxWidth write SetMaxWidth;
    property MinHeight: Integer read FMinHeight write SetMinHeight;
    property MinWidth: Integer read FMinWidth write SetMinWidth;
  end;

  TMargins = class(TPersistent)
  public
    property Left: Integer read FLeft write SetLeft;
    property Top: Integer read FTop write SetTop;
    property Right: Integer read FRight write SetRight;
    property Bottom: Integer read FBottom write SetBottom;
  end;

  TWinControl = class;

  TControl = class(TComponent)
  public
    constructor Create(AOwner: TComponent); override;
    procedure BringToFront;
    procedure SendToBack;
    procedure Hide;
    procedure Show;
    procedure Invalidate; virtual;
    procedure Refresh;
    procedure Repaint; virtual;
    procedure Update; virtual;
    procedure SetBounds(ALeft, ATop, AWidth, AHeight: Integer); virtual;
    function ClientToScreen(const Point: TPoint): TPoint;
    function ScreenToClient(const Point: TPoint): TPoint;
    function Perform(Msg: Cardinal; WParam: NativeUInt; LParam: NativeInt): NativeInt; overload;
    property Align: TAlign read FAlign write SetAlign;
    property AlignWithMargins: Boolean read GetAlignWithMargins write SetAlignWithMargins;
    property Anchors: TAnchors read FAnchors write SetAnchors;
    property BoundsRect: TRect read GetBoundsRect write SetBoundsRect;
    property Caption: string read GetText write SetText;
    property ClientHeight: Integer read GetClientHeight write SetClientHeight;
    property ClientRect: TRect read GetClientRect;
    property ClientWidth: Integer read GetClientWidth write SetClientWidth;
    property Color: TColor read FColor write SetColor;
    property Constraints: TSizeConstraints read FConstraints write SetConstraints;
    property Cursor: TCursor read FCursor write SetCursor;
    property Enabled: Boolean read GetEnabled write SetEnabled;
    property Font: TFont read FFont write SetFont;
    property Height: Integer read FHeight write SetHeight;
    property Hint: string read FHint write FHint;
    property Left: Integer read FLeft write SetLeft;
    property Margins: TMargins read FMargins write SetMargins;
    property Parent: TWinControl read FParent write SetParent;
    property ParentFont: Boolean read FParentFont write SetParentFont;
    property PopupMenu: TPopupMenu read GetPopupMenu write SetPopupMenu;
    property ShowHint: Boolean read FShowHint write SetShowHint;
    property Text: string read GetText write SetText;
    property Top: Integer read FTop write SetTop;
    property Visible: Boolean read FVisible write SetVisible;
    property Width: Integer read FWidth write SetWidth;
    property OnClick: TNotifyEvent read FOnClick write FOnClick;
    property OnDblClick: TNotifyEvent read FOnDblClick write FOnDblClick;
    property OnMouseDown: TMouseEvent read FOnMouseDown write FOnMouseDown;
    property OnMouseMove: TMouseMoveEvent read FOnMouseMove write FOnMouseMove;
    property OnMouseUp: TMouseEvent read FOnMouseUp write FOnMouseUp;
    property OnResize: TNotifyEvent read FOnResize write FOnResize;
    property OnDragDrop: TDragDropEvent read FOnDragDrop write FOnDragDrop;
    property OnDragOver: TDragOverEvent read FOnDragOver write FOnDragOver;
  end;
  TControlClass = class of TControl;

  TWinControl = class(TControl)
  public
    function CanFocus: Boolean; dynamic;
    function Focused: Boolean; dynamic;
    procedure SetFocus; virtual;
    function HandleAllocated: Boolean;
    procedure HandleNeeded;
    procedure InsertControl(AControl: TControl);
    procedure RemoveControl(AControl: TControl);
    property ControlCount: Integer read GetControlCount;
    property Controls[Index: Integer]: TControl read GetControl;
    property Handle: THandle read GetHandle;
    property TabOrder: Integer read GetTabOrder write SetTabOrder;
    property TabStop: Boolean read FTabStop write SetTabStop;
    property OnEnter: TNotifyEvent read FOnEnter write FOnEnter;
    property OnExit: TNotifyEvent read FOnExit write FOnExit;
    property OnKeyDown: TKeyEvent read FOnKeyDown write FOnKeyDown;
    property OnKeyPress: TKeyPressEvent read FOnKeyPress write FOnKeyPress;
    property OnKeyUp: TKeyEvent read FOnKeyUp write FOnKeyUp;
  end;

  TGraphicControl = class(TControl)
  public
    property Canvas: TCanvas read FCanvas;
  end;

  TCustomControl = class(TWinControl)
  public
    property Canvas: TCanvas read FCanvas;
  end;

  TImageList = class(TComponent)
  public
    property Count: Integer read GetCount;
    property Height: Integer read FHeight write SetHeight;
    property Width: Integer read FWidth write SetWidth;
  end;

const
  crDefault = TCursor(0);
  crHourGlass = TCursor(-11);
  crHandPoint = TCursor(-21);

implementation

end.
