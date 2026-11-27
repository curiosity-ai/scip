// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Vcl.StdCtrls;

interface

uses System.Classes, System.UITypes, Vcl.Controls, Vcl.Graphics;

type
  TCheckBoxState = (cbUnchecked, cbChecked, cbGrayed);
  TEditCharCase = (ecNormal, ecUpperCase, ecLowerCase);
  TScrollStyle = System.UITypes.TScrollStyle;
  TComboBoxStyle = (csDropDown, csSimple, csDropDownList, csOwnerDrawFixed, csOwnerDrawVariable);

  TCustomLabel = class(TGraphicControl)
  public
    property AutoSize: Boolean read FAutoSize write SetAutoSize;
    property WordWrap: Boolean read FWordWrap write SetWordWrap;
  end;

  TLabel = class(TCustomLabel)
  public
    property FocusControl: TWinControl read FFocusControl write SetFocusControl;
    property Layout: Integer read FLayout write SetLayout;
  end;

  TCustomEdit = class(TWinControl)
  public
    procedure Clear; virtual;
    procedure ClearSelection;
    procedure CopyToClipboard;
    procedure CutToClipboard;
    procedure PasteFromClipboard;
    procedure SelectAll;
    property CharCase: TEditCharCase read FCharCase write SetCharCase;
    property MaxLength: Integer read FMaxLength write SetMaxLength;
    property Modified: Boolean read GetModified write SetModified;
    property PasswordChar: Char read FPasswordChar write SetPasswordChar;
    property ReadOnly: Boolean read FReadOnly write SetReadOnly;
    property SelLength: Integer read GetSelLength write SetSelLength;
    property SelStart: Integer read GetSelStart write SetSelStart;
    property SelText: string read GetSelText write SetSelText;
    property TextHint: string read FTextHint write SetTextHint;
    property OnChange: TNotifyEvent read FOnChange write FOnChange;
  end;

  TEdit = class(TCustomEdit);

  TCustomMemo = class(TCustomEdit)
  public
    property Lines: TStrings read FLines write SetLines;
    property ScrollBars: TScrollStyle read FScrollBars write SetScrollBars;
    property WordWrap: Boolean read FWordWrap write SetWordWrap;
  end;

  TMemo = class(TCustomMemo);

  TButtonControl = class(TWinControl);

  TButton = class(TButtonControl)
  public
    procedure Click; virtual;
    property Cancel: Boolean read FCancel write FCancel;
    property Default: Boolean read FDefault write SetDefault;
    property ModalResult: TModalResult read FModalResult write FModalResult;
  end;

  TCustomCheckBox = class(TButtonControl)
  public
    property Checked: Boolean read GetChecked write SetChecked;
    property State: TCheckBoxState read FState write SetState;
    property AllowGrayed: Boolean read FAllowGrayed write FAllowGrayed;
  end;

  TCheckBox = class(TCustomCheckBox);

  TRadioButton = class(TButtonControl)
  public
    property Checked: Boolean read FChecked write SetChecked;
  end;

  TCustomListControl = class(TWinControl)
  public
    procedure Clear; virtual; abstract;
    property ItemIndex: Integer read GetItemIndex write SetItemIndex;
  end;

  TCustomCombo = class(TCustomListControl)
  public
    property Items: TStrings read FItems write SetItems;
    property DropDownCount: Integer read FDropDownCount write SetDropDownCount;
    property OnChange: TNotifyEvent read FOnChange write FOnChange;
    property OnSelect: TNotifyEvent read FOnSelect write FOnSelect;
  end;

  TCustomComboBox = class(TCustomCombo)
  public
    property Style: TComboBoxStyle read FStyle write SetStyle;
    property Sorted: Boolean read FSorted write SetSorted;
  end;

  TComboBox = class(TCustomComboBox);

  TCustomListBox = class(TCustomListControl)
  public
    procedure Clear; override;
    property Items: TStrings read FItems write SetItems;
    property MultiSelect: Boolean read FMultiSelect write SetMultiSelect;
    property Selected[Index: Integer]: Boolean read GetSelected write SetSelected;
    property Sorted: Boolean read FSorted write SetSorted;
  end;

  TListBox = class(TCustomListBox);

  TCustomGroupBox = class(TCustomControl);

  TGroupBox = class(TCustomGroupBox);

  TScrollBar = class(TWinControl)
  public
    property Max: Integer read FMax write SetMax;
    property Min: Integer read FMin write SetMin;
    property Position: Integer read FPosition write SetPosition;
    property OnChange: TNotifyEvent read FOnChange write FOnChange;
  end;

implementation

end.
