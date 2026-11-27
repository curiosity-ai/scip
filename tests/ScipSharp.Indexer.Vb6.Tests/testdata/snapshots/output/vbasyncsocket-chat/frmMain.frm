'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmMain#
 VERSION 5.00
 Begin VB.Form frmMain 
'         ^^^^ reference scip-vb6 vbp VB . Form#
'              ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmMain#
    Caption         =   "Sample Chat"
    ClientHeight    =   3312
    ClientLeft      =   108
    ClientTop       =   456
    ClientWidth     =   4956
    LinkTopic       =   "Form1"
    ScaleHeight     =   3312
    ScaleWidth      =   4956
    StartUpPosition =   3  'Windows Default
    Begin VB.CheckBox chkUdp 
'            ^^^^^^^^ reference scip-vb6 vbp VB . CheckBox#
'                     ^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#chkUdp.
'                            kind Field
'                            display_name chkUdp
'                            signature_documentation
'                            > chkUdp As VB.CheckBox
'                            documentation
'                            > Caption: Use UDP
       Caption         =   "Use UDP"
       Height          =   192
       Left            =   588
       TabIndex        =   0
       Top             =   252
       Width           =   3204
    End
    Begin VB.TextBox txtServer 
'            ^^^^^^^ reference scip-vb6 vbp VB . TextBox#
'                    ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#txtServer.
'                              kind Field
'                              display_name txtServer
'                              signature_documentation
'                              > txtServer As VB.TextBox
       Height          =   348
       Left            =   504
       TabIndex        =   3
       Top             =   2352
       Width           =   3960
    End
    Begin VB.CommandButton Command4 
'            ^^^^^^^^^^^^^ reference scip-vb6 vbp VB . CommandButton#
'                          ^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#Command4.
'                                   kind Field
'                                   display_name Command4
'                                   signature_documentation
'                                   > Command4 As VB.CommandButton
'                                   documentation
'                                   > Caption: Client
       Caption         =   "Client"
       Height          =   432
       Left            =   504
       TabIndex        =   2
       Top             =   1512
       Width           =   1356
    End
    Begin VB.CommandButton Command3 
'            ^^^^^^^^^^^^^ reference scip-vb6 vbp VB . CommandButton#
'                          ^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#Command3.
'                                   kind Field
'                                   display_name Command3
'                                   signature_documentation
'                                   > Command3 As VB.CommandButton
'                                   documentation
'                                   > Caption: Server
       Caption         =   "Server"
       Height          =   432
       Left            =   504
       TabIndex        =   1
       Top             =   756
       Width           =   1356
    End
    Begin VB.Label Label3 
'            ^^^^^ reference scip-vb6 vbp VB . Label#
'                  ^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#Label3.
'                         kind Field
'                         display_name Label3
'                         signature_documentation
'                         > Label3 As VB.Label
'                         documentation
'                         > Caption: Server IP/Name (leave empty for local)
       Caption         =   "Server IP/Name (leave empty for local)"
       Height          =   264
       Left            =   504
       TabIndex        =   6
       Top             =   2100
       Width           =   3120
    End
    Begin VB.Label Label2 
'            ^^^^^ reference scip-vb6 vbp VB . Label#
'                  ^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#Label2.
'                         kind Field
'                         display_name Label2
'                         signature_documentation
'                         > Label2 As VB.Label
'                         documentation
'                         > Caption: (start single instance or restart)
       Caption         =   "(start single instance or restart)"
       Height          =   264
       Left            =   2016
       TabIndex        =   5
       Top             =   840
       Width           =   2868
    End
    Begin VB.Label Label1 
'            ^^^^^ reference scip-vb6 vbp VB . Label#
'                  ^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#Label1.
'                         kind Field
'                         display_name Label1
'                         signature_documentation
'                         > Label1 As VB.Label
'                         documentation
'                         > Caption: (start multiple clients)
       Caption         =   "(start multiple clients)"
       Height          =   264
       Left            =   2016
       TabIndex        =   4
       Top             =   1596
       Width           =   2868
    End
 End
 Attribute VB_Name = "frmMain"
'                     ^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#
'                             kind Class
'                             display_name frmMain
'                             signature_documentation
'                             > Form frmMain As VB.Form
'                             documentation
'                             > Caption: Sample Chat
 Attribute VB_GlobalNameSpace = False
 Attribute VB_Creatable = False
 Attribute VB_PredeclaredId = True
 Attribute VB_Exposed = False
 Option Explicit
 DefObj A-Z
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmMain#DEF_CHAT_PORT.
 Private Const DEF_CHAT_PORT     As Long = 5123
'              ^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#DEF_CHAT_PORT.
'                            kind Constant
'                            display_name DEF_CHAT_PORT
'                            signature_documentation
'                            > Private Const DEF_CHAT_PORT As Long = 5123
'                                             ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmMain#DEF_CHAT_PORT.
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmMain#Command3_Click().
 Private Sub Command3_Click()
'            ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmMain#Command3.
'            ^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#Command3_Click().
'                           kind Method
'                           display_name Command3_Click
'                           signature_documentation
'                           > Private Sub Command3_Click()
'                           relationship scip-vb6 vbp VB . CommandButton#Click(). reference
     On Error GoTo EH
'                  ^^ reference local 0
     With frmServer
'         ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#
         .Init DEF_CHAT_PORT, IIf(chkUdp.Value = vbChecked, ucsSckDatagram, ucsSckStream)
'         ^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#Init().
'              ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmMain#DEF_CHAT_PORT.
'                                 ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmMain#chkUdp.
'                                                           ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#ucsSckDatagram.
'                                                                           ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#ucsSckStream.
     End With
 EH:
'^^ definition local 0
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmMain#Command3_Click().
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmMain#Command3_Click().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmMain#Command4_Click().
 Private Sub Command4_Click()
'            ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmMain#Command4.
'            ^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#Command4_Click().
'                           kind Method
'                           display_name Command4_Click
'                           signature_documentation
'                           > Private Sub Command4_Click()
'                           relationship scip-vb6 vbp VB . CommandButton#Click(). reference
     On Error GoTo EH
'                  ^^ reference local 1
     With New frmClient
'             ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#
         .Init txtServer.Text, DEF_CHAT_PORT, IIf(chkUdp.Value = vbChecked, ucsSckDatagram, ucsSckStream)
'         ^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#Init().
'              ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmMain#txtServer.
'                              ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmMain#DEF_CHAT_PORT.
'                                                 ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmMain#chkUdp.
'                                                                           ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#ucsSckDatagram.
'                                                                                           ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#ucsSckStream.
     End With
 EH:
'^^ definition local 1
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmMain#Command4_Click().
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmMain#Command4_Click().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmMain#Form_Load().
 Private Sub Form_Load()
'            ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmMain#Form_Load().
'                      kind Method
'                      display_name Form_Load
'                      signature_documentation
'                      > Private Sub Form_Load()
'                      relationship scip-vb6 vbp VB . Form#Load(). reference
     Left = Screen.Width - Width - 1000
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmMain#Form_Load().
 
'⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmMain#
