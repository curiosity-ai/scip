// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Vcl.Menus;

interface

uses System.Classes;

type
  TShortCut = Low(Word)..High(Word);

  TMenuItem = class(TComponent)
  public
    procedure Add(Item: TMenuItem); overload;
    procedure Click; virtual;
    procedure Clear;
    procedure Delete(Index: Integer);
    property Caption: string read FCaption write SetCaption;
    property Checked: Boolean read FChecked write SetChecked;
    property Count: Integer read GetCount;
    property Enabled: Boolean read FEnabled write SetEnabled;
    property Items[Index: Integer]: TMenuItem read GetItem; default;
    property ShortCut: TShortCut read FShortCut write SetShortCut;
    property Visible: Boolean read FVisible write SetVisible;
    property OnClick: TNotifyEvent read FOnClick write FOnClick;
  end;

  TMenu = class(TComponent)
  public
    property Items: TMenuItem read FItems;
  end;

  TMainMenu = class(TMenu);

  TPopupMenu = class(TMenu)
  public
    procedure Popup(X, Y: Integer); virtual;
    property OnPopup: TNotifyEvent read FOnPopup write FOnPopup;
  end;

function ShortCut(Key: Word; Shift: TShiftState): TShortCut;

implementation

end.
