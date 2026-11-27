// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.DateUtils;

interface

function YearOf(const AValue: TDateTime): Word;
function MonthOf(const AValue: TDateTime): Word;
function DayOf(const AValue: TDateTime): Word;
function HourOf(const AValue: TDateTime): Word;
function MinuteOf(const AValue: TDateTime): Word;
function SecondOf(const AValue: TDateTime): Word;
function IncDay(const AValue: TDateTime; const ANumberOfDays: Integer = 1): TDateTime;
function IncHour(const AValue: TDateTime; const ANumberOfHours: Int64 = 1): TDateTime;
function IncMinute(const AValue: TDateTime; const ANumberOfMinutes: Int64 = 1): TDateTime;
function IncSecond(const AValue: TDateTime; const ANumberOfSeconds: Int64 = 1): TDateTime;
function IncMilliSecond(const AValue: TDateTime; const ANumberOfMilliSeconds: Int64 = 1): TDateTime;
function DaysBetween(const ANow, AThen: TDateTime): Integer;
function SecondsBetween(const ANow, AThen: TDateTime): Int64;
function MilliSecondsBetween(const ANow, AThen: TDateTime): Int64;
function StartOfTheDay(const AValue: TDateTime): TDateTime;
function EndOfTheDay(const AValue: TDateTime): TDateTime;
function DateOf(const AValue: TDateTime): TDateTime;
function TimeOf(const AValue: TDateTime): TDateTime;
function Today: TDateTime;
function Tomorrow: TDateTime;
function Yesterday: TDateTime;
function DateToISO8601(const ADate: TDateTime; AInputIsUTC: Boolean = True): string;
function ISO8601ToDate(const AISODate: string; AReturnUTC: Boolean = True): TDateTime;
function DateTimeToUnix(const AValue: TDateTime; AInputIsUTC: Boolean = True): Int64;
function UnixToDateTime(const AValue: Int64; AReturnUTC: Boolean = True): TDateTime;
function EncodeDateTime(const AYear, AMonth, ADay, AHour, AMinute, ASecond, AMilliSecond: Word): TDateTime;
function CompareDateTime(const A, B: TDateTime): Integer;
function SameDate(const A, B: TDateTime): Boolean;

implementation

end.
