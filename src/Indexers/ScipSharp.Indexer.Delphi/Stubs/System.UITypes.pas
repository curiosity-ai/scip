// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.UITypes;

interface

type
  TColor = -$7FFFFFFF-1..$7FFFFFFF;
  TAlphaColor = Cardinal;
  TModalResult = Low(Integer)..High(Integer);
  TMsgDlgType = (mtWarning, mtError, mtInformation, mtConfirmation, mtCustom);
  TMsgDlgBtn = (mbYes, mbNo, mbOK, mbCancel, mbAbort, mbRetry, mbIgnore, mbAll, mbNoToAll, mbYesToAll, mbHelp, mbClose);
  TMsgDlgButtons = set of TMsgDlgBtn;
  TFontStyle = (fsBold, fsItalic, fsUnderline, fsStrikeOut);
  TFontStyles = set of TFontStyle;
  TCursor = -32768..32767;
  TMouseButton = (mbLeft, mbRight, mbMiddle);
  TScrollStyle = (ssNone, ssHorizontal, ssVertical, ssBoth);
  TWindowState = (wsNormal, wsMinimized, wsMaximized);

  TColors = record
  const
    Black = TColor($000000);
    White = TColor($FFFFFF);
    Red = TColor($0000FF);
    Green = TColor($008000);
    Blue = TColor($FF0000);
    Yellow = TColor($00FFFF);
    Gray = TColor($808080);
  end;

  TAlphaColors = record
  const
    Black = TAlphaColor($FF000000);
    White = TAlphaColor($FFFFFFFF);
    Red = TAlphaColor($FFFF0000);
    Green = TAlphaColor($FF008000);
    Blue = TAlphaColor($FF0000FF);
  end;

const
  mrNone = 0;
  mrOk = 1;
  mrCancel = 2;
  mrAbort = 3;
  mrRetry = 4;
  mrIgnore = 5;
  mrYes = 6;
  mrNo = 7;
  mrClose = 8;
  mrAll = 12;

implementation

end.
