unit MainForm;

interface

uses
  Winapi.Windows, System.Classes, Vcl.Forms, Vcl.Controls, Vcl.StdCtrls, Vcl.ExtCtrls, Shapes;

type
  TFormMain = class(TForm)
    PanelTop: TPanel;
    ButtonDraw: TButton;
    EditName: TEdit;
    MemoLog: TMemo;
    procedure ButtonDrawClick(Sender: TObject);
    procedure FormCreate(Sender: TObject);
  private
    FShape: TShape;
  end;

var
  FormMain: TFormMain;

implementation

{$R *.dfm}

procedure TFormMain.FormCreate(Sender: TObject);
begin
  FShape := MakeCircle(2.0);
  Caption := 'Shapes';
end;

procedure TFormMain.ButtonDrawClick(Sender: TObject);
begin
  FShape.Name := EditName.Text;
  FShape.Draw(MemoLog.Lines);
  MemoLog.Lines.Add(IntToStr(MemoLog.Lines.Count));
end;

end.
