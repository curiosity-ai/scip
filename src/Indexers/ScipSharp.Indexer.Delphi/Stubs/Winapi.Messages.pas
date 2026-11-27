// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Winapi.Messages;

interface

uses Winapi.Windows;

type
  TMessage = record
    Msg: Cardinal;
    WParam: WPARAM;
    LParam: LPARAM;
    Result: LRESULT;
  end;

  TWMNoParams = record
    Msg: Cardinal;
    Result: LRESULT;
  end;

  TWMKey = record
    Msg: Cardinal;
    CharCode: Word;
    KeyData: Longint;
    Result: LRESULT;
  end;

  TWMPaint = record
    Msg: Cardinal;
    DC: HDC;
    Result: LRESULT;
  end;

  TWMSize = record
    Msg: Cardinal;
    SizeType: WPARAM;
    Width: Word;
    Height: Word;
    Result: LRESULT;
  end;

const
  WM_USER = $0400;
  WM_CREATE = $0001;
  WM_DESTROY = $0002;
  WM_MOVE = $0003;
  WM_SIZE = $0005;
  WM_PAINT = $000F;
  WM_CLOSE = $0010;
  WM_QUIT = $0012;
  WM_KEYDOWN = $0100;
  WM_KEYUP = $0101;
  WM_CHAR = $0102;
  WM_COMMAND = $0111;
  WM_TIMER = $0113;
  WM_MOUSEMOVE = $0200;
  WM_LBUTTONDOWN = $0201;
  WM_LBUTTONUP = $0202;

implementation

end.
