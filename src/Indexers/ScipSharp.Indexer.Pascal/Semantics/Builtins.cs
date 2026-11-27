using ScipKind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Pascal.Semantics;

/// <summary>
/// A compact description of the commonly used parts of the FPC RTL/FCL, Turbo Pascal units and the LCL, so that
/// references into units that are not part of the indexed project still resolve to stable external symbols
/// (<c>scip-pascal fpc rtl . SysUtils/IntToStr().</c>). This is knowledge written for this indexer, not copied code.
/// </summary>
internal static class Builtins
{
    public static readonly PackageInfo Rtl = new("fpc", "rtl", ".");
    public static readonly PackageInfo Lcl = new("lazarus", "lcl", ".");
    public static readonly PackageInfo Unknown = new(".", ".", ".");

    private static readonly HashSet<string> LclUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "Forms", "Controls", "Graphics", "Dialogs", "StdCtrls", "ExtCtrls", "ComCtrls", "Menus", "Buttons", "LCLType",
        "LCLIntf", "LCLProc", "Interfaces", "LResources", "ActnList", "Grids", "Spin", "MaskEdit", "CheckLst",
        "Clipbrd", "ImgList", "ExtDlgs", "EditBtn", "ButtonPanel", "LazUTF8", "LazFileUtils", "FileUtil", "LazLogger",
        "LMessages", "LCLVersion", "IntfGraphics", "GraphType", "Printers", "DBCtrls", "DBGrids", "ValEdit", "Arrow",
        "ColorBox", "Calendar", "TAGraph", "SynEdit", "LazUtils", "LazStringUtils", "LazMethodList", "LCLTranslator",
    };

    /// <summary>Units that declare nothing user code references; unresolved names are never attributed to them.</summary>
    private static readonly HashSet<string> Empty = new(StringComparer.OrdinalIgnoreCase)
    {
        "cthreads", "cmem", "heaptrc", "Interfaces", "cwstring", "lineinfo", "lnfodwrf", "ObjPas", "LResources",
    };

    public static PackageInfo PackageForUnit(string name) =>
        LclUnits.Contains(name) ? Lcl : KnownRtl.Contains(name) || Table.Contains("unit " + name, StringComparison.OrdinalIgnoreCase) ? Rtl : Unknown;

    public static bool DeclaresNothing(string unitName) => Empty.Contains(unitName);

    private static readonly HashSet<string> KnownRtl = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "ObjPas", "SysUtils", "Classes", "Math", "StrUtils", "Types", "Variants", "TypInfo", "DateUtils",
        "Process", "Contnrs", "fgl", "SyncObjs", "Crt", "Dos", "Strings", "Objects", "Graph", "Windows", "BaseUnix",
        "Unix", "UnixType", "cthreads", "cmem", "heaptrc", "cwstring", "Sockets", "fpjson", "jsonparser", "jsonscanner",
        "fpcunit", "testregistry", "consoletestrunner", "testutils", "IniFiles", "RegExpr", "Zipper", "zstream",
        "base64", "md5", "sha1", "CustApp", "fphttpclient", "opensslsockets", "httpdefs", "fphttpserver", "XMLRead",
        "XMLWrite", "DOM", "SQLDB", "DB", "BufDataset", "Generics.Collections", "Generics.Defaults", "Character",
        "WinCrt", "Printer", "Keyboard", "Video", "Mouse", "matrix", "ucomplex", "getopts", "Pipes", "streamex",
        "RtlConsts", "SysConst", "Unicode", "Utf8Process", "ctypes", "dynlibs", "Strutils", "Masks", "gvector", "gmap",
        "gset", "ghashmap", "gutil", "fpexprpars", "fpTemplate", "eventlog", "SimpleIPC", "fptimer", "Resolve", "ssockets",
        "lineinfo", "lnfodwrf", "ObjPas", "MacPas", "ISO7185", "ExtPas", "Comobj", "ActiveX", "ShellApi", "Messages",
        "Registry", "ShlObj", "MMSystem", "WinSock", "WinSock2", "TermIO", "Linux", "Errors", "Ports", "Overlay", "Printer",
    };

    private const string Table = """
        unit System
        simple Integer Cardinal ShortInt SmallInt LongInt Int64 Byte Word LongWord DWord QWord UInt64 Int8 Int16 Int32 UInt8 UInt16 UInt32
        simple Boolean ByteBool WordBool LongBool Boolean8 Boolean16 Boolean32 Boolean64
        simple Real Real48 Single Double Extended Currency Comp ValReal
        simple NativeInt NativeUInt PtrInt PtrUInt SizeInt SizeUInt ValSInt ValUInt CodePointer TThreadID THandle HRESULT
        simple Variant OleVariant TDateTime TDate TTime TextRec FileRec TypedFile TVarRec
        char Char AnsiChar WideChar UnicodeChar UCS4Char
        string String AnsiString ShortString UnicodeString WideString RawByteString UTF8String OpenString
        pchar PChar PAnsiChar PWideChar PUnicodeChar
        pointer Pointer PInteger PByte PWord PLongInt PLongWord PInt64 PQWord PDouble PSingle PBoolean PShortString PAnsiString PString PPChar PVariant PPointer PSmallInt PCardinal PPtrInt PPtrUInt PSizeInt
        file Text TextFile File
        const True False: Boolean
        const MaxInt MaxLongInt MaxSmallInt MaxShortInt MaxByte MaxWord MaxCardinal MaxUIntValue MaxSIntValue: Integer
        const LineEnding sLineBreak DirectorySeparator DriveSeparator PathSeparator AllFilesMask: String
        var Input Output ErrOutput StdOut StdErr: Text
        var ExitCode ErrorCode: Integer
        var IsConsole IsLibrary: Boolean
        var RandSeed FileMode: LongInt
        var ExitProc ErrorAddr: Pointer
        func Abs Sqr Sqrt Sin Cos ArcTan Exp Ln Frac Int Pi Random: Real
        func Trunc Round Ord Length High Low SizeOf Pos IOResult ParamCount FilePos FileSize BitSizeOf CompareByte IndexByte IndexChar: Integer
        func Chr UpCase: Char
        func Odd Eof Eoln Assigned SeekEof SeekEoln: Boolean
        func Copy Concat ParamStr LowerCase StringOfChar BinStr HexStr OctStr: String
        func Ptr Addr TypeOf TypeInfo GetMem: Pointer
        func Hi Lo: Byte
        func Succ Pred Swap Default Ofs Seg CSeg DSeg SSeg SPtr Get_Frame MemSize Align
        proc Write WriteLn Read ReadLn Reset Rewrite Append Close Assign AssignFile CloseFile Get Put Page New Dispose Pack Unpack
        proc SetLength Delete Insert Inc Dec Halt Exit Break Continue FillChar Move Val Str Randomize FreeMem ReallocMem Include Exclude
        proc Seek Erase Rename BlockRead BlockWrite Flush ChDir MkDir RmDir GetDir RunError Assert Initialize Finalize FillByte FillWord FillDWord SetString ReadStr WriteStr Truncate SetTextBuf
        proc EnterCriticalSection LeaveCriticalSection InitCriticalSection DoneCriticalSection BeginThread EndThread ThreadSwitch
        func InterlockedIncrement InterlockedDecrement InterlockedExchange InterlockedCompareExchange InterlockedExchangeAdd: LongInt
        func GetThreadID GetCurrentThreadId: TThreadID
        simple TRTLCriticalSection TClass TInterfacedClass TMethod TArray TBoundArray
        proc UniqueString SetCodePage Abort
        func StringCodePage: Word
        record TGUID
          field D1: LongWord
          field D2 D3: Word
        end
        class TObject
          ctor Create
          dtor Destroy
          proc Free AfterConstruction BeforeDestruction Dispatch DefaultHandler FreeInstance CleanupInstance
          func ClassName UnitName ToString QualifiedClassName: String
          func ClassType ClassParent: TClass
          func InheritsFrom Equals GetInterface ClassNameIs: Boolean
          func GetHashCode InstanceSize: Integer
          func NewInstance: TObject
          func ClassInfo MethodAddress FieldAddress: Pointer
          func MethodName: String
        end
        intf IInterface
          func QueryInterface _AddRef _Release: LongInt
        end
        intf IUnknown(IInterface)
        end
        intf IEnumerator(IInterface)
          func MoveNext: Boolean
          prop Current: TObject
          proc Reset
        end
        intf IEnumerable(IInterface)
          func GetEnumerator: IEnumerator
        end
        class TInterfacedObject(TObject, IInterface)
          func QueryInterface _AddRef _Release: LongInt
          prop RefCount: LongInt
        end
        class TAggregatedObject(TObject)
        end
        class TContainedObject(TAggregatedObject, IInterface)
        end

        unit SysUtils
        class Exception(TObject)
          ctor Create CreateFmt CreateRes CreateResFmt CreateHelp CreateFmtHelp
          prop Message: String
          prop HelpContext: Integer
        end
        class EConvertError EAbort EInOutError EAccessViolation EOutOfMemory EArgumentException EInvalidOperation EInvalidCast EDivByZero ERangeError EIntOverflow EMathError EZeroDivide EOverflow EUnderflow EExternal ENotImplemented EFileNotFoundException EDirectoryNotFoundException EVariantError EInvalidPointer EOSError EHeapMemoryError EIntfCastError ENoThreadSupport EPropReadOnly EPropWriteOnly ENotSupportedException EPathTooLongException EInvalidArgument EBusError EControlC EPrivilege EStackOverflow EAssertionFailed EAbstractError EFormatError EPackageError(Exception)
        end
        class EArgumentOutOfRangeException EArgumentNilException(EArgumentException)
        end
        simple TBytes TStringArray TSysCharSet TFileName TReplaceFlags TIntegerSet TSysLocale TProcedure
        record TSearchRec
          field Name: String
          field Size: Int64
          field Attr Time: LongInt
        end
        record TFormatSettings
          field DecimalSeparator ThousandSeparator DateSeparator TimeSeparator ListSeparator CurrencyDecimals: Char
          field ShortDateFormat LongDateFormat ShortTimeFormat LongTimeFormat CurrencyString: String
        end
        var FormatSettings DefaultFormatSettings: TFormatSettings
        const PathDelim DriveDelim PathSep ExtensionSeparator: Char
        const EmptyStr: String
        const faAnyFile faDirectory faReadOnly faHidden faSysFile faArchive faSymLink faVolumeId: LongInt
        const fmOpenRead fmOpenWrite fmOpenReadWrite fmShareDenyNone fmShareExclusive fmShareDenyWrite fmShareDenyRead: Word
        const rfReplaceAll rfIgnoreCase: Integer
        const MinDateTime MaxDateTime: TDateTime
        func IntToStr IntToHex FloatToStr Format Trim TrimLeft TrimRight UpperCase LowerCase QuotedStr AnsiQuotedStr AnsiDequotedStr DequotedStr: String
        func ExtractFileName ExtractFilePath ExtractFileExt ExtractFileDir ExtractFileDrive ChangeFileExt IncludeTrailingPathDelimiter ExcludeTrailingPathDelimiter IncludeTrailingBackslash ExcludeTrailingBackslash ExpandFileName ExtractRelativePath SetDirSeparators ConcatPaths: String
        func GetCurrentDir GetTempDir GetTempFileName GetAppConfigDir GetAppConfigFile GetUserDir GetEnvironmentVariable SysErrorMessage ParamStr: String
        func DateTimeToStr DateToStr TimeToStr FormatDateTime FormatFloat FloatToStrF CurrToStr BoolToStr StringReplace AnsiUpperCase AnsiLowerCase StrPas AdjustLineBreaks WrapText UIntToStr: String
        func StrToInt StrToIntDef CompareStr CompareText AnsiCompareStr AnsiCompareText LastDelimiter GetLastOSError FileGetAttr AnsiPos FileOpen FileCreate FileRead FileWrite FileSeek FileAge FindFirst FindNext ExecuteProcess: LongInt
        func StrToInt64 StrToInt64Def: Int64
        func StrToQWord StrToQWordDef: QWord
        func StrToFloat StrToFloatDef StrToCurr: Double
        func StrToBool StrToBoolDef TryStrToInt TryStrToFloat TryStrToInt64 TryStrToBool TryStrToDate TryStrToDateTime TryEncodeDate TryEncodeTime FileExists DirectoryExists ForceDirectories CreateDir RemoveDir DeleteFile RenameFile SameText SameStr IsDelimiter Supports SetCurrentDir IsLeapYear SameFileName FileIsReadOnly IsValidIdent AnsiSameText AnsiSameStr: Boolean
        func Now Date Time EncodeDate EncodeTime StrToDate StrToTime StrToDateTime IncMonth FileDateToDateTime: TDateTime
        func GetTickCount64: QWord
        func GetTickCount: LongWord
        func AllocMem: Pointer
        func StrLen: SizeInt
        func StrNew StrCopy StrPCopy StrAlloc StrLCopy StrScan StrRScan StrEnd StrUpper StrLower StrPos StrCat: PChar
        func StrComp StrIComp StrLComp StrLIComp DayOfWeek: Integer
        func Languages: TObject
        proc FreeAndNil Abort Beep Sleep DecodeDate DecodeTime RaiseLastOSError FindClose ShowException OutOfMemoryError FileClose StrDispose ReplaceDate ReplaceTime GetLocalTime
        helper TStringHelper(String)
          func StartsWith EndsWith Contains IsEmpty Equals StartsText EndsText: Boolean
          func IndexOf LastIndexOf IndexOfAny CompareTo Length CountChar ToInteger: Integer
          func Trim TrimLeft TrimRight ToUpper ToLower ToUpperInvariant ToLowerInvariant Replace Substring PadLeft PadRight Insert Remove QuotedString DeQuotedString Join Format Create: String
          func Split: TStringArray
          func ToDouble ToSingle ToExtended: Double
          func ToInt64: Int64
          func ToBoolean: Boolean
          func ToCharArray: TCharArray
          prop! Chars: Char
        end
        helper TIntegerHelper(Integer)
          func ToString ToHexString ToBinString: String
          func Parse TryParse Size: Integer
        end
        helper TBooleanHelper(Boolean)
          func ToString: String
        end
        helper TDoubleHelper(Double)
          func ToString: String
          func IsNan IsInfinity: Boolean
        end
        simple TCharArray
        func ExceptObject: TObject
        func ExceptAddr: Pointer

        unit Classes
        class TPersistent(TObject)
          proc Assign
          func GetNamePath: String
        end
        class TComponent(TPersistent)
          ctor Create
          prop Name: String
          prop Owner: TComponent
          prop Tag: PtrInt
          prop ComponentCount ComponentIndex: Integer
          prop Components: TComponent
          prop ComponentState: TComponentState
          func FindComponent: TComponent
          proc InsertComponent RemoveComponent FreeNotification RemoveFreeNotification Loaded Notification
        end
        class TStrings(TPersistent)
          func Add IndexOf IndexOfName IndexOfObject AddObject AddPair: Integer
          proc Clear Delete Insert LoadFromFile SaveToFile AddStrings AddText BeginUpdate EndUpdate Exchange Move LoadFromStream SaveToStream Append SetStrings
          prop Count Capacity: Integer
          prop! Strings: String
          prop Text CommaText DelimitedText LineBreak: String
          prop Names Values ValueFromIndex: String
          prop Objects: TObject
          prop Delimiter QuoteChar NameValueSeparator: Char
          prop StrictDelimiter TrailingLineBreak: Boolean
          func GetEnumerator: TStringsEnumerator
          func ToStringArray: TStringArray
        end
        class TStringsEnumerator(TObject)
          func MoveNext: Boolean
          prop Current: String
        end
        class TStringList(TStrings)
          prop Sorted CaseSensitive OwnsObjects: Boolean
          prop Duplicates: TDuplicates
          prop OnChange OnChanging: TNotifyEvent
          proc Sort CustomSort
          func Find: Boolean
        end
        class TList(TObject)
          func Add IndexOf Remove: Integer
          proc Clear Delete Insert Pack Sort Exchange Move Assign
          prop Count Capacity: Integer
          prop! Items: Pointer
          func First Last Extract Expand: Pointer
          prop List: Pointer
        end
        class TFPList(TObject)
          func Add IndexOf Remove: Integer
          proc Clear Delete Insert Pack Sort Exchange Move Assign
          prop Count Capacity: Integer
          prop! Items: Pointer
          func First Last Extract: Pointer
        end
        class TThreadList(TObject)
          func LockList: TList
          proc UnlockList Add Remove Clear
        end
        class TCollection(TPersistent)
          func Add Insert FindItemID: TCollectionItem
          prop Count: Integer
          prop! Items: TCollectionItem
          proc Clear BeginUpdate EndUpdate Delete
        end
        class TCollectionItem(TPersistent)
          prop Collection: TCollection
          prop Index ID: Integer
          prop DisplayName: String
        end
        class TOwnedCollection(TCollection)
        end
        class TStream(TObject)
          func Read Write Seek CopyFrom: Int64
          proc ReadBuffer WriteBuffer WriteAnsiString
          func ReadAnsiString: String
          func ReadByte: Byte
          proc WriteByte
          prop Position Size: Int64
        end
        class THandleStream(TStream)
          prop Handle: THandle
        end
        class TFileStream(THandleStream)
          ctor Create
          prop FileName: String
        end
        class TCustomMemoryStream(TStream)
          prop Memory: Pointer
          proc SaveToFile SaveToStream
        end
        class TMemoryStream(TCustomMemoryStream)
          proc Clear LoadFromFile LoadFromStream SetSize
        end
        class TBytesStream(TMemoryStream)
          prop Bytes: TBytes
        end
        class TStringStream(TMemoryStream)
          prop DataString: String
          func ReadString: String
          proc WriteString
        end
        class TThread(TObject)
          ctor Create
          proc Execute Start Terminate Resume Suspend Synchronize Queue
          func WaitFor: Integer
          prop Terminated FreeOnTerminate Suspended Finished: Boolean
          prop ThreadID: TThreadID
          prop ReturnValue: Integer
          prop Priority: TThreadPriority
          prop Handle: THandle
          prop OnTerminate: TNotifyEvent
          func CurrentThread: TThread
          func ProcessorCount: Integer
          proc Sleep SpinWait Yield
          func CheckTerminated: Boolean
        end
        class EStreamError EFCreateError EFOpenError EListError EStringListError EFilerError EReadError EWriteError EClassNotFound EThread EResNotFound EBitsError EInvalidOperation EComponentError EParserError EOutOfResources EMethodNotFound(Exception)
        end
        class TBits(TObject)
          prop Size: Integer
          prop! Bits: Boolean
          func OpenBit: Integer
        end
        class TParser(TObject)
        end
        class TReader TWriter TFiler(TObject)
        end
        simple TNotifyEvent TShiftState TDuplicates TThreadPriority TSeekOrigin TAlignment TStringsSortCompare TListSortCompare TOperation TComponentState TPersistentClass TComponentClass TThreadMethod TGetStrProc TPoint TRect TSize TSmallPoint THelpContext TCollectionItemClass TListNotification
        const soBeginning soCurrent soEnd soFromBeginning soFromCurrent soFromEnd: TSeekOrigin
        const dupIgnore dupAccept dupError: TDuplicates
        const fmCreate: Word
        const tpIdle tpLowest tpLower tpNormal tpHigher tpHighest tpTimeCritical: TThreadPriority
        const ssShift ssAlt ssCtrl ssLeft ssRight ssMiddle ssDouble ssMeta ssSuper ssHyper ssAltGr ssCaps ssNum ssScroll: Integer
        proc RegisterClass RegisterClasses UnRegisterClass
        func FindClass GetClass: TPersistentClass
        func Point Rect Bounds: TPoint
        func InitInheritedComponent: Boolean

        unit Types
        simple TPoint TRect TSize TPointF TRectF TByteDynArray TIntegerDynArray TStringDynArray TDoubleDynArray TSingleDynArray TBooleanDynArray TValueRelationship TDuplicates
        const LessThanValue EqualsValue GreaterThanValue: Integer

        unit Math
        func Power Log10 Log2 LogN Tan ArcSin ArcCos ArcTan2 Hypot DegToRad RadToDeg Mean Sum MaxValue MinValue Cot Sinh Cosh Tanh IntPower Ldexp ArcSinh ArcCosh ArcTanh Norm StdDev Variance SimpleRoundTo RoundTo FMod: Double
        func Ceil Floor Sign CompareValue RandomRange Ceil64 Floor64 MaxIntValue MinIntValue: Integer
        func IsZero IsNan IsInfinite InRange SameValue: Boolean
        func Max Min EnsureRange IfThen RandomFrom
        proc DivMod SinCos ClearExceptions SetExceptionMask SetRoundMode
        const MaxDouble MinDouble MaxSingle MinSingle MaxExtended MinExtended NaN Infinity NegInfinity: Double

        unit StrUtils
        func ReplaceStr ReplaceText LeftStr RightStr MidStr ReverseString DupeString PadLeft PadRight PadCenter AnsiReplaceStr AnsiReplaceText AnsiLeftStr AnsiRightStr AnsiMidStr ExtractWord ExtractDelimited Copy2Symb Copy2Space Copy2SymbDel Copy2SpaceDel DelSpace DelSpace1 DelChars Tab2Space IntToBin Dec2Numb AddChar AddCharR StuffString RandomFrom IfThen WrapText: String
        func StartsStr EndsStr StartsText EndsText ContainsStr ContainsText AnsiStartsStr AnsiEndsStr AnsiStartsText AnsiEndsText AnsiContainsStr AnsiContainsText MatchStr MatchText IsEmptyStr IsWordPresent: Boolean
        func PosEx RPos RPosEx NPos WordCount WordPosition IndexStr IndexText AnsiIndexStr AnsiIndexText Numb2Dec Hex2Dec: Integer
        func SplitString: TStringArray

        unit Crt
        proc ClrScr ClrEol GotoXY TextColor TextBackground Delay Sound NoSound Window HighVideo LowVideo NormVideo InsLine DelLine CursorOn CursorOff CursorBig TextMode AssignCrt
        func ReadKey: Char
        func KeyPressed: Boolean
        func WhereX WhereY: Integer
        const Black Blue Green Cyan Red Magenta Brown LightGray DarkGray LightBlue LightGreen LightCyan LightRed LightMagenta Yellow White Blink: Byte
        const BW40 CO40 BW80 CO80 Mono Font8x8: Word
        var TextAttr LastMode: Word
        var CheckBreak CheckEOF DirectVideo CheckSnow: Boolean
        var WindMin WindMax: Word

        unit Dos
        proc GetDate GetTime SetDate SetTime FindFirst FindNext FindClose Exec GetFAttr SetFAttr GetFTime SetFTime GetIntVec SetIntVec Intr MsDos SwapVectors Keep PackTime UnpackTime GetCBreak SetCBreak GetVerify SetVerify FSplit
        func DosVersion: Word
        func DiskFree DiskSize: Int64
        func FSearch FExpand GetEnv EnvStr: String
        func EnvCount DosExitCode: Integer
        var DosError: Integer
        record SearchRec
          field Name: String
          field Size Time: LongInt
          field Attr: Byte
        end
        record Registers
          field AX BX CX DX BP SI DI DS ES Flags: Word
          field AL AH BL BH CL CH DL DH: Byte
        end
        record DateTime
          field Year Month Day Hour Min Sec: Word
        end
        simple PathStr DirStr NameStr ExtStr ComStr
        const AnyFile Directory ReadOnly Hidden SysFile Archive VolumeID: Word
        const FCarry FParity FAuxiliary FZero FSign FOverflow: Word

        unit Strings
        func StrLen: SizeInt
        func StrCopy StrCat StrPCopy StrNew StrEnd StrPos StrScan StrRScan StrUpper StrLower StrMove StrLCopy StrECopy StrLCat StrAlloc: PChar
        func StrComp StrIComp StrLComp StrLIComp: Integer
        func StrPas: String
        proc StrDispose

        unit SyncObjs
        class TSynchroObject(TObject)
          proc Acquire Release
        end
        class TCriticalSection(TSynchroObject)
          proc Enter Leave
          func TryEnter: Boolean
        end
        class THandleObject(TSynchroObject)
          prop Handle: THandle
        end
        class TEventObject(THandleObject)
          proc SetEvent ResetEvent
          func WaitFor: TWaitResult
        end
        class TEvent(TEventObject)
        end
        class TSimpleEvent(TEventObject)
        end
        simple TWaitResult
        const wrSignaled wrTimeout wrAbandoned wrError: TWaitResult
        const INFINITE: Cardinal

        unit fgl
        class TFPSList(TObject)
          prop Count Capacity: Integer
          proc Clear Delete Exchange Sort Pack
        end
        class TFPGList<T>(TFPSList)
          func Add IndexOf Remove: Integer
          prop! Items: T
          func First Last Extract: T
          proc Insert Assign AddList
          func GetEnumerator: TObject
        end
        class TFPGObjectList<T>(TFPSList)
          func Add IndexOf Remove: Integer
          prop! Items: T
          func First Last Extract: T
          proc Insert Assign
          prop FreeObjects: Boolean
        end
        class TFPGInterfacedObjectList<T>(TFPSList)
          func Add IndexOf Remove: Integer
          prop! Items: T
        end
        class TFPSMap(TFPSList)
          prop Sorted Duplicates: Boolean
        end
        class TFPGMap<TKey, TData>(TFPSMap)
          func Add IndexOf IndexOfData Remove: Integer
          func Find TryGetData: Boolean
          prop Keys: TKey
          prop Data: TData
          prop! KeyData: TData
          proc InsertKey InsertKeyData AddOrSetData
        end
        class TFPGMapObject<TKey, TData>(TFPSMap)
          func Add IndexOf: Integer
          func Find: Boolean
          prop Keys: TKey
          prop Data: TData
          prop! KeyData: TData
        end

        unit Generics.Collections
        class TEnumerable<T>(TObject)
          func GetEnumerator: TObject
        end
        class TList<T>(TEnumerable)
          func Add IndexOf Remove LastIndexOf Expand: Integer
          proc Clear Delete Insert Sort Reverse AddRange Exchange Move DeleteRange Pack TrimExcess
          prop Count Capacity: Integer
          prop! Items: T
          func Contains BinarySearch: Boolean
          func First Last Extract: T
        end
        class TObjectList<T>(TList)
          prop OwnsObjects: Boolean
        end
        class TThreadList<T>(TObject)
          func LockList: TObject
          proc UnlockList Add Remove Clear
        end
        record TPair<TKey, TValue>
          field Key: TKey
          field Value: TValue
          ctor Create
        end
        class TDictionary<TKey, TValue>(TEnumerable)
          proc Add AddOrSetValue Remove Clear TrimExcess
          func TryGetValue ContainsKey ContainsValue TryAdd: Boolean
          func ExtractPair: TPair
          prop Count: Integer
          prop! Items: TValue
          prop Keys Values: TObject
        end
        class TObjectDictionary<TKey, TValue>(TDictionary)
        end
        class TQueue<T>(TEnumerable)
          proc Enqueue Clear TrimExcess
          func Dequeue Peek Extract: T
          prop Count Capacity: Integer
        end
        class TStack<T>(TEnumerable)
          proc Push Clear TrimExcess
          func Pop Peek Extract: T
          prop Count Capacity: Integer
        end
        class TObjectQueue<T>(TQueue)
        end
        class TObjectStack<T>(TStack)
        end
        class THashSet<T>(TEnumerable)
          func Add Remove Contains: Boolean
          proc Clear
          prop Count: Integer
        end
        class TArray(TObject)
          proc Sort
          func BinarySearch: Boolean
        end
        class EListError(Exception)
        end

        unit Generics.Defaults
        intf IComparer<T>(IInterface)
          func Compare: Integer
        end
        intf IEqualityComparer<T>(IInterface)
          func Equals: Boolean
          func GetHashCode: UInt32
        end
        class TComparer<T>(TInterfacedObject)
          func Compare: Integer
          func Default Construct: IComparer
        end
        class TEqualityComparer<T>(TInterfacedObject)
          func Default Construct: IEqualityComparer
        end
        simple TComparison TEqualityComparison THasher

        unit Contnrs
        class TObjectList(TList)
          prop OwnsObjects: Boolean
          prop! Items: TObject
          func Add IndexOf Remove: Integer
          func First Last Extract: TObject
        end
        class TComponentList(TObjectList)
        end
        class TClassList(TList)
        end
        class TFPObjectList(TObject)
          func Add IndexOf Remove: Integer
          prop Count Capacity: Integer
          prop! Items: TObject
          prop OwnsObjects: Boolean
          proc Clear Delete Insert Pack Sort Exchange
          func First Last Extract: TObject
        end
        class TOrderedList(TObject)
          func Count: Integer
          func AtLeast: Boolean
          func Push Pop Peek: Pointer
        end
        class TStack TQueue(TOrderedList)
        end
        class TObjectStack(TStack)
        end
        class TObjectQueue(TQueue)
        end
        class TFPHashList(TObject)
          func Add IndexOf FindIndexOf: Integer
          func Find: Pointer
          prop Count: Integer
          prop! Items: Pointer
          proc Clear Delete Remove
        end
        class TFPHashObjectList(TObject)
          func Add IndexOf FindIndexOf: Integer
          func Find: TObject
          prop Count: Integer
          prop! Items: TObject
          proc Clear Delete Remove
        end
        class TBucketList(TObject)
          func Add Remove Find: Pointer
          proc Clear
          func Exists: Boolean
        end

        unit IniFiles
        class TCustomIniFile(TObject)
          ctor Create
          func ReadString: String
          func ReadInteger: LongInt
          func ReadBool SectionExists ValueExists: Boolean
          func ReadFloat: Double
          func ReadDateTime: TDateTime
          proc WriteString WriteInteger WriteBool WriteFloat WriteDateTime ReadSection ReadSections ReadSectionValues EraseSection DeleteKey UpdateFile
          prop FileName: String
        end
        class TIniFile(TCustomIniFile)
        end
        class TMemIniFile(TIniFile)
          proc Clear GetStrings SetStrings
        end

        unit Variants
        func VarIsNull VarIsEmpty VarIsArray VarIsStr VarIsNumeric VarIsOrdinal VarIsFloat VarSameValue: Boolean
        func VarToStr VarToStrDef: String
        func VarArrayCreate VarArrayOf Null Unassigned VarAsType: Variant
        func VarType VarArrayHighBound VarArrayLowBound VarArrayDimCount: Integer
        proc VarClear

        unit TypInfo
        func GetEnumName GetStrProp GetPropValue SetToString: String
        func GetEnumValue GetOrdProp StringToSet: Integer
        func GetPropInfo GetTypeData: Pointer
        func IsPublishedProp PropIsType IsStoredProp: Boolean
        proc SetOrdProp SetStrProp SetPropValue SetEnumProp
        simple PTypeInfo TTypeKind PPropInfo PTypeData TTypeKinds

        unit DateUtils
        func YearOf MonthOf DayOf HourOf MinuteOf SecondOf MilliSecondOf DayOfTheWeek DayOfTheYear DaysBetween HoursBetween MinutesBetween SecondsBetween MilliSecondsBetween YearsBetween MonthsBetween WeeksBetween WeekOf DaysInMonth DaysInYear WeekOfTheYear CompareDate CompareDateTime CompareTime: Integer
        func IncDay IncHour IncMinute IncSecond IncMilliSecond IncYear IncWeek StartOfTheDay EndOfTheDay StartOfTheMonth EndOfTheMonth StartOfTheYear EndOfTheYear Today Yesterday Tomorrow DateOf TimeOf UnixToDateTime EncodeDateTime RecodeDate RecodeTime ISO8601ToDate: TDateTime
        func DateTimeToUnix: Int64
        func SameDate SameDateTime SameTime IsToday IsSameDay IsValidDate IsValidTime IsValidDateTime IsInLeapYear TryEncodeDateTime: Boolean
        func DateToISO8601: String
        proc DecodeDateTime DecodeDateFully
        func MilliSecondsSince: Int64

        unit Process
        class TProcess(TComponent)
          proc Execute CloseInput CloseOutput CloseStderr
          func Terminate WaitOnExit: Boolean
          prop CommandLine Executable CurrentDirectory ApplicationName: String
          prop Parameters Environment: TStrings
          prop Options ShowWindow Priority: Integer
          prop Output Input Stderr: TStream
          prop ExitStatus ExitCode ProcessID: Integer
          prop Running Active: Boolean
        end
        func RunCommand RunCommandInDir: Boolean
        const poWaitOnExit poUsePipes poNoConsole poStderrToOutPut poNewConsole poRunSuspended poDetached: Integer

        unit fpjson
        class TJSONData(TObject)
          prop AsString AsJSON: String
          prop AsInteger: Integer
          prop AsInt64: Int64
          prop AsFloat: Double
          prop AsBoolean IsNull: Boolean
          prop Count: Integer
          prop! Items: TJSONData
          prop JSONType: Integer
          func FindPath GetPath Clone: TJSONData
          func FormatJSON: String
          proc Clear
        end
        class TJSONObject(TJSONData)
          func Add IndexOfName: Integer
          proc Delete Remove
          func Find Extract: TJSONData
          func Get: Variant
          prop Strings: String
          prop Integers: Integer
          prop Int64s: Int64
          prop Booleans: Boolean
          prop Floats: Double
          prop Objects: TJSONObject
          prop Arrays: TJSONArray
          prop Elements: TJSONData
          prop Names: String
          prop Nulls: Boolean
        end
        class TJSONArray(TJSONData)
          func Add IndexOf: Integer
          proc Delete Remove Insert
          prop Strings: String
          prop Integers: Integer
          prop Booleans: Boolean
          prop Floats: Double
          prop Objects: TJSONObject
          prop Arrays: TJSONArray
        end
        class TJSONString TJSONNumber TJSONBoolean TJSONNull(TJSONData)
        end
        class TJSONIntegerNumber TJSONFloatNumber TJSONInt64Number TJSONQWordNumber(TJSONNumber)
        end
        simple TJSONType TJSONStringType TJSONFloat
        func GetJSON: TJSONData
        class EJSON(Exception)
        end

        unit jsonparser
        class TJSONParser(TObject)
          ctor Create
          func Parse: TJSONData
        end

        unit fpcunit
        class TAssert(TObject)
          proc AssertTrue AssertFalse AssertEquals AssertNull AssertNotNull AssertSame AssertNotSame Fail AssertException Check CheckEquals CheckNotEquals CheckTrue CheckFalse CheckNull CheckNotNull FailEquals FailNotEquals AssertNotEquals CheckException
        end
        class TTest(TAssert)
          func TestName TestSuiteName: String
          func CountTestCases: Integer
        end
        class TTestCase(TTest)
          ctor Create CreateWith CreateWithName
          proc SetUp TearDown SetUpOnce TearDownOnce Run RunTest
          prop TestName: String
        end
        class TTestSuite(TTest)
          ctor Create
          proc AddTest AddTestSuiteFromClass
        end
        class EAssertionFailedError(Exception)
        end
        simple TTestCaseClass

        unit testregistry
        proc RegisterTest RegisterTests RegisterTestDecorator
        func GetTestRegistry: TTestSuite

        unit consoletestrunner
        class TTestRunner(TComponent)
          proc Initialize Run
          prop Title: String
        end
        var DefaultFormat DefaultRunAllTests: Integer

        unit Objects
        object TObject
          ctor Init
          dtor Done
          proc Free
        end
        object TCollection(TObject)
          ctor Init Load
          proc Insert AtInsert AtDelete AtFree Delete DeleteAll Free FreeAll ForEach Store Error
          func At IndexOf FirstThat LastThat: Pointer
          field Count Limit Delta: Integer
          field Items: Pointer
        end
        object TSortedCollection(TCollection)
          func Compare: Integer
          func KeyOf: Pointer
          func Search: Boolean
          field Duplicates: Boolean
        end
        object TStringCollection(TSortedCollection)
        end
        object TStream(TObject)
          proc Read Write Seek Reset Flush Truncate
          func GetPos GetSize: LongInt
          field Status ErrorInfo: Integer
        end
        object TDosStream(TStream)
          ctor Init
        end
        object TBufStream(TDosStream)
        end
        object TMemoryStream(TStream)
        end
        func NewStr: PString
        proc DisposeStr Abstract RegisterType RegisterObjects
        simple PObject PCollection PStream PString TPoint TRect

        unit Graph
        proc InitGraph CloseGraph Line LineTo LineRel MoveTo MoveRel Circle Rectangle Bar Bar3D SetColor SetBkColor OutTextXY OutText PutPixel SetFillStyle FloodFill ClearDevice SetLineStyle Arc Ellipse FillEllipse SetTextStyle DetectGraph SetViewPort ClearViewPort SetTextJustify FillPoly DrawPoly PieSlice Sector SetWriteMode SetPalette SetAllPalette GetImage PutImage SetActivePage SetVisualPage RestoreCrtMode SetGraphMode
        func GetMaxX GetMaxY GraphResult GetPixel GetColor GetX GetY GetMaxColor TextWidth TextHeight GetGraphMode GetBkColor ImageSize: Integer
        func GraphErrorMsg GetDriverName GetModeName: String
        const Detect grOk VGA VGAHi SolidFill SolidLn DottedLn CenterLn DashedLn LeftText CenterText RightText TopText BottomText DefaultFont TriplexFont SmallFont SansSerifFont GothicFont HorizDir VertDir NormWidth ThickWidth: Integer

        unit Controls
        class TControl(TComponent)
          prop Caption Text Hint Name: String
          prop Left Top Width Height ClientWidth ClientHeight Tag: Integer
          prop Visible Enabled ShowHint AutoSize ParentFont ParentColor: Boolean
          prop Parent: TWinControl
          prop Align: TAlign
          prop Color: TColor
          prop Font: TFont
          prop Cursor: TCursor
          prop PopupMenu: TComponent
          prop Anchors BorderSpacing Constraints: TObject
          prop OnClick OnDblClick OnMouseDown OnMouseUp OnMouseMove OnMouseEnter OnMouseLeave OnResize OnChangeBounds OnContextPopup: TNotifyEvent
          proc Show Hide Invalidate Repaint Refresh Update BringToFront SendToBack SetBounds
          func ClientToScreen ScreenToClient: TPoint
        end
        class TWinControl(TControl)
          proc SetFocus
          func Focused CanFocus: Boolean
          prop ControlCount TabOrder: Integer
          prop Controls: TControl
          prop TabStop DoubleBuffered: Boolean
          prop Handle: THandle
          prop OnEnter OnExit OnKeyDown OnKeyPress OnKeyUp OnUTF8KeyPress: TNotifyEvent
        end
        class TCustomControl(TWinControl)
          prop Canvas: TCanvas
          prop OnPaint: TNotifyEvent
        end
        class TGraphicControl(TControl)
          prop Canvas: TCanvas
          prop OnPaint: TNotifyEvent
        end
        class TImageList(TComponent)
          prop Count: Integer
        end
        simple TAlign TCursor TModalResult TMouseButton TKeyEvent TMouseEvent TMouseMoveEvent TKeyPressEvent TCloseEvent TCloseQueryEvent TAnchors TBorderStyle TControlClass
        const mrOk mrCancel mrYes mrNo mrNone mrAbort mrRetry mrIgnore mrAll mrNoToAll mrYesToAll mrClose: TModalResult
        const alNone alTop alBottom alLeft alRight alClient alCustom: TAlign
        const crDefault crHourGlass crHandPoint crArrow crCross crIBeam crNone: TCursor
        const mbLeft mbRight mbMiddle: TMouseButton

        unit Forms
        class TScrollingWinControl(TWinControl)
        end
        class TCustomForm(TScrollingWinControl)
          proc Close Release
          func ShowModal CloseQuery: Integer
          prop ModalResult: TModalResult
          prop OnCreate OnDestroy OnShow OnHide OnClose OnCloseQuery OnActivate OnDeactivate OnPaint OnKeyDown: TNotifyEvent
          prop BorderStyle Position WindowState FormStyle BorderIcons: Integer
          prop ActiveControl: TWinControl
          prop Menu: TComponent
          prop KeyPreview: Boolean
        end
        class TForm(TCustomForm)
        end
        class TCustomFrame TFrame(TScrollingWinControl)
        end
        class TApplication(TComponent)
          proc Initialize Run CreateForm Terminate ProcessMessages MessageBox Minimize Restore BringToFront HandleException ShowException
          prop Title ExeName HelpFile: String
          prop MainForm: TForm
          prop Terminated ShowMainForm Scaled: Boolean
          prop OnIdle OnException: TNotifyEvent
        end
        class TScreen(TComponent)
          prop Width Height FormCount PixelsPerInch: Integer
          prop Cursor: TCursor
          prop ActiveForm: TForm
          prop Forms: TForm
        end
        var Application: TApplication
        var Screen: TScreen
        var RequireDerivedFormResource: Boolean
        simple TFormClass TCloseAction TFormBorderStyle TPosition TWindowState
        const caNone caHide caFree caMinimize: TCloseAction
        const bsNone bsSingle bsSizeable bsDialog bsToolWindow bsSizeToolWin: TFormBorderStyle
        const poDesigned poDefault poScreenCenter poMainFormCenter poOwnerFormCenter poDesktopCenter: TPosition
        const wsNormal wsMinimized wsMaximized wsFullScreen: TWindowState

        unit StdCtrls
        class TButtonControl(TWinControl)
          prop Checked: Boolean
        end
        class TCustomButton TButton(TButtonControl)
          prop ModalResult: TModalResult
          prop Default Cancel: Boolean
        end
        class TCustomLabel TLabel(TGraphicControl)
          prop WordWrap Transparent FocusControl: Boolean
          prop Alignment Layout: Integer
        end
        class TCustomEdit TEdit(TWinControl)
          prop ReadOnly: Boolean
          prop MaxLength SelStart SelLength: Integer
          prop SelText PasswordChar TextHint: String
          prop OnChange: TNotifyEvent
          proc SelectAll Clear ClearSelection CopyToClipboard CutToClipboard PasteFromClipboard
        end
        class TCustomMemo TMemo(TCustomEdit)
          prop Lines: TStrings
          prop ScrollBars WordWrap WantReturns WantTabs: Integer
          proc Append
        end
        class TCustomCheckBox TCheckBox(TButtonControl)
          prop State: Integer
          prop AllowGrayed: Boolean
          prop OnChange: TNotifyEvent
        end
        class TRadioButton TToggleBox(TCustomCheckBox)
        end
        class TCustomListBox TListBox(TWinControl)
          prop Items: TStrings
          prop ItemIndex Count TopIndex: Integer
          prop MultiSelect Sorted: Boolean
          prop Selected: Boolean
          prop OnSelectionChange: TNotifyEvent
          proc Clear ClearSelection
        end
        class TCustomComboBox TComboBox(TWinControl)
          prop Items: TStrings
          prop ItemIndex DropDownCount: Integer
          prop Style Sorted ReadOnly: Boolean
          prop OnChange OnSelect: TNotifyEvent
          proc Clear
        end
        class TCustomGroupBox TGroupBox(TWinControl)
        end
        class TScrollBar(TWinControl)
          prop Position Min Max LargeChange SmallChange PageSize Kind: Integer
          prop OnChange OnScroll: TNotifyEvent
        end
        class TStaticText(TCustomControl)
        end

        unit ExtCtrls
        class TCustomPanel TPanel(TCustomControl)
          prop BevelInner BevelOuter BevelWidth BorderWidth Alignment: Integer
        end
        class TCustomTimer TTimer(TComponent)
          prop Interval: Cardinal
          prop Enabled: Boolean
          prop OnTimer OnStartTimer OnStopTimer: TNotifyEvent
        end
        class TCustomImage TImage(TGraphicControl)
          prop Picture: TPicture
          prop Stretch Proportional Center Transparent: Boolean
        end
        class TShape TPaintBox TSplitter TBevel(TGraphicControl)
          prop Brush: TBrush
          prop Pen: TPen
          prop Shape: Integer
        end
        class TCustomRadioGroup TRadioGroup TCustomCheckGroup TCheckGroup(TWinControl)
          prop Items: TStrings
          prop ItemIndex Columns: Integer
          prop Checked: Boolean
          prop OnClick OnItemClick: TNotifyEvent
        end
        class TNotebook TPage TTrayIcon TIdleTimer TFlowPanel TControlBar TLabeledEdit(TWinControl)
        end

        unit ComCtrls
        class TStatusBar(TWinControl)
          prop SimpleText: String
          prop SimplePanel: Boolean
          prop Panels: TCollection
        end
        class TCustomProgressBar TProgressBar TTrackBar TUpDown(TWinControl)
          prop Position Min Max Step Frequency Increment: Integer
          prop OnChange: TNotifyEvent
          proc StepIt StepBy
        end
        class TPageControl(TWinControl)
          prop ActivePage: TTabSheet
          prop ActivePageIndex PageCount TabIndex: Integer
          prop Pages: TTabSheet
          prop OnChange: TNotifyEvent
        end
        class TTabSheet(TWinControl)
          prop PageControl: TPageControl
          prop PageIndex ImageIndex: Integer
          prop TabVisible: Boolean
        end
        class TTreeNodes(TPersistent)
          func Add AddChild AddFirst AddObject AddChildObject: TTreeNode
          proc Clear Delete BeginUpdate EndUpdate
          prop Count: Integer
          prop! Item: TTreeNode
        end
        class TTreeNode(TPersistent)
          prop Text: String
          prop Data: Pointer
          prop Parent: TTreeNode
          prop Count Level Index ImageIndex SelectedIndex: Integer
          prop Expanded Selected HasChildren: Boolean
          prop! Items: TTreeNode
          func GetFirstChild GetNext GetNextSibling GetPrev: TTreeNode
          proc Expand Collapse Delete DeleteChildren MakeVisible
        end
        class TCustomTreeView TTreeView(TCustomControl)
          prop Items: TTreeNodes
          prop Selected: TTreeNode
          prop OnChange OnClick OnDblClick: TNotifyEvent
          proc FullExpand FullCollapse
        end
        class TListItems(TPersistent)
          func Add: TListItem
          proc Clear BeginUpdate EndUpdate Delete
          prop Count: Integer
          prop! Item: TListItem
        end
        class TListItem(TPersistent)
          prop Caption: String
          prop SubItems: TStrings
          prop Data: Pointer
          prop Index ImageIndex: Integer
          prop Selected Checked: Boolean
        end
        class TCustomListView TListView(TWinControl)
          prop Items: TListItems
          prop Selected: TListItem
          prop ItemIndex: Integer
          prop Columns: TCollection
          prop ViewStyle: Integer
        end
        class TToolBar TCoolBar THeaderControl TCustomTabControl TTabControl(TWinControl)
        end
        class TToolButton(TGraphicControl)
          prop Down: Boolean
          prop ImageIndex: Integer
        end

        unit Menus
        class TMenuItem(TComponent)
          prop Caption ShortCutText: String
          prop Checked Enabled Visible RadioItem AutoCheck: Boolean
          prop OnClick: TNotifyEvent
          prop Count ImageIndex GroupIndex: Integer
          prop! Items: TMenuItem
          prop ShortCut: Integer
          proc Add Clear Delete Insert Remove Click
        end
        class TMenu(TComponent)
          prop Items: TMenuItem
          prop Images: TImageList
        end
        class TMainMenu(TMenu)
        end
        class TPopupMenu(TMenu)
          proc PopUp Close
          prop OnPopup OnClose: TNotifyEvent
        end
        func NewItem NewLine: TMenuItem
        func ShortCut TextToShortCut: Integer

        unit Dialogs
        class TCommonDialog(TComponent)
          func Execute: Boolean
          prop Title: String
        end
        class TFileDialog TOpenDialog(TCommonDialog)
          prop FileName Filter InitialDir DefaultExt: String
          prop Files: TStrings
          prop FilterIndex Options: Integer
        end
        class TSaveDialog TSelectDirectoryDialog(TOpenDialog)
        end
        class TColorDialog(TCommonDialog)
          prop Color: TColor
        end
        class TFontDialog(TCommonDialog)
          prop Font: TFont
        end
        class TFindDialog TReplaceDialog TPrintDialog TPrinterSetupDialog(TCommonDialog)
          prop FindText ReplaceText: String
        end
        proc ShowMessage ShowMessageFmt ShowMessagePos
        func MessageDlg QuestionDlg MessageDlgPos: Integer
        func InputBox PasswordBox: String
        func InputQuery SelectDirectory: Boolean
        simple TMsgDlgType TMsgDlgButtons TMsgDlgBtn
        const mtWarning mtError mtInformation mtConfirmation mtCustom: TMsgDlgType
        const mbYes mbNo mbOK mbCancel mbAbort mbRetry mbIgnore mbAll mbNoToAll mbYesToAll mbHelp mbClose mbYesNo mbYesNoCancel mbOKCancel mbAbortRetryIgnore: TMsgDlgBtn

        unit Graphics
        class TGraphicsObject(TPersistent)
          prop OnChange: TNotifyEvent
        end
        class TFont(TGraphicsObject)
          prop Name: String
          prop Size Height Pitch Quality: Integer
          prop Color: TColor
          prop Style: TFontStyles
        end
        class TPen(TGraphicsObject)
          prop Color: TColor
          prop Width Style Mode: Integer
        end
        class TBrush(TGraphicsObject)
          prop Color: TColor
          prop Style: Integer
          prop Bitmap: TBitmap
        end
        class TCanvas(TPersistent)
          proc Rectangle Ellipse LineTo MoveTo TextOut TextRect FillRect Draw StretchDraw Polygon Polyline Arc Pie RoundRect FrameRect Clear CopyRect FloodFill Lock Unlock
          func TextWidth TextHeight: Integer
          func TextExtent: TSize
          prop Pen: TPen
          prop Brush: TBrush
          prop Font: TFont
          prop Pixels: TColor
          prop PenPos: TPoint
          prop ClipRect: TRect
          prop Handle: THandle
        end
        class TGraphic(TPersistent)
          proc LoadFromFile SaveToFile LoadFromStream SaveToStream Assign Clear
          prop Width Height: Integer
          prop Empty Transparent: Boolean
        end
        class TRasterImage(TGraphic)
          prop Canvas: TCanvas
          prop PixelFormat: Integer
          func ScanLine: Pointer
          proc SetSize BeginUpdate EndUpdate
        end
        class TCustomBitmap TBitmap TPortableNetworkGraphic TJPEGImage TPixmap TIcon TGIFImage TPortableAnyMapGraphic(TRasterImage)
        end
        class TPicture(TPersistent)
          prop Bitmap: TBitmap
          prop Graphic: TGraphic
          prop PNG: TPortableNetworkGraphic
          prop Width Height: Integer
          proc LoadFromFile SaveToFile Clear Assign
        end
        simple TColor TFontStyle TFontStyles TPenStyle TBrushStyle TGraphicClass
        const clBlack clWhite clRed clGreen clBlue clYellow clGray clSilver clNavy clMaroon clPurple clTeal clOlive clLime clAqua clFuchsia clNone clDefault clBtnFace clWindow clWindowText clHighlight clHighlightText clSkyBlue clMoneyGreen clCream clMedGray clForm clInfoBk clGrayText clMenu clScrollBar: TColor
        const fsBold fsItalic fsUnderline fsStrikeOut: TFontStyle
        const psSolid psDash psDot psDashDot psClear: TPenStyle
        const bsSolid bsClear bsHorizontal bsVertical bsCross: TBrushStyle
        func RGBToColor ColorToRGB: TColor
        func Red Green Blue: Byte

        unit Buttons
        class TCustomBitBtn TBitBtn(TButton)
          prop Glyph: TBitmap
          prop Kind Layout Spacing: Integer
        end
        class TCustomSpeedButton TSpeedButton(TGraphicControl)
          prop Down Flat AllowAllUp: Boolean
          prop Glyph: TBitmap
          prop GroupIndex: Integer
        end

        unit Grids
        class TCustomGrid TCustomDrawGrid TDrawGrid(TCustomControl)
          prop ColCount RowCount FixedCols FixedRows Col Row DefaultColWidth DefaultRowHeight: Integer
          prop ColWidths RowHeights: Integer
          prop Options: Integer
          prop OnDrawCell OnSelectCell: TNotifyEvent
        end
        class TCustomStringGrid TStringGrid(TCustomDrawGrid)
          prop Cells: String
          prop Cols Rows: TStrings
          prop Objects: TObject
        end

        unit Spin
        class TCustomSpinEdit TSpinEdit TCustomFloatSpinEdit TFloatSpinEdit(TCustomEdit)
          prop Value MinValue MaxValue Increment: Integer
        end

        unit ActnList
        class TBasicAction TContainedAction TCustomAction TAction(TComponent)
          prop Caption Hint Category: String
          prop Checked Enabled Visible: Boolean
          prop OnExecute OnUpdate: TNotifyEvent
          func Execute: Boolean
        end
        class TCustomActionList TActionList(TComponent)
        end

        unit Clipbrd
        class TClipboard(TPersistent)
          prop AsText: String
          proc Clear Assign
          func HasFormat: Boolean
        end
        func Clipboard: TClipboard

        unit FileUtil
        func FindAllFiles FindAllDirectories: TStringList
        func CopyFile CopyDirTree DeleteDirectory: Boolean
        func ProgramDirectory: String

        unit LazFileUtils
        func FileExistsUTF8 DirectoryExistsUTF8 ForceDirectoriesUTF8 DeleteFileUTF8 RenameFileUTF8 CreateDirUTF8 FilenameIsAbsolute: Boolean
        func ExtractFileNameOnly ExtractFileNameWithoutExt AppendPathDelim ChompPathDelim CreateAbsolutePath CreateRelativePath ExpandFileNameUTF8 GetCurrentDirUTF8 CleanAndExpandDirectory ResolveDots TrimFilename: String
        func CompareFilenames FileAgeUTF8 FindFirstUTF8 FindNextUTF8: Integer
        func FileSizeUtf8: Int64
        proc FindCloseUTF8

        unit LazUTF8
        func UTF8Length UTF8Pos UTF8CompareStr UTF8CompareText UTF8CodepointSize UTF8CharacterLength: Integer
        func UTF8Copy UTF8UpperCase UTF8LowerCase UTF8Trim UTF8ToSys SysToUTF8 UTF8ToWinCP WinCPToUTF8 UTF8ToConsole ConsoleToUTF8 UTF8StringReplace UTF8LeftStr UTF8RightStr UTF8Reverse UnicodeToUTF8 UTF16ToUTF8: String
        func UTF8Decode UTF8ToUTF16: UnicodeString
        proc UTF8Delete UTF8Insert
        func ParamStrUTF8 GetEnvironmentVariableUTF8: String
        """;

    /// <summary>Builds the built-in units (keyed by lower-case name) and fills the well-known types of <paramref name="types"/>.</summary>
    public static Dictionary<string, CompileUnit> Build(TypeSystem types)
    {
        var units = new Dictionary<string, CompileUnit>(StringComparer.OrdinalIgnoreCase);
        var allTypes = new Dictionary<string, Sym>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<(Sym Sym, string TypeName, bool IsResult)>();
        var ancestorsPending = new List<(PType Type, string[] Names)>();
        var helpersPending = new List<(PType Helper, string Target)>();
        CompileUnit? unit = null;
        Sym? container = null;
        foreach (var raw in Table.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var kw = space < 0 ? line : line[..space];
            var rest = space < 0 ? "" : line[(space + 1)..].Trim();
            if (kw == "unit")
            {
                unit = new CompileUnit(rest, UnitFileKind.Unit, PackageForUnit(rest)) { HasTable = true, State = UnitState.Done };
                var usym = new Sym(rest, SymKind.Unit, null)
                {
                    Symbol = unit.Package.Prefix + Names.Namespace(rest),
                    ScipKind = ScipKind.Module,
                    External = true,
                    Unit = unit,
                };
                unit.Sym = usym;
                units[rest] = unit;
                container = null;
                continue;
            }

            if (unit?.Sym is not { } owner)
            {
                continue;
            }

            if (kw == "end")
            {
                container = null;
                continue;
            }

            var (namesPart, typeName) = SplitType(rest);
            string[]? bases = null;
            var paren = namesPart.IndexOf('(', StringComparison.Ordinal);
            if (paren >= 0)
            {
                bases = namesPart[(paren + 1)..].TrimEnd(')').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                namesPart = namesPart[..paren];
            }

            var names = namesPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (kw)
            {
                case "simple" or "char" or "string" or "pchar" or "pointer" or "file":
                    foreach (var n in names)
                    {
                        var t = new PType(kw switch
                        {
                            "string" => TypeKind.String,
                            "pchar" or "pointer" => TypeKind.Pointer,
                            "file" => TypeKind.File,
                            _ => TypeKind.Simple,
                        });
                        var s = AddType(owner, unit, n, t, ScipKind.Type);
                        allTypes.TryAdd(n, s);
                        if (kw == "pchar")
                        {
                            t.Elem = types.CharType;
                        }

                        switch (n)
                        {
                            case "Char":
                                types.CharType = t;
                                break;
                            case "String":
                                types.StringType = t;
                                break;
                            case "Boolean":
                                types.BooleanType = t;
                                break;
                            case "Integer":
                                types.IntegerType = t;
                                break;
                            case "Real":
                                types.RealType = t;
                                break;
                        }
                    }

                    if (kw == "char")
                    {
                        foreach (var p in unit.Interface.All)
                        {
                            if (p.Type is { Kind: TypeKind.Pointer } pt && pt.Elem is { Kind: TypeKind.Simple } && pt.Elem != types.CharType && p.Name.StartsWith('P'))
                            {
                                pt.Elem = types.CharType;
                            }
                        }
                    }

                    break;
                case "class" or "intf" or "record" or "object" or "helper":
                    foreach (var n in names)
                    {
                        var lt = n.IndexOf('<', StringComparison.Ordinal);
                        var baseName = lt < 0 ? n : n[..lt];
                        var t = new PType(kw switch
                        {
                            "class" => TypeKind.Class,
                            "intf" => TypeKind.Interface,
                            "object" => TypeKind.Object,
                            "helper" => TypeKind.Helper,
                            _ => TypeKind.Record,
                        });
                        var s = AddType(owner, unit, baseName, t, kw switch
                        {
                            "class" => ScipKind.Class,
                            "intf" => ScipKind.Interface,
                            "object" => ScipKind.Object,
                            "helper" => ScipKind.Extension,
                            _ => ScipKind.Struct,
                        });
                        t.Members = new Scope(s);
                        if (lt >= 0)
                        {
                            s.TypeParams = [];
                            var i = 0;
                            foreach (var tp in n[(lt + 1)..].TrimEnd('>').Split(',', StringSplitOptions.TrimEntries))
                            {
                                var ps = new Sym(tp, SymKind.TypeParam, s)
                                {
                                    Symbol = s.Symbol + Names.TypeParameter(tp),
                                    External = true,
                                    ScipKind = ScipKind.TypeParameter,
                                    Index = i++,
                                };
                                ps.Type = new PType(TypeKind.TypeParam) { Decl = ps };
                                s.TypeParams.Add(ps);
                                t.Members.Add(ps);
                            }
                        }

                        if (bases is not null && kw == "helper")
                        {
                            helpersPending.Add((t, bases[0]));
                        }
                        else if (bases is not null)
                        {
                            ancestorsPending.Add((t, bases));
                        }

                        if (baseName == "TObject" && unit.Name == "System")
                        {
                            types.ObjectType = t;
                        }

                        allTypes.TryAdd(baseName, s);
                        container = s;
                    }

                    break;
                default:
                    foreach (var n in names)
                    {
                        var parent = container ?? owner;
                        var scope = container?.Type?.Members ?? unit.Interface;
                        var isDefault = kw == "prop!";
                        var (kind, scipKind, suffix) = kw switch
                        {
                            "func" or "proc" => (SymKind.Routine, container is null ? ScipKind.Function : ScipKind.Method, "()"),
                            "ctor" => (SymKind.Routine, ScipKind.Constructor, "()"),
                            "dtor" => (SymKind.Routine, ScipKind.Method, "()"),
                            "const" => (SymKind.Const, ScipKind.Constant, "."),
                            "var" => (SymKind.Var, ScipKind.Variable, "."),
                            "field" => (SymKind.Field, ScipKind.Field, "."),
                            _ => (SymKind.Property, ScipKind.Property, "."),
                        };
                        var s = new Sym(n, kind, parent)
                        {
                            Symbol = parent.Symbol + (suffix == "()" ? Names.Method(n, 0) : Names.Term(n)),
                            ScipKind = scipKind,
                            External = true,
                        };
                        if (kind == SymKind.Routine)
                        {
                            s.Routine = new RoutineInfo(kw switch
                            {
                                "func" => RoutineKind.Function,
                                "ctor" => RoutineKind.Constructor,
                                "dtor" => RoutineKind.Destructor,
                                _ => RoutineKind.Procedure,
                            })
                            { MinArgs = 0, MaxArgs = int.MaxValue, HasBody = true };
                            s.Signature = $"{(kw switch { "func" => "function", "ctor" => "constructor", "dtor" => "destructor", _ => "procedure" })} {n}{(typeName is null ? "" : ": " + typeName)}";
                        }
                        else
                        {
                            s.Signature = $"{(kind switch { SymKind.Const => "const ", SymKind.Var => "var ", SymKind.Property => "property ", _ => "" })}{n}{(typeName is null ? "" : ": " + typeName)}";
                        }

                        if (typeName is not null)
                        {
                            pending.Add((s, typeName, kind == SymKind.Routine));
                        }

                        if (isDefault && container?.Type is { } ct)
                        {
                            ct.DefaultProperty = s;
                        }

                        scope.Add(s);
                    }

                    break;
            }
        }

        foreach (var (type, baseNames) in ancestorsPending)
        {
            foreach (var b in baseNames)
            {
                if (allTypes.TryGetValue(b, out var bs) && bs.Type is { } bt && !ReferenceEquals(bt, type))
                {
                    type.Ancestors.Add(bt);
                }
            }
        }

        foreach (var (helper, target) in helpersPending)
        {
            if (allTypes.TryGetValue(target, out var ts) && ts.Type is { } tt)
            {
                helper.Elem = tt;
                types.RegisterHelper(helper, tt);
            }
        }

        foreach (var (sym, typeName, isResult) in pending)
        {
            PType? t = null;
            if (sym.Owner?.TypeParams?.FirstOrDefault(p => p.Name == typeName) is { } tp)
            {
                t = tp.Type;
            }
            else if (allTypes.TryGetValue(typeName, out var ts))
            {
                t = ts.Type;
            }

            if (isResult)
            {
                sym.Routine!.Result = t;
            }
            else
            {
                sym.Type = t;
            }
        }

        return units;
    }

    private static (string Names, string? Type) SplitType(string rest)
    {
        var colon = rest.LastIndexOf(':');
        return colon < 0 ? (rest, null) : (rest[..colon].Trim(), rest[(colon + 1)..].Trim());
    }

    private static Sym AddType(Sym owner, CompileUnit unit, string name, PType type, ScipKind kind)
    {
        var s = new Sym(name, SymKind.Type, owner)
        {
            Symbol = owner.Symbol + Names.Type(name),
            ScipKind = kind,
            External = true,
            Type = type,
            Signature = $"type {name}",
        };
        type.Decl = s;
        unit.Interface.Add(s);
        return s;
    }
}
