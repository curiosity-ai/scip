// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Vcl.Graphics;

interface

uses System.Classes, System.Types, System.UITypes;

type
  TColor = System.UITypes.TColor;
  TFontStyle = System.UITypes.TFontStyle;
  TFontStyles = System.UITypes.TFontStyles;
  TPenStyle = (psSolid, psDash, psDot, psDashDot, psDashDotDot, psClear, psInsideFrame);
  TBrushStyle = (bsSolid, bsClear, bsHorizontal, bsVertical, bsFDiagonal, bsBDiagonal, bsCross, bsDiagCross);

  TGraphicsObject = class(TPersistent)
  public
    property OnChange: TNotifyEvent read FOnChange write FOnChange;
  end;

  TFont = class(TGraphicsObject)
  public
    constructor Create;
    procedure Assign(Source: TPersistent); override;
    property Color: TColor read FColor write SetColor;
    property Height: Integer read GetHeight write SetHeight;
    property Name: string read GetName write SetName;
    property Size: Integer read GetSize write SetSize;
    property Style: TFontStyles read GetStyle write SetStyle;
  end;

  TPen = class(TGraphicsObject)
  public
    property Color: TColor read GetColor write SetColor;
    property Style: TPenStyle read GetStyle write SetStyle;
    property Width: Integer read GetWidth write SetWidth;
  end;

  TBrush = class(TGraphicsObject)
  public
    property Color: TColor read GetColor write SetColor;
    property Style: TBrushStyle read GetStyle write SetStyle;
  end;

  TCanvas = class(TPersistent)
  public
    procedure Draw(X, Y: Integer; Graphic: TGraphic);
    procedure FillRect(const Rect: TRect);
    procedure LineTo(X, Y: Integer);
    procedure MoveTo(X, Y: Integer);
    procedure Rectangle(X1, Y1, X2, Y2: Integer); overload;
    procedure Ellipse(X1, Y1, X2, Y2: Integer); overload;
    procedure StretchDraw(const Rect: TRect; Graphic: TGraphic);
    function TextHeight(const Text: string): Integer;
    procedure TextOut(X, Y: Integer; const Text: string);
    function TextWidth(const Text: string): Integer;
    property Brush: TBrush read FBrush write SetBrush;
    property Font: TFont read FFont write SetFont;
    property Pen: TPen read FPen write SetPen;
  end;

  TGraphic = class(TInterfacedPersistent)
  public
    constructor Create; virtual;
    procedure LoadFromFile(const Filename: string); virtual;
    procedure SaveToFile(const Filename: string); virtual;
    procedure LoadFromStream(Stream: TStream); virtual; abstract;
    procedure SaveToStream(Stream: TStream); virtual; abstract;
    property Empty: Boolean read GetEmpty;
    property Height: Integer read GetHeight write SetHeight;
    property Width: Integer read GetWidth write SetWidth;
  end;

  TBitmap = class(TGraphic)
  public
    constructor Create; override;
    procedure SetSize(AWidth, AHeight: Integer); virtual;
    property Canvas: TCanvas read GetCanvas;
  end;

  TIcon = class(TGraphic);

  TPicture = class(TInterfacedPersistent)
  public
    procedure LoadFromFile(const Filename: string);
    procedure SaveToFile(const Filename: string);
    property Bitmap: TBitmap read GetBitmap write SetBitmap;
    property Graphic: TGraphic read FGraphic write SetGraphic;
    property Height: Integer read GetHeight;
    property Width: Integer read GetWidth;
  end;

const
  clBlack = TColor($000000);
  clMaroon = TColor($000080);
  clGreen = TColor($008000);
  clNavy = TColor($800000);
  clGray = TColor($808080);
  clSilver = TColor($C0C0C0);
  clRed = TColor($0000FF);
  clLime = TColor($00FF00);
  clYellow = TColor($00FFFF);
  clBlue = TColor($FF0000);
  clWhite = TColor($FFFFFF);
  clNone = TColor($1FFFFFFF);
  clDefault = TColor($20000000);
  clBtnFace = TColor(-16777201);
  clWindow = TColor(-16777211);
  clWindowText = TColor(-16777208);

implementation

end.
