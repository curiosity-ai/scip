// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Vcl.Dialogs;

interface

uses System.Classes, System.UITypes, Vcl.Controls, Vcl.Forms;

type
  TMsgDlgType = System.UITypes.TMsgDlgType;
  TMsgDlgBtn = System.UITypes.TMsgDlgBtn;
  TMsgDlgButtons = System.UITypes.TMsgDlgButtons;

  TCommonDialog = class(TComponent)
  public
    function Execute: Boolean; overload; virtual;
    property OnClose: TNotifyEvent read FOnClose write FOnClose;
    property OnShow: TNotifyEvent read FOnShow write FOnShow;
  end;

  TOpenDialog = class(TCommonDialog)
  public
    property DefaultExt: string read GetDefaultExt write SetDefaultExt;
    property FileName: string read GetFileName write SetFileName;
    property Files: TStrings read GetFiles;
    property Filter: string read GetFilter write SetFilter;
    property FilterIndex: Integer read FFilterIndex write FFilterIndex;
    property InitialDir: string read GetInitialDir write SetInitialDir;
    property Title: string read GetTitle write SetTitle;
  end;

  TSaveDialog = class(TOpenDialog);

  TColorDialog = class(TCommonDialog)
  public
    property Color: Integer read FColor write FColor;
  end;

  TFontDialog = class(TCommonDialog);

  TTaskDialog = class(TComponent)
  public
    function Execute: Boolean; overload; virtual;
    property Caption: string read FCaption write FCaption;
    property Text: string read FText write FText;
    property Title: string read FTitle write FTitle;
  end;

const
  mtWarning = System.UITypes.TMsgDlgType.mtWarning;
  mtError = System.UITypes.TMsgDlgType.mtError;
  mtInformation = System.UITypes.TMsgDlgType.mtInformation;
  mtConfirmation = System.UITypes.TMsgDlgType.mtConfirmation;
  mbYesNo = [mbYes, mbNo];
  mbYesNoCancel = [mbYes, mbNo, mbCancel];
  mbOKCancel = [mbOK, mbCancel];

procedure ShowMessage(const Msg: string);
procedure ShowMessageFmt(const Msg: string; Params: array of const);
procedure ShowMessagePos(const Msg: string; X, Y: Integer);
function MessageDlg(const Msg: string; DlgType: TMsgDlgType; Buttons: TMsgDlgButtons; HelpCtx: Longint): Integer; overload;
function InputBox(const ACaption, APrompt, ADefault: string): string;
function InputQuery(const ACaption, APrompt: string; var Value: string): Boolean; overload;

implementation

end.
