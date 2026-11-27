'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 frmMain#
 VERSION 5.00
 Object = "{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0"; "MSCOMCTL.OCX"
 Begin VB.Form frmMain
'         ^^^^ reference scip-vb6 vbp VB . Form#
'              ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#
    Caption         =   "Feature tour"
    ClientHeight    =   3195
    ClientWidth     =   4680
    BeginProperty Font
       Name            =   "Tahoma"
       Size            =   8.25
    EndProperty
    StartUpPosition =   3  'Windows Default
    Begin VB.Frame fraOptions
'            ^^^^^ reference scip-vb6 vbp VB . Frame#
'                  ^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#fraOptions.
'                             kind Field
'                             display_name fraOptions
'                             signature_documentation
'                             > fraOptions As VB.Frame
'                             documentation
'                             > Caption: Options
       Caption         =   "Options"
       Height          =   1200
       Begin VB.OptionButton optKind
'               ^^^^^^^^^^^^ reference scip-vb6 vbp VB . OptionButton#
'                            ^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#optKind.
'                                    kind Field
'                                    display_name optKind
'                                    signature_documentation
'                                    > optKind() As VB.OptionButton
'                                    documentation
'                                    > Control array.
'                                    documentation
'                                    > Caption: Circle
          Caption         =   "Circle"
          Index           =   0
       End
       Begin VB.OptionButton optKind
'               ^^^^^^^^^^^^ reference scip-vb6 vbp VB . OptionButton#
'                            ^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#optKind.
'                                    kind Field
'                                    display_name optKind
'                                    signature_documentation
'                                    > optKind() As VB.OptionButton
'                                    documentation
'                                    > Control array.
'                                    documentation
'                                    > Caption: Circle
          Caption         =   "Square"
          Index           =   1
       End
    End
    Begin VB.CommandButton cmdGo
'            ^^^^^^^^^^^^^ reference scip-vb6 vbp VB . CommandButton#
'                          ^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#cmdGo.
'                                kind Field
'                                display_name cmdGo
'                                signature_documentation
'                                > cmdGo As VB.CommandButton
'                                documentation
'                                > Caption: &Go
       Caption         =   "&Go"
       Default         =   -1  'True
    End
    Begin MSComctlLib.ProgressBar pbProgress
'                     ^^^^^^^^^^^ reference scip-vb6 vbp MSComctlLib . ProgressBar#
'                                 ^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#pbProgress.
'                                            kind Field
'                                            display_name pbProgress
'                                            signature_documentation
'                                            > pbProgress As MSComctlLib.ProgressBar
       Height          =   255
    End
    Begin VB.Menu mnuFile
'            ^^^^ reference scip-vb6 vbp VB . Menu#
'                 ^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#mnuFile.
'                         kind Field
'                         display_name mnuFile
'                         signature_documentation
'                         > mnuFile As VB.Menu
'                         documentation
'                         > Caption: &File
       Caption         =   "&File"
       Begin VB.Menu mnuFileExit
'               ^^^^ reference scip-vb6 vbp VB . Menu#
'                    ^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#mnuFileExit.
'                                kind Field
'                                display_name mnuFileExit
'                                signature_documentation
'                                > mnuFileExit As VB.Menu
'                                documentation
'                                > Caption: E&xit
          Caption         =   "E&xit"
       End
    End
 End
 Attribute VB_Name = "frmMain"
'                     ^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#
'                             kind Class
'                             display_name frmMain
'                             signature_documentation
'                             > Form frmMain As VB.Form
'                             documentation
'                             > Caption: Feature tour
 Attribute VB_GlobalNameSpace = False
 Attribute VB_Creatable = False
 Attribute VB_PredeclaredId = True
 Attribute VB_Exposed = False
 Option Explicit
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle.
 Private WithEvents m_oCircle As cCircle
'                   ^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle.
'                             kind Field
'                             display_name m_oCircle
'                             signature_documentation
'                             > Private WithEvents m_oCircle As cCircle
'                                ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#
'                                      ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle.
 Attribute m_oCircle.VB_VarHelpID = -1
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 frmMain#Form_Load().
 Private Sub Form_Load()
'            ^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#Form_Load().
'                      kind Method
'                      display_name Form_Load
'                      signature_documentation
'                      > Private Sub Form_Load()
'                      relationship scip-vb6 vbp VB . Form#Load(). reference
     Set m_oCircle = New cCircle
'        ^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle.
'                        ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#
     optKind(0).Value = True
'    ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#optKind.
     Me.cmdGo.Caption = "Go!"
'       ^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#cmdGo.
     pbProgress.Value = 0
'    ^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#pbProgress.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 frmMain#Form_Load().
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 frmMain#cmdGo_Click().
 Private Sub cmdGo_Click()
'            ^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#cmdGo.
'            ^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#cmdGo_Click().
'                        kind Method
'                        display_name cmdGo_Click
'                        signature_documentation
'                        > Private Sub cmdGo_Click()
'                        relationship scip-vb6 vbp VB . CommandButton#Click(). reference
     m_oCircle.Radius = m_oCircle.Radius * 2
'    ^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle.
'              ^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#Radius.
'                       ^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle.
'                                 ^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#Radius.
     frmMain.Caption = APP_NAME & ": " & modMain.Area(m_oCircle)
'    ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#
'                      ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#APP_NAME.
'                                        ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#
'                                                ^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#Area().
'                                                     ^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 frmMain#cmdGo_Click().
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 frmMain#optKind_Click().
 Private Sub optKind_Click(Index As Integer)
'            ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#optKind.
'            ^^^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#optKind_Click().
'                          kind Method
'                          display_name optKind_Click
'                          signature_documentation
'                          > Private Sub optKind_Click(Index As Integer)
'                          relationship scip-vb6 vbp VB . OptionButton#Click(). reference
'                          ^^^^^ definition local 0
'                                kind Parameter
'                                display_name Index
'                                signature_documentation
'                                > Index As Integer
'                                enclosing_symbol scip-vb6 vbp Features 1.2.3 frmMain#optKind_Click().
     If TypeOf m_oCircle Is IShape Then cmdGo.Enabled = (Index = 0)
'              ^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle.
'                           ^^^^^^ reference scip-vb6 vbp Features 1.2.3 IShape#
'                                       ^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#cmdGo.
'                                                        ^^^^^ reference local 0
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 frmMain#optKind_Click().
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle_Changed().
 Private Sub m_oCircle_Changed(ByVal OldRadius As Double)
'            ^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle.
'            ^^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle_Changed().
'                              kind Method
'                              display_name m_oCircle_Changed
'                              signature_documentation
'                              > Private Sub m_oCircle_Changed(ByVal OldRadius As Double)
'                              relationship scip-vb6 vbp Features 1.2.3 cCircle#Changed(). reference
'                                    ^^^^^^^^^ definition local 1
'                                              kind Parameter
'                                              display_name OldRadius
'                                              signature_documentation
'                                              > ByVal OldRadius As Double
'                                              enclosing_symbol scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle_Changed().
     pbProgress.Value = OldRadius
'    ^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#pbProgress.
'                       ^^^^^^^^^ reference local 1
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 frmMain#m_oCircle_Changed().
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 frmMain#mnuFileExit_Click().
 Private Sub mnuFileExit_Click()
'            ^^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#mnuFileExit.
'            ^^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 frmMain#mnuFileExit_Click().
'                              kind Method
'                              display_name mnuFileExit_Click
'                              signature_documentation
'                              > Private Sub mnuFileExit_Click()
'                              relationship scip-vb6 vbp VB . Menu#Click(). reference
     Unload Me
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 frmMain#mnuFileExit_Click().
 
'⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 frmMain#
