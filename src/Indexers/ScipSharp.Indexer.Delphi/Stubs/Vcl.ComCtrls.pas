// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Vcl.ComCtrls;

interface

uses System.Classes, Vcl.Controls, Vcl.Graphics, Vcl.StdCtrls;

type
  TViewStyle = (vsIcon, vsSmallIcon, vsList, vsReport);

  TStatusPanel = class(TCollectionItem)
  public
    property Text: string read FText write SetText;
    property Width: Integer read FWidth write SetWidth;
  end;

  TStatusPanels = class(TCollection)
  public
    function Add: TStatusPanel;
    property Items[Index: Integer]: TStatusPanel read GetItem write SetItem; default;
  end;

  TStatusBar = class(TWinControl)
  public
    property Panels: TStatusPanels read FPanels write SetPanels;
    property SimplePanel: Boolean read FSimplePanel write SetSimplePanel;
    property SimpleText: string read FSimpleText write SetSimpleText;
  end;

  TProgressBar = class(TWinControl)
  public
    procedure StepIt;
    property Max: Integer read GetMax write SetMax;
    property Min: Integer read GetMin write SetMin;
    property Position: Integer read GetPosition write SetPosition;
    property Step: Integer read FStep write SetStep;
  end;

  TTabSheet = class(TWinControl)
  public
    property PageControl: TPageControl read FPageControl write SetPageControl;
    property PageIndex: Integer read GetPageIndex write SetPageIndex;
    property TabVisible: Boolean read FTabVisible write SetTabVisible;
  end;

  TPageControl = class(TWinControl)
  public
    property ActivePage: TTabSheet read FActivePage write SetActivePage;
    property ActivePageIndex: Integer read GetActivePageIndex write SetActivePageIndex;
    property PageCount: Integer read GetPageCount;
    property Pages[Index: Integer]: TTabSheet read GetPage;
    property OnChange: TNotifyEvent read FOnChange write FOnChange;
  end;

  TTreeNode = class(TPersistent)
  public
    property Count: Integer read GetCount;
    property Data: Pointer read GetData write SetData;
    property Expanded: Boolean read GetExpanded write SetExpanded;
    property Item[Index: Integer]: TTreeNode read GetItem; default;
    property Parent: TTreeNode read GetParent;
    property Text: string read FText write SetText;
  end;

  TTreeNodes = class(TPersistent)
  public
    function Add(Sibling: TTreeNode; const S: string): TTreeNode;
    function AddChild(Parent: TTreeNode; const S: string): TTreeNode;
    procedure Clear;
    property Count: Integer read GetCount;
    property Item[Index: Integer]: TTreeNode read GetNodeFromIndex; default;
  end;

  TTreeView = class(TWinControl)
  public
    property Items: TTreeNodes read FTreeNodes write SetTreeNodes;
    property Selected: TTreeNode read GetSelection write SetSelection;
    property OnChange: TNotifyEvent read FOnChange write FOnChange;
  end;

  TListItem = class(TPersistent)
  public
    property Caption: string read FCaption write SetCaption;
    property Data: Pointer read FData write SetData;
    property SubItems: TStrings read GetSubItems write SetSubItems;
  end;

  TListItems = class(TPersistent)
  public
    function Add: TListItem;
    procedure Clear;
    property Count: Integer read GetCount;
    property Item[Index: Integer]: TListItem read GetItem; default;
  end;

  TListView = class(TWinControl)
  public
    property Items: TListItems read FListItems write SetItems;
    property Selected: TListItem read GetSelected write SetSelected;
    property ViewStyle: TViewStyle read FViewStyle write SetViewStyle;
  end;

  TRichEdit = class(TCustomMemo);

  TDateTimePicker = class(TWinControl)
  public
    property Date: TDate read GetDate write SetDate;
    property DateTime: TDateTime read GetDateTime write SetDateTime;
    property Time: TTime read GetTime write SetTime;
  end;

implementation

end.
