// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit FMX.Forms;

interface

uses System.Classes, System.Types, System.UITypes, FMX.Types, FMX.Controls;

type
  TCloseAction = (caNone, caHide, caFree, caMinimize);
  TCloseEvent = procedure(Sender: TObject; var Action: TCloseAction) of object;

  TCommonCustomForm = class(TFmxObject)
  public
    constructor Create(AOwner: TComponent); override;
    procedure Close;
    procedure Show;
    procedure Hide;
    function ShowModal: TModalResult; overload;
    property Caption: string read GetCaption write SetCaption;
    property ClientHeight: Integer read GetClientHeight write SetClientHeight;
    property ClientWidth: Integer read GetClientWidth write SetClientWidth;
    property Height: Integer read GetHeight write SetHeight;
    property ModalResult: TModalResult read FModalResult write SetModalResult;
    property Width: Integer read GetWidth write SetWidth;
    property OnClose: TCloseEvent read FOnClose write FOnClose;
    property OnCreate: TNotifyEvent read FOnCreate write FOnCreate;
    property OnDestroy: TNotifyEvent read FOnDestroy write FOnDestroy;
    property OnShow: TNotifyEvent read FOnShow write FOnShow;
  end;

  TCustomForm = class(TCommonCustomForm);

  TForm = class(TCustomForm);

  TFrame = class(TControl);

  TApplication = class(TComponent)
  public
    procedure CreateForm(const InstanceClass: TComponentClass; var Reference);
    procedure Initialize;
    procedure ProcessMessages;
    procedure Run;
    procedure Terminate;
    property MainForm: TCommonCustomForm read FMainForm write SetMainForm;
    property Title: string read GetTitle write SetTitle;
  end;

var
  Application: TApplication;

implementation

end.
