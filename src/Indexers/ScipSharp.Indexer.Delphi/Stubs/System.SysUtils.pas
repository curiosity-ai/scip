// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.SysUtils;

interface

uses System.Types;

type
  TSysCharSet = set of AnsiChar;
  TReplaceFlags = set of (rfReplaceAll, rfIgnoreCase);
  TFormatSettings = record
    DecimalSeparator: Char;
    ThousandSeparator: Char;
    DateSeparator: Char;
    TimeSeparator: Char;
    ShortDateFormat: string;
    LongDateFormat: string;
    ShortTimeFormat: string;
    LongTimeFormat: string;
    class function Create: TFormatSettings; overload; static;
    class function Create(const LocaleName: string): TFormatSettings; overload; static;
    class function Invariant: TFormatSettings; static;
  end;

  Exception = class(TObject)
  public
    constructor Create(const Msg: string);
    constructor CreateFmt(const Msg: string; const Args: array of const);
    constructor CreateRes(Ident: Integer);
    constructor CreateHelp(const Msg: string; AHelpContext: Integer);
    function GetBaseException: Exception; virtual;
    procedure RaiseOuterException(E: Exception);
    class procedure ThrowOuterException(E: Exception);
    property BaseException: Exception read GetBaseException;
    property HelpContext: Integer read FHelpContext write FHelpContext;
    property InnerException: Exception read FInnerException;
    property Message: string read FMessage write FMessage;
    property StackTrace: string read GetStackTrace;
  end;
  ExceptClass = class of Exception;

  EAbort = class(Exception);
  EHeapException = class(Exception);
  EOutOfMemory = class(EHeapException);
  EInOutError = class(Exception);
  EExternal = class(Exception);
  EIntError = class(EExternal);
  EDivByZero = class(EIntError);
  ERangeError = class(EIntError);
  EIntOverflow = class(EIntError);
  EMathError = class(EExternal);
  EInvalidOp = class(EMathError);
  EZeroDivide = class(EMathError);
  EOverflow = class(EMathError);
  EUnderflow = class(EMathError);
  EInvalidPointer = class(EHeapException);
  EInvalidCast = class(Exception);
  EConvertError = class(Exception);
  EAccessViolation = class(EExternal);
  EPrivilege = class(EExternal);
  EStackOverflow = class(EExternal);
  EControlC = class(EExternal);
  EVariantError = class(Exception);
  EPropReadOnly = class(Exception);
  EPropWriteOnly = class(Exception);
  EAssertionFailed = class(Exception);
  EAbstractError = class(Exception);
  EIntfCastError = class(Exception);
  EInvalidContainer = class(Exception);
  EInvalidInsert = class(Exception);
  EPackageError = class(Exception);
  EOSError = class(Exception)
  public
    ErrorCode: Cardinal;
  end;
  ESafecallException = class(Exception);
  EMonitor = class(Exception);
  EMonitorLockException = class(EMonitor);
  ENoMonitorSupportException = class(EMonitor);
  EProgrammerNotFound = class(Exception);
  ENotImplemented = class(Exception);
  ENotSupportedException = class(Exception);
  EArgumentException = class(Exception);
  EArgumentOutOfRangeException = class(EArgumentException);
  EArgumentNilException = class(EArgumentException);
  EPathTooLongException = class(Exception);
  EDirectoryNotFoundException = class(Exception);
  EFileNotFoundException = class(Exception);
  EPathNotFoundException = class(Exception);
  EListError = class(Exception);
  EInvalidOpException = class(Exception);
  ENoConstructException = class(Exception);
  EOperationCancelled = class(Exception);

  TEncoding = class
  public
    function GetBytes(const S: string): TBytes; overload;
    function GetString(const Bytes: TBytes): string; overload;
    function GetPreamble: TBytes; virtual; abstract;
    class function GetEncoding(CodePage: Integer): TEncoding; overload; static;
    class property ANSI: TEncoding read GetANSI;
    class property ASCII: TEncoding read GetASCII;
    class property BigEndianUnicode: TEncoding read GetBigEndianUnicode;
    class property Default: TEncoding read GetDefault;
    class property Unicode: TEncoding read GetUnicode;
    class property UTF7: TEncoding read GetUTF7;
    class property UTF8: TEncoding read GetUTF8;
    property CodePage: Cardinal read FCodePage;
    property EncodingName: string read GetEncodingName;
  end;

  TStringBuilder = class
  public
    constructor Create; overload;
    constructor Create(Capacity: Integer); overload;
    constructor Create(const Value: string); overload;
    function Append(const Value: string): TStringBuilder; overload;
    function AppendLine: TStringBuilder; overload;
    function AppendLine(const Value: string): TStringBuilder; overload;
    function AppendFormat(const Format: string; const Args: array of const): TStringBuilder;
    function Clear: TStringBuilder;
    function Insert(Index: Integer; const Value: string): TStringBuilder;
    function Remove(StartIndex, RemLength: Integer): TStringBuilder;
    function Replace(const OldValue, NewValue: string): TStringBuilder;
    function ToString: string; override;
    property Capacity: Integer read GetCapacity write SetCapacity;
    property Chars[Index: Integer]: Char read GetChars write SetChars; default;
    property Length: Integer read GetLength write SetLength;
  end;

  TStringSplitOptions = (None, ExcludeEmpty, ExcludeLastEmpty);

  TStringHelper = record helper for string
  public
    class function Create(C: Char; Count: Integer): string; static;
    class function Join(const Separator: string; const Values: array of string): string; static;
    class function IsNullOrEmpty(const Value: string): Boolean; static;
    class function IsNullOrWhiteSpace(const Value: string): Boolean; static;
    class function Parse(const Value: Integer): string; static;
    function Contains(const Value: string): Boolean;
    function StartsWith(const Value: string): Boolean;
    function EndsWith(const Value: string): Boolean;
    function IndexOf(const Value: string): Integer;
    function LastIndexOf(const Value: string): Integer;
    function IndexOfAny(const AnyOf: array of Char): Integer;
    function Insert(StartIndex: Integer; const Value: string): string;
    function IsEmpty: Boolean;
    function Remove(StartIndex: Integer): string;
    function Replace(const OldValue, NewValue: string): string;
    function Split(const Separator: array of Char): TArray<string>;
    function Substring(StartIndex: Integer): string;
    function ToBoolean: Boolean;
    function ToInteger: Integer;
    function ToInt64: Int64;
    function ToDouble: Double;
    function ToLower: string;
    function ToUpper: string;
    function ToLowerInvariant: string;
    function ToUpperInvariant: string;
    function ToCharArray: TArray<Char>;
    function Trim: string;
    function TrimLeft: string;
    function TrimRight: string;
    function PadLeft(TotalWidth: Integer): string;
    function PadRight(TotalWidth: Integer): string;
    function QuotedString: string;
    function DeQuotedString: string;
    function Equals(const Value: string): Boolean;
    function CompareTo(const strB: string): Integer;
    function CountChar(const C: Char): Integer;
    property Chars[Index: Integer]: Char read GetChars;
    property Length: Integer read GetLength;
  end;

  TIntegerHelper = record helper for Integer
  public
    function ToString: string;
    function ToHexString: string;
    function ToBoolean: Boolean;
    function ToDouble: Double;
    class function Parse(const S: string): Integer; static;
    class function TryParse(const S: string; out Value: Integer): Boolean; static;
    class function Size: Integer; static;
  end;

  TInt64Helper = record helper for Int64
  public
    function ToString: string;
  end;

  TBooleanHelper = record helper for Boolean
  public
    function ToString: string;
    function ToInteger: Integer;
  end;

  TDoubleHelper = record helper for Double
  public
    function ToString: string;
    function IsNan: Boolean;
  end;

  TGuidHelper = record helper for TGUID
  public
    function ToString: string;
    class function NewGuid: TGUID; static;
  end;

  TSearchRec = record
    Time: Integer;
    Size: Int64;
    Attr: Integer;
    Name: string;
  end;

  TProcedure = procedure;
  TFileName = string;

const
  PathDelim = '\';
  DriveDelim = ':';
  PathSep = ';';
  faReadOnly = $00000001;
  faHidden = $00000002;
  faDirectory = $00000010;
  faArchive = $00000020;
  faAnyFile = $000001FF;
  fmOpenRead = $0000;
  fmOpenWrite = $0001;
  fmOpenReadWrite = $0002;
  fmShareDenyNone = $0040;
  EmptyStr: string = '';

var
  FormatSettings: TFormatSettings;

function IntToStr(Value: Integer): string; overload;
function IntToStr(Value: Int64): string; overload;
function UIntToStr(Value: Cardinal): string;
function IntToHex(Value: Integer; Digits: Integer): string;
function StrToInt(const S: string): Integer;
function StrToIntDef(const S: string; Default: Integer): Integer;
function TryStrToInt(const S: string; out Value: Integer): Boolean;
function StrToInt64(const S: string): Int64;
function StrToInt64Def(const S: string; const Default: Int64): Int64;
function TryStrToInt64(const S: string; out Value: Int64): Boolean;
function StrToFloat(const S: string): Extended;
function StrToFloatDef(const S: string; const Default: Extended): Extended;
function TryStrToFloat(const S: string; out Value: Extended): Boolean;
function FloatToStr(Value: Extended): string;
function FormatFloat(const Format: string; Value: Extended): string;
function CurrToStr(Value: Currency): string;
function BoolToStr(B: Boolean; UseBoolStrs: Boolean = False): string;
function StrToBool(const S: string): Boolean;
function StrToBoolDef(const S: string; const Default: Boolean): Boolean;
function TryStrToBool(const S: string; out Value: Boolean): Boolean;
function Format(const Format: string; const Args: array of const): string;
function FormatDateTime(const Format: string; DateTime: TDateTime): string;
function DateToStr(const DateTime: TDateTime): string;
function TimeToStr(const DateTime: TDateTime): string;
function DateTimeToStr(const DateTime: TDateTime): string;
function StrToDate(const S: string): TDateTime;
function StrToDateTime(const S: string): TDateTime;
function TryStrToDate(const S: string; out Value: TDateTime): Boolean;
function TryStrToDateTime(const S: string; out Value: TDateTime): Boolean;
function Now: TDateTime;
function Date: TDateTime;
function Time: TDateTime;
function EncodeDate(Year, Month, Day: Word): TDateTime;
function EncodeTime(Hour, Min, Sec, MSec: Word): TDateTime;
procedure DecodeDate(const DateTime: TDateTime; var Year, Month, Day: Word);
procedure DecodeTime(const DateTime: TDateTime; var Hour, Min, Sec, MSec: Word);
function DayOfWeek(const DateTime: TDateTime): Word;
function IncMonth(const DateTime: TDateTime; NumberOfMonths: Integer = 1): TDateTime;
function IsLeapYear(Year: Word): Boolean;
function Trim(const S: string): string;
function TrimLeft(const S: string): string;
function TrimRight(const S: string): string;
function UpperCase(const S: string): string;
function LowerCase(const S: string): string;
function AnsiUpperCase(const S: string): string;
function AnsiLowerCase(const S: string): string;
function CompareStr(const S1, S2: string): Integer;
function CompareText(const S1, S2: string): Integer;
function SameStr(const S1, S2: string): Boolean;
function SameText(const S1, S2: string): Boolean;
function AnsiCompareStr(const S1, S2: string): Integer;
function AnsiCompareText(const S1, S2: string): Integer;
function AnsiSameText(const S1, S2: string): Boolean;
function AnsiPos(const Substr, S: string): Integer;
function QuotedStr(const S: string): string;
function AnsiQuotedStr(const S: string; Quote: Char): string;
function AnsiDequotedStr(const S: string; AQuote: Char): string;
function StringReplace(const Source, OldPattern, NewPattern: string; Flags: TReplaceFlags): string;
function WrapText(const Line: string; MaxCol: Integer = 45): string;
function IsDelimiter(const Delimiters, S: string; Index: Integer): Boolean;
function LastDelimiter(const Delimiters, S: string): Integer;
function StrPas(const Str: PChar): string;
function StrLen(const Str: PChar): Cardinal;
function StrPCopy(Dest: PChar; const Source: string): PChar;
function StrLComp(const Str1, Str2: PChar; MaxLen: Cardinal): Integer;
function CharInSet(C: Char; const CharSet: TSysCharSet): Boolean;
function ExtractFilePath(const FileName: string): string;
function ExtractFileDir(const FileName: string): string;
function ExtractFileName(const FileName: string): string;
function ExtractFileExt(const FileName: string): string;
function ExtractFileDrive(const FileName: string): string;
function ChangeFileExt(const FileName, Extension: string): string;
function ExpandFileName(const FileName: string): string;
function ExtractRelativePath(const BaseName, DestName: string): string;
function IncludeTrailingPathDelimiter(const S: string): string;
function ExcludeTrailingPathDelimiter(const S: string): string;
function IncludeTrailingBackslash(const S: string): string;
function ExcludeTrailingBackslash(const S: string): string;
function FileExists(const FileName: string; FollowLink: Boolean = True): Boolean;
function DirectoryExists(const Directory: string; FollowLink: Boolean = True): Boolean;
function ForceDirectories(Dir: string): Boolean;
function CreateDir(const Dir: string): Boolean;
function RemoveDir(const Dir: string): Boolean;
function DeleteFile(const FileName: string): Boolean;
function RenameFile(const OldName, NewName: string): Boolean;
function GetCurrentDir: string;
function SetCurrentDir(const Dir: string): Boolean;
function FileAge(const FileName: string): Integer;
function FileOpen(const FileName: string; Mode: LongWord): THandle;
function FileCreate(const FileName: string): THandle;
function FileRead(Handle: THandle; var Buffer; Count: LongWord): Integer;
function FileWrite(Handle: THandle; const Buffer; Count: LongWord): Integer;
function FileSeek(Handle: THandle; Offset, Origin: Integer): Integer;
procedure FileClose(Handle: THandle);
function FindFirst(const Path: string; Attr: Integer; var F: TSearchRec): Integer;
function FindNext(var F: TSearchRec): Integer;
procedure FindClose(var F: TSearchRec);
function GetEnvironmentVariable(const Name: string): string;
function SysErrorMessage(ErrorCode: Cardinal): string;
procedure RaiseLastOSError;
procedure Abort;
procedure Beep;
procedure Sleep(milliseconds: Cardinal);
procedure FreeAndNil(var Obj);
function Supports(const Instance: IInterface; const IID: TGUID; out Intf): Boolean; overload;
function Supports(const Instance: TObject; const IID: TGUID; out Intf): Boolean; overload;
function Supports(const Instance: IInterface; const IID: TGUID): Boolean; overload;
function CreateGUID(out Guid: TGUID): HResult;
function GUIDToString(const Guid: TGUID): string;
function StringToGUID(const S: string): TGUID;
function IsEqualGUID(const Guid1, Guid2: TGUID): Boolean;
function ExceptionErrorMessage(ExceptObject: TObject; ExceptAddr: Pointer; Buffer: PChar; Size: Integer): Integer;
procedure ShowException(ExceptObject: TObject; ExceptAddr: Pointer);
function Languages: TObject;
function GetTickCount: Cardinal;
function BytesOf(const Val: string): TBytes;
function StringOf(const Bytes: TBytes): string;
function WideCharToString(Source: PWideChar): string;
function UTF8Encode(const WS: string): UTF8String;
function UTF8Decode(const S: UTF8String): string;
function UTF8ToString(const S: UTF8String): string;
function AnsiToUtf8(const S: string): UTF8String;
function Utf8ToAnsi(const S: UTF8String): string;

implementation

end.
