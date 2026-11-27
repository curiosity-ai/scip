// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Vcl.Forms;

interface

uses System.Classes, System.Types, System.UITypes, Vcl.Controls, Vcl.Graphics, Vcl.Menus;

type
  TFormStyle = (fsNormal, fsMDIChild, fsMDIForm, fsStayOnTop);
  TFormBorderStyle = (bsNone, bsSingle, bsSizeable, bsDialog, bsToolWindow, bsSizeToolWin);
  TPosition = (poDesigned, poDefault, poDefaultPosOnly, poDefaultSizeOnly, poScreenCenter, poDesktopCenter, poMainFormCenter, poOwnerFormCenter);
  TWindowState = System.UITypes.TWindowState;
  TCloseAction = (caNone, caHide, caFree, caMinimize);
  TCloseEvent = procedure(Sender: TObject; var Action: TCloseAction) of object;
  TCloseQueryEvent = procedure(Sender: TObject; var CanClose: Boolean) of object;

  TScrollingWinControl = class(TWinControl)
  public
    procedure ScrollInView(AControl: TControl);
  end;

  TScrollBox = class(TScrollingWinControl);

  TCustomFrame = class(TScrollingWinControl)
  public
    constructor Create(AOwner: TComponent); override;
  end;

  TFrame = class(TCustomFrame);

  TCustomForm = class(TScrollingWinControl)
  public
    constructor Create(AOwner: TComponent); override;
    constructor CreateNew(AOwner: TComponent; Dummy: Integer = 0); virtual;
    destructor Destroy; override;
    procedure Close;
    function CloseQuery: Boolean; virtual;
    procedure Release;
    function ShowModal: Integer; virtual;
    procedure Show;
    procedure Hide;
    procedure BringToFront;
    property ActiveControl: TWinControl read FActiveControl write SetActiveControl;
    property BorderStyle: TFormBorderStyle read FBorderStyle write SetBorderStyle;
    property Canvas: TCanvas read GetCanvas;
    property FormStyle: TFormStyle read FFormStyle write SetFormStyle;
    property KeyPreview: Boolean read FKeyPreview write FKeyPreview;
    property Menu: TMainMenu read FMenu write SetMenu;
    property ModalResult: TModalResult read FModalResult write FModalResult;
    property Position: TPosition read FPosition write SetPosition;
    property WindowState: TWindowState read FWindowState write SetWindowState;
    property OnActivate: TNotifyEvent read FOnActivate write FOnActivate;
    property OnClose: TCloseEvent read FOnClose write FOnClose;
    property OnCloseQuery: TCloseQueryEvent read FOnCloseQuery write FOnCloseQuery;
    property OnCreate: TNotifyEvent read FOnCreate write FOnCreate;
    property OnDeactivate: TNotifyEvent read FOnDeactivate write FOnDeactivate;
    property OnDestroy: TNotifyEvent read FOnDestroy write FOnDestroy;
    property OnHide: TNotifyEvent read FOnHide write FOnHide;
    property OnPaint: TNotifyEvent read FOnPaint write FOnPaint;
    property OnShow: TNotifyEvent read FOnShow write FOnShow;
  end;
  TCustomFormClass = class of TCustomForm;

  TForm = class(TCustomForm);
  TFormClass = class of TForm;

  TApplication = class(TComponent)
  public
    procedure CreateForm(InstanceClass: TComponentClass; var Reference);
    procedure HandleException(Sender: TObject);
    procedure Initialize;
    function MessageBox(const Text, Caption: PChar; Flags: Longint = 0): Integer;
    procedure Minimize;
    procedure ProcessMessages;
    procedure Restore;
    procedure Run;
    procedure ShowException(E: Exception);
    procedure Terminate;
    property ExeName: string read GetExeName;
    property Handle: THandle read FHandle write SetHandle;
    property HintHidePause: Integer read FHintHidePause write FHintHidePause;
    property MainForm: TForm read FMainForm;
    property MainFormOnTaskbar: Boolean read FMainFormOnTaskBar write SetMainFormOnTaskBar;
    property ShowMainForm: Boolean read FShowMainForm write FShowMainForm;
    property Terminated: Boolean read FTerminate;
    property Title: string read GetTitle write SetTitle;
    property OnException: TNotifyEvent read FOnException write FOnException;
    property OnIdle: TNotifyEvent read FOnIdle write FOnIdle;
  end;

  TScreen = class(TComponent)
  public
    property ActiveForm: TForm read FActiveForm;
    property Cursor: TCursor read FCursor write SetCursor;
    property FormCount: Integer read GetFormCount;
    property Forms[Index: Integer]: TForm read GetForm;
    property Height: Integer read GetHeight;
    property Width: Integer read GetWidth;
  end;

var
  Application: TApplication;
  Screen: TScreen;

implementation

end.
