// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.IOUtils;

interface

uses System.SysUtils, System.Types;

type
  TSearchOption = (soTopDirectoryOnly, soAllDirectories);

  TPath = record
  public
    class function Combine(const Path1, Path2: string): string; overload; static;
    class function GetDirectoryName(FileName: string): string; static;
    class function GetExtension(const FileName: string): string; static;
    class function GetFileName(const FileName: string): string; static;
    class function GetFileNameWithoutExtension(const FileName: string): string; static;
    class function GetFullPath(const Path: string): string; static;
    class function GetTempPath: string; static;
    class function GetTempFileName: string; static;
    class function GetHomePath: string; static;
    class function GetDocumentsPath: string; static;
    class function ChangeExtension(const Path, Extension: string): string; static;
    class function HasExtension(const Path: string): Boolean; static;
    class function IsPathRooted(const Path: string): Boolean; static;
    class function GetRandomFileName: string; static;
    class property DirectorySeparatorChar: Char read FDirectorySeparatorChar;
    class property PathSeparator: Char read FPathSeparator;
  end;

  TFile = record
  public
    class function Exists(const Path: string; FollowLink: Boolean = True): Boolean; static;
    class procedure Delete(const Path: string); static;
    class procedure Copy(const SourceFileName, DestFileName: string); overload; static;
    class procedure Move(const SourceFileName, DestFileName: string); static;
    class function ReadAllText(const Path: string): string; overload; static;
    class function ReadAllText(const Path: string; const Encoding: TEncoding): string; overload; static;
    class function ReadAllBytes(const Path: string): TBytes; static;
    class function ReadAllLines(const Path: string): TStringDynArray; overload; static;
    class procedure WriteAllText(const Path, Contents: string); overload; static;
    class procedure WriteAllText(const Path, Contents: string; const Encoding: TEncoding); overload; static;
    class procedure WriteAllBytes(const Path: string; const Bytes: TBytes); static;
    class procedure AppendAllText(const Path, Contents: string); overload; static;
    class function GetSize(const Path: string): Int64; static;
    class function GetLastWriteTime(const Path: string): TDateTime; static;
  end;

  TDirectory = record
  public
    class procedure CreateDirectory(Path: string); static;
    class procedure Delete(const Path: string; const Recursive: Boolean = False); overload; static;
    class function Exists(const Path: string; FollowLink: Boolean = True): Boolean; static;
    class function GetFiles(const Path: string): TStringDynArray; overload; static;
    class function GetFiles(const Path, SearchPattern: string): TStringDynArray; overload; static;
    class function GetFiles(const Path, SearchPattern: string; const SearchOption: TSearchOption): TStringDynArray; overload; static;
    class function GetDirectories(const Path: string): TStringDynArray; overload; static;
    class function GetCurrentDirectory: string; static;
    class procedure SetCurrentDirectory(const Path: string); static;
    class function IsEmpty(const Path: string): Boolean; static;
  end;

implementation

end.
