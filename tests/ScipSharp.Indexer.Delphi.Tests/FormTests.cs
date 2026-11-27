using ScipSharp.Indexer.Delphi.Forms;
using ScipSharp.Indexer.Delphi.Project;

namespace ScipSharp.Indexer.Delphi.Tests;

public class FormTests
{
    [Test]
    public async Task ParsesObjectsPropertiesAndSkipsCollections()
    {
        var roots = DfmParser.Parse("""
            object Form1: TForm1
              Caption = 'Hello'
              OnCreate = FormCreate
              object Grid: TStringGrid
                Columns = <
                  item
                    Title = 'A'
                  end
                  item
                    Title = 'B'
                  end>
                Picture.Data = {
                  0102030405
                  0607}
                Items.Strings = (
                  'one'
                  'two')
                OnClick = GridClick
              end
              inherited Panel1: TPanel
              end
              inline Frame1: TFrame1
              end
            end
            """);
        var form = roots.Single();
        await Assert.That(form.Name).IsEqualTo("Form1");
        await Assert.That(form.TypeName).IsEqualTo("TForm1");
        await Assert.That(form.Properties.Single(p => p.Name == "OnCreate").IdentifierValue).IsEqualTo("FormCreate");
        await Assert.That(form.Properties.Single(p => p.Name == "Caption").IdentifierValue).IsNull();
        await Assert.That(form.Children.Select(c => c.Keyword).ToArray()).IsEquivalentTo(["object", "inherited", "inline"]);
        var grid = form.Children[0];
        await Assert.That(grid.Properties.Select(p => p.Name).ToArray()).IsEquivalentTo(["Columns", "Picture.Data", "Items.Strings", "OnClick"]);
        await Assert.That(grid.Properties[^1].Line).IsEqualTo(17);
        await Assert.That(grid.Properties[^1].ValueColumn).IsEqualTo(14);
    }

    [Test]
    public async Task BinaryFormsAreDetected()
    {
        await Assert.That(DfmParser.IsBinary("TPF0\u0006TForm1"u8)).IsTrue();
        await Assert.That(DfmParser.IsBinary("object X: TY"u8)).IsFalse();
    }

    [Test]
    public async Task FormComponentsAndEventsResolveToTheFormClass()
    {
        var files = new Dictionary<string, string>
        {
            ["Main.pas"] = """
                unit Main;
                interface
                uses Vcl.Forms, Vcl.StdCtrls;
                type
                  TMainForm = class(TForm)
                    OkButton: TButton;
                    procedure OkButtonClick(Sender: TObject);
                  end;
                var
                  MainForm: TMainForm;
                implementation
                {$R *.dfm}
                procedure TMainForm.OkButtonClick(Sender: TObject); begin Close; end;
                end.
                """,
            ["Main.dfm"] = """
                object MainForm: TMainForm
                  Caption = 'Main'
                  object OkButton: TButton
                    Caption = 'OK'
                    OnClick = OkButtonClick
                  end
                end
                """,
        };
        var index = TestUtil.Index(files, new DelphiIndexerOptions { ProjectName = "p", ProjectVersion = "1" });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        var dfm = index.Documents.Single(d => d.RelativePath == "Main.dfm");
        await Assert.That(dfm.Language).IsEqualTo("Delphi");
        var symbols = dfm.Occurrences.Select(o => o.Symbol).ToHashSet();
        await Assert.That(symbols).Contains("scip-delphi delphi p 1 Main/TMainForm#");
        await Assert.That(symbols).Contains("scip-delphi delphi p 1 Main/MainForm.");
        await Assert.That(symbols).Contains("scip-delphi delphi p 1 Main/TMainForm#OkButton.");
        await Assert.That(symbols).Contains("scip-delphi delphi p 1 Main/TMainForm#OkButtonClick().");
        await Assert.That(symbols).Contains("scip-delphi delphi vcl . Vcl/StdCtrls/TButton#");
        await Assert.That(symbols).Contains("scip-delphi delphi vcl . Vcl/Controls/TControl#OnClick.");
        await Assert.That(index.Documents.All(d => d.Occurrences.All(o => o.GetSourceRange() is not null))).IsTrue();
    }

    [Test]
    public async Task FormsCanBeDisabled()
    {
        var dir = TestUtil.NewProject(new Dictionary<string, string>
        {
            ["Main.pas"] = "unit Main; interface type TMainForm = class end; implementation end.",
            ["Main.dfm"] = "object MainForm: TMainForm\nend\n",
        });
        var index = TestUtil.Index(dir, new DelphiIndexerOptions { IndexForms = false });
        await Assert.That(index.Documents.Any(d => d.RelativePath == "Main.dfm")).IsFalse();
    }
}
