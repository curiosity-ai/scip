// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.Math;

interface

type
  TValueSign = -1..1;
  TRoundToRange = -37..37;

const
  NaN = 0.0 / 0.0;
  Infinity = 1.0 / 0.0;
  NegInfinity = -1.0 / 0.0;
  MinDouble = 4.9406564584124654418e-324;
  MaxDouble = 1.7976931348623157081e+308;

function Max(A, B: Integer): Integer; overload;
function Max(A, B: Double): Double; overload;
function Min(A, B: Integer): Integer; overload;
function Min(A, B: Double): Double; overload;
function Ceil(const X: Extended): Integer;
function Floor(const X: Extended): Integer;
function Power(const Base, Exponent: Extended): Extended;
function IntPower(const Base: Extended; const Exponent: Integer): Extended;
function Log10(const X: Extended): Extended;
function Log2(const X: Extended): Extended;
function LogN(const Base, X: Extended): Extended;
function RoundTo(const AValue: Double; const ADigit: TRoundToRange): Double;
function SimpleRoundTo(const AValue: Double; const ADigit: TRoundToRange = -2): Double;
function Sign(const AValue: Double): TValueSign;
function IsNan(const AValue: Double): Boolean;
function IsInfinite(const AValue: Double): Boolean;
function IsZero(const A: Double): Boolean;
function SameValue(const A, B: Double): Boolean;
function InRange(const AValue, AMin, AMax: Integer): Boolean;
function EnsureRange(const AValue, AMin, AMax: Integer): Integer;
function RandomRange(const AFrom, ATo: Integer): Integer;
function Sum(const Data: array of Double): Extended;
function Mean(const Data: array of Double): Extended;
function DegToRad(const Degrees: Extended): Extended;
function RadToDeg(const Radians: Extended): Extended;
function Tan(const X: Extended): Extended;
function ArcSin(const X: Extended): Extended;
function ArcCos(const X: Extended): Extended;
function ArcTan2(const Y, X: Extended): Extended;
function Hypot(const X, Y: Extended): Extended;

implementation

end.
