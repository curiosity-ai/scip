// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.Types;

interface

type
  TStringDynArray = array of string;
  TIntegerDynArray = array of Integer;
  TByteDynArray = array of Byte;
  TDirection = (FromBeginning, FromEnd);
  TDuplicates = (dupIgnore, dupAccept, dupError);
  TValueRelationship = -1..1;

  TPoint = record
    X: Integer;
    Y: Integer;
    constructor Create(AX, AY: Integer);
    function IsZero: Boolean;
  end;
  PPoint = ^TPoint;

  TSize = record
    cx: Integer;
    cy: Integer;
    constructor Create(W, H: Integer);
    property Width: Integer read cx write cx;
    property Height: Integer read cy write cy;
  end;

  TRect = record
    Left: Integer;
    Top: Integer;
    Right: Integer;
    Bottom: Integer;
    constructor Create(ALeft, ATop, ARight, ABottom: Integer);
    function Width: Integer;
    function Height: Integer;
    function Contains(const Pt: TPoint): Boolean;
    function IsEmpty: Boolean;
    property TopLeft: TPoint read GetTopLeft;
    property BottomRight: TPoint read GetBottomRight;
  end;
  PRect = ^TRect;

  TPointF = record
    X: Single;
    Y: Single;
    constructor Create(AX, AY: Single);
  end;

  TSizeF = record
    cx: Single;
    cy: Single;
  end;

  TRectF = record
    Left: Single;
    Top: Single;
    Right: Single;
    Bottom: Single;
    constructor Create(ALeft, ATop, ARight, ABottom: Single);
    function Width: Single;
    function Height: Single;
  end;

function Point(X, Y: Integer): TPoint;
function Rect(Left, Top, Right, Bottom: Integer): TRect;
function Bounds(ALeft, ATop, AWidth, AHeight: Integer): TRect;
function PointF(X, Y: Single): TPointF;
function RectF(Left, Top, Right, Bottom: Single): TRectF;

implementation

end.
