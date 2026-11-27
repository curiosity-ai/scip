VERSION 5.00
Object = "{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0"; "MSCOMCTL.OCX"
Begin VB.Form frmMain
   Caption         =   "Feature tour"
   ClientHeight    =   3195
   ClientWidth     =   4680
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   8.25
   EndProperty
   StartUpPosition =   3  'Windows Default
   Begin VB.Frame fraOptions
      Caption         =   "Options"
      Height          =   1200
      Begin VB.OptionButton optKind
         Caption         =   "Circle"
         Index           =   0
      End
      Begin VB.OptionButton optKind
         Caption         =   "Square"
         Index           =   1
      End
   End
   Begin VB.CommandButton cmdGo
      Caption         =   "&Go"
      Default         =   -1  'True
   End
   Begin MSComctlLib.ProgressBar pbProgress
      Height          =   255
   End
   Begin VB.Menu mnuFile
      Caption         =   "&File"
      Begin VB.Menu mnuFileExit
         Caption         =   "E&xit"
      End
   End
End
Attribute VB_Name = "frmMain"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Option Explicit

Private WithEvents m_oCircle As cCircle
Attribute m_oCircle.VB_VarHelpID = -1

Private Sub Form_Load()
    Set m_oCircle = New cCircle
    optKind(0).Value = True
    Me.cmdGo.Caption = "Go!"
    pbProgress.Value = 0
End Sub

Private Sub cmdGo_Click()
    m_oCircle.Radius = m_oCircle.Radius * 2
    frmMain.Caption = APP_NAME & ": " & modMain.Area(m_oCircle)
End Sub

Private Sub optKind_Click(Index As Integer)
    If TypeOf m_oCircle Is IShape Then cmdGo.Enabled = (Index = 0)
End Sub

Private Sub m_oCircle_Changed(ByVal OldRadius As Double)
    pbProgress.Value = OldRadius
End Sub

Private Sub mnuFileExit_Click()
    Unload Me
End Sub
