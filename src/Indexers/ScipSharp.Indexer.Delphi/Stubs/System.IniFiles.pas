// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.IniFiles;

interface

uses System.Classes, System.SysUtils;

type
  EIniFileException = class(Exception);

  TCustomIniFile = class(TObject)
  public
    constructor Create(const FileName: string);
    function SectionExists(const Section: string): Boolean;
    function ReadString(const Section, Ident, Default: string): string; virtual; abstract;
    procedure WriteString(const Section, Ident, Value: string); virtual; abstract;
    function ReadInteger(const Section, Ident: string; Default: Integer): Integer; virtual;
    procedure WriteInteger(const Section, Ident: string; Value: Integer); virtual;
    function ReadInt64(const Section, Ident: string; Default: Int64): Int64; virtual;
    procedure WriteInt64(const Section, Ident: string; Value: Int64); virtual;
    function ReadBool(const Section, Ident: string; Default: Boolean): Boolean; virtual;
    procedure WriteBool(const Section, Ident: string; Value: Boolean); virtual;
    function ReadFloat(const Section, Name: string; Default: Double): Double; virtual;
    procedure WriteFloat(const Section, Name: string; Value: Double); virtual;
    function ReadDateTime(const Section, Name: string; Default: TDateTime): TDateTime; virtual;
    procedure WriteDateTime(const Section, Name: string; Value: TDateTime); virtual;
    procedure ReadSection(const Section: string; Strings: TStrings); virtual; abstract;
    procedure ReadSections(Strings: TStrings); overload; virtual; abstract;
    procedure ReadSectionValues(const Section: string; Strings: TStrings); virtual; abstract;
    procedure EraseSection(const Section: string); virtual; abstract;
    procedure DeleteKey(const Section, Ident: string); virtual; abstract;
    procedure UpdateFile; virtual; abstract;
    function ValueExists(const Section, Ident: string): Boolean; virtual;
    property FileName: string read FFileName;
  end;

  TIniFile = class(TCustomIniFile)
  public
    destructor Destroy; override;
    function ReadString(const Section, Ident, Default: string): string; override;
    procedure WriteString(const Section, Ident, Value: string); override;
    procedure UpdateFile; override;
  end;

  TMemIniFile = class(TCustomIniFile)
  public
    constructor Create(const FileName: string); overload;
    constructor Create(const FileName: string; const Encoding: TEncoding); overload;
    destructor Destroy; override;
    procedure Clear;
    procedure GetStrings(List: TStrings);
    procedure SetStrings(List: TStrings);
    function ReadString(const Section, Ident, Default: string): string; override;
    procedure WriteString(const Section, Ident, Value: string); override;
    procedure UpdateFile; override;
    property AutoSave: Boolean read FAutoSave write FAutoSave;
    property CaseSensitive: Boolean read GetCaseSensitive write SetCaseSensitive;
    property Encoding: TEncoding read FEncoding write FEncoding;
  end;

implementation

end.
