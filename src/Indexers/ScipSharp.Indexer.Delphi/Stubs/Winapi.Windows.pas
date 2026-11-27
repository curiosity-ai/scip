// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Winapi.Windows;

interface

type
  BOOL = LongBool;
  DWORD = Cardinal;
  UINT = Cardinal;
  ULONG = Cardinal;
  LONG = Integer;
  WPARAM = NativeUInt;
  LPARAM = NativeInt;
  LRESULT = NativeInt;
  HWND = NativeUInt;
  HDC = NativeUInt;
  HMODULE = NativeUInt;
  HINST = NativeUInt;
  HKEY = NativeUInt;
  HICON = NativeUInt;
  HBITMAP = NativeUInt;
  HMENU = NativeUInt;
  LPCWSTR = PWideChar;
  LPWSTR = PWideChar;
  PDWORD = ^DWORD;

  TRTLCriticalSection = record
  end;

  TSystemTime = record
    wYear: Word;
    wMonth: Word;
    wDayOfWeek: Word;
    wDay: Word;
    wHour: Word;
    wMinute: Word;
    wSecond: Word;
    wMilliseconds: Word;
  end;

  TMsg = record
    hwnd: HWND;
    message: UINT;
    wParam: WPARAM;
    lParam: LPARAM;
  end;

const
  MAX_PATH = 260;
  INFINITE = DWORD($FFFFFFFF);
  MB_OK = $00000000;
  MB_OKCANCEL = $00000001;
  MB_YESNO = $00000004;
  MB_ICONERROR = $00000010;
  MB_ICONWARNING = $00000030;
  MB_ICONINFORMATION = $00000040;
  IDOK = 1;
  IDCANCEL = 2;
  IDYES = 6;
  IDNO = 7;
  ERROR_SUCCESS = 0;
  WAIT_OBJECT_0 = 0;
  WAIT_TIMEOUT = 258;
  SW_SHOW = 5;
  SW_HIDE = 0;
  SW_SHOWNORMAL = 1;

function MessageBox(hWnd: HWND; lpText, lpCaption: PChar; uType: UINT): Integer; stdcall;
function GetTickCount: DWORD; stdcall;
function GetTickCount64: UInt64; stdcall;
function GetCurrentThreadId: DWORD; stdcall;
function GetCurrentProcessId: DWORD; stdcall;
function GetLastError: DWORD; stdcall;
procedure Sleep(dwMilliseconds: DWORD); stdcall;
function CloseHandle(hObject: THandle): BOOL; stdcall;
function WaitForSingleObject(hHandle: THandle; dwMilliseconds: DWORD): DWORD; stdcall;
function LoadLibrary(lpLibFileName: PChar): HMODULE; stdcall;
function FreeLibrary(hLibModule: HMODULE): BOOL; stdcall;
function GetProcAddress(hModule: HMODULE; lpProcName: PAnsiChar): Pointer; stdcall;
function GetModuleFileName(hModule: HMODULE; lpFilename: PChar; nSize: DWORD): DWORD; stdcall;
function SendMessage(hWnd: HWND; Msg: UINT; wParam: WPARAM; lParam: LPARAM): LRESULT; stdcall;
function PostMessage(hWnd: HWND; Msg: UINT; wParam: WPARAM; lParam: LPARAM): BOOL; stdcall;
function ShellExecute(hWnd: HWND; Operation, FileName, Parameters, Directory: PChar; ShowCmd: Integer): HINST; stdcall;
function QueryPerformanceCounter(var lpPerformanceCount: Int64): BOOL; stdcall;
function QueryPerformanceFrequency(var lpFrequency: Int64): BOOL; stdcall;
procedure OutputDebugString(lpOutputString: PChar); stdcall;
procedure InitializeCriticalSection(var lpCriticalSection: TRTLCriticalSection); stdcall;
procedure EnterCriticalSection(var lpCriticalSection: TRTLCriticalSection); stdcall;
procedure LeaveCriticalSection(var lpCriticalSection: TRTLCriticalSection); stdcall;
procedure DeleteCriticalSection(var lpCriticalSection: TRTLCriticalSection); stdcall;
function InterlockedIncrement(var Addend: Integer): Integer; stdcall;
function InterlockedDecrement(var Addend: Integer): Integer; stdcall;
function VirtualProtect(lpAddress: Pointer; dwSize: NativeUInt; flNewProtect: DWORD; var OldProtect: DWORD): BOOL; stdcall;
function FlushInstructionCache(hProcess: THandle; const lpBaseAddress: Pointer; dwSize: NativeUInt): BOOL; stdcall;
function GetCurrentProcess: THandle; stdcall;

implementation

end.
