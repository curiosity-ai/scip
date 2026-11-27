// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Vcl.ActnList;

interface

uses System.Classes, Vcl.Controls, Vcl.Menus;

type
  TContainedAction = class(TBasicAction)
  public
    property Category: string read FCategory write SetCategory;
    property Index: Integer read GetIndex write SetIndex;
  end;

  TCustomAction = class(TContainedAction)
  public
    property Caption: string read FCaption write SetCaption;
    property Checked: Boolean read FChecked write SetChecked;
    property Enabled: Boolean read FEnabled write SetEnabled;
    property Hint: string read FHint write SetHint;
    property ImageIndex: Integer read FImageIndex write SetImageIndex;
    property ShortCut: TShortCut read FShortCut write SetShortCut;
    property Visible: Boolean read FVisible write SetVisible;
    property OnUpdate: TNotifyEvent read FOnUpdate write SetOnUpdate;
  end;

  TAction = class(TCustomAction)
  public
    constructor Create(AOwner: TComponent); override;
  end;

  TCustomActionList = class(TComponent)
  public
    function ExecuteAction(Action: TBasicAction): Boolean; override;
    function UpdateAction(Action: TBasicAction): Boolean; override;
    property ActionCount: Integer read GetActionCount;
    property Actions[Index: Integer]: TContainedAction read GetAction write SetAction; default;
    property State: Integer read FState write SetState;
    property OnExecute: TNotifyEvent read FOnExecute write FOnExecute;
    property OnUpdate: TNotifyEvent read FOnUpdate write FOnUpdate;
  end;

  TActionList = class(TCustomActionList);

implementation

end.
