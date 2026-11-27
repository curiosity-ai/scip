'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#
 VERSION 5.00
 Object = "{8405D0DF-9FDD-4829-AEAD-8E2B0A18FEA4}#1.0#0"; "Inked.dll"
 Begin VB.Form frmClient 
'         ^^^^ reference scip-vb6 vbp VB . Form#
'              ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#
    Caption         =   "Client"
    ClientHeight    =   4572
    ClientLeft      =   108
    ClientTop       =   456
    ClientWidth     =   5112
    LinkTopic       =   "Form3"
    ScaleHeight     =   4572
    ScaleWidth      =   5112
    StartUpPosition =   3  'Windows Default
    Begin VB.CommandButton cmdSend 
'            ^^^^^^^^^^^^^ reference scip-vb6 vbp VB . CommandButton#
'                          ^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend.
'                                  kind Field
'                                  display_name cmdSend
'                                  signature_documentation
'                                  > cmdSend As VB.CommandButton
'                                  documentation
'                                  > Caption: Send
       Caption         =   "Send"
       Default         =   -1  'True
       Height          =   432
       Left            =   3444
       TabIndex        =   1
       Top             =   3864
       Width           =   768
    End
    Begin VB.TextBox txtMsg 
'            ^^^^^^^ reference scip-vb6 vbp VB . TextBox#
'                    ^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
'                           kind Field
'                           display_name txtMsg
'                           signature_documentation
'                           > txtMsg As VB.TextBox
       BeginProperty Font 
          Name            =   "Tahoma"
          Size            =   9
          Charset         =   204
          Weight          =   400
          Underline       =   0   'False
          Italic          =   0   'False
          Strikethrough   =   0   'False
       EndProperty
       Height          =   432
       Left            =   0
       TabIndex        =   0
       Top             =   3864
       Width           =   3288
    End
    Begin INKEDLibCtl.InkEdit rchChat 
'                     ^^^^^^^ reference scip-vb6 vbp INKEDLibCtl . InkEdit#
'                             ^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#rchChat.
'                                     kind Field
'                                     display_name rchChat
'                                     signature_documentation
'                                     > rchChat As INKEDLibCtl.InkEdit
       Height          =   3624
       Left            =   0
       OleObjectBlob   =   "frmClient.frx":0000
       TabIndex        =   2
       Top             =   0
       Width           =   4716
    End
 End
 Attribute VB_Name = "frmClient"
'                     ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#
'                               kind Class
'                               display_name frmClient
'                               signature_documentation
'                               > Form frmClient As VB.Form
'                               documentation
'                               > Caption: Client
 Attribute VB_GlobalNameSpace = False
 Attribute VB_Creatable = False
 Attribute VB_PredeclaredId = True
 Attribute VB_Exposed = False
 Option Explicit
 DefObj A-Z
 
 '=========================================================================
 ' Constants and member variables
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#DARK_RED.
 Private Const DARK_RED              As Long = &H80
'              ^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#DARK_RED.
'                       kind Constant
'                       display_name DARK_RED
'                       signature_documentation
'                       > Private Const DARK_RED As Long = &H80
'                                                 ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#DARK_RED.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#DARK_GREEN.
 Private Const DARK_GREEN            As Long = &H8000&
'              ^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#DARK_GREEN.
'                         kind Constant
'                         display_name DARK_GREEN
'                         signature_documentation
'                         > Private Const DARK_GREEN As Long = &H8000&
'                                                    ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#DARK_GREEN.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#DARK_BLUE.
 Private Const DARK_BLUE             As Long = &H800000
'              ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#DARK_BLUE.
'                        kind Constant
'                        display_name DARK_BLUE
'                        signature_documentation
'                        > Private Const DARK_BLUE As Long = &H800000
'                                                     ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#DARK_BLUE.
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
 Private WithEvents m_oSocket    As cAsyncSocket
'                   ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                             kind Field
'                             display_name m_oSocket
'                             signature_documentation
'                             > Private WithEvents m_oSocket As cAsyncSocket
'                                   ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
'                                              ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
 Attribute m_oSocket.VB_VarHelpID = -1
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_sUserName.
 Private m_sUserName             As String
'        ^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_sUserName.
'                    kind Field
'                    display_name m_sUserName
'                    signature_documentation
'                    > Private m_sUserName As String
'                                        ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_sUserName.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
 Private m_sHostAddress          As String
'        ^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
'                       kind Field
'                       display_name m_sHostAddress
'                       signature_documentation
'                       > Private m_sHostAddress As String
'                                        ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostIP.
 Private m_sHostIP               As String
'        ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostIP.
'                  kind Field
'                  display_name m_sHostIP
'                  signature_documentation
'                  > Private m_sHostIP As String
'                                        ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostIP.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
 Private m_lHostPort             As Long
'        ^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
'                    kind Field
'                    display_name m_lHostPort
'                    signature_documentation
'                    > Private m_lHostPort As Long
'                                      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_bReconnect.
 Private m_bReconnect            As Boolean
'        ^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_bReconnect.
'                     kind Field
'                     display_name m_bReconnect
'                     signature_documentation
'                     > Private m_bReconnect As Boolean
'                                         ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_bReconnect.
 
 '=========================================================================
 ' Methods
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#Init().
 Public Function Init(sAddress As String, ByVal lPort As Long, ByVal eType As UcsAsyncSocketTypeEnum) As Boolean
'                ^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#Init().
'                     kind Method
'                     display_name Init
'                     signature_documentation
'                     > Public Function Init(sAddress As String, ByVal lPort As Long, ByVal eType As UcsAsyncSocketTypeEnum) As Boolean
'                     ^^^^^^^^ definition local 0
'                              kind Parameter
'                              display_name sAddress
'                              signature_documentation
'                              > sAddress As String
'                              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#Init().
'                                               ^^^^^ definition local 1
'                                                     kind Parameter
'                                                     display_name lPort
'                                                     signature_documentation
'                                                     > ByVal lPort As Long
'                                                     enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#Init().
'                                                                    ^^^^^ definition local 2
'                                                                          kind Parameter
'                                                                          display_name eType
'                                                                          signature_documentation
'                                                                          > ByVal eType As UcsAsyncSocketTypeEnum
'                                                                          enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#Init().
'                                                                             ^^^^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#
     On Error GoTo EH
'                  ^^ reference local 4
     m_sHostAddress = IIf(LenB(sAddress), sAddress, "localhost")
'    ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
'                              ^^^^^^^^ reference local 0
'                                         ^^^^^^^^ reference local 0
     m_sHostIP = m_sHostAddress
'    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostIP.
'                ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
     m_lHostPort = lPort
'    ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
'                  ^^^^^ reference local 1
     m_sUserName = InputBox("Enter your chat username (leave empty for anonymous)", "Connect to " & m_sHostAddress)
'    ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sUserName.
'                                                                                                   ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
     If StrPtr(m_sUserName) = 0 Then '--- InputBox cancelled
'              ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sUserName.
         Exit Function
     End If
     Caption = IIf(LenB(m_sUserName) <> 0, m_sUserName, "Anonymous") & " - " & IIf(eType = ucsSckStream, "TCP", "UDP") & " Chat on " & m_sHostAddress
'                       ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sUserName.
'                                          ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sUserName.
'                                                                                  ^^^^^ reference local 2
'                                                                                          ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#ucsSckStream.
'                                                                                                                                      ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
     Set m_oSocket = New cAsyncSocket
'        ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                        ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
     If Not m_oSocket.Create(SocketType:=eType) Then
'           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                     ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#Create().
'                                        ^^^^^ reference local 2
         GoTo QH
'             ^^ reference local 3
     End If
     If Not m_oSocket.Connect(m_sHostAddress, m_lHostPort) Then
'           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                     ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#Connect().
'                             ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
'                                             ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
         GoTo QH
'             ^^ reference local 3
     End If
     Show
     '--- success
     Init = True
'    ^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#Init().
     Exit Function
 QH:
'^^ definition local 3
'   display_name QH
'   signature_documentation
'   > QH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#Init().
     Err.Raise vbObjectError, , Printf("Error %1: %2", m_oSocket.LastError, m_oSocket.GetErrorDescription(m_oSocket.LastError))
'                               ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                      ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                                                                ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#LastError.
'                                                                           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                                                                                     ^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#GetErrorDescription().
'                                                                                                         ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                                                                                                                   ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#LastError.
 EH:
'^^ definition local 4
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#Init().
     MsgBox Err.Description, vbCritical
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#Init().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
 Property Get pvPeerName() As String
'             ^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
'                        kind Property
'                        display_name pvPeerName
'                        signature_documentation
'                        > Property Get pvPeerName() As String
'        ⌄ enclosing_range_start local 5
     Dim sType           As String
'        ^^^^^ definition local 5
'              kind Variable
'              display_name sType
'              signature_documentation
'              > Dim sType As String
'              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
'                                ⌃ enclosing_range_end local 5
'        ⌄ enclosing_range_start local 6
     Dim sAddr           As String
'        ^^^^^ definition local 6
'              kind Variable
'              display_name sAddr
'              signature_documentation
'              > Dim sAddr As String
'              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
'                                ⌃ enclosing_range_end local 6
'        ⌄ enclosing_range_start local 7
     Dim lPort           As Long
'        ^^^^^ definition local 7
'              kind Variable
'              display_name lPort
'              signature_documentation
'              > Dim lPort As Long
'              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
'                              ⌃ enclosing_range_end local 7
     
     If Not m_oSocket Is Nothing Then
'           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
         sType = IIf(m_oSocket.SockOpt(ucsSsoType) = ucsSckStream, "tcp", "udp")
'        ^^^^^ reference local 5
'                    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                              ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#SockOpt.
'                                      ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketOptionNameEnum#ucsSsoType.
'                                                    ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#ucsSckStream.
         If sType = "tcp" And m_oSocket.GetPeerName(sAddr, lPort) Then
'           ^^^^^ reference local 5
'                             ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                                       ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#GetPeerName().
'                                                   ^^^^^ reference local 6
'                                                          ^^^^^ reference local 7
             pvPeerName = m_sHostAddress & " (" & sAddr & ") port " & lPort & "/" & sType
'            ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
'                         ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
'                                                 ^^^^^ reference local 6
'                                                                     ^^^^^ reference local 7
'                                                                                   ^^^^^ reference local 5
         Else
             pvPeerName = m_sHostAddress & " (" & m_sHostIP & ") port " & m_lHostPort & "/" & sType
'            ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
'                         ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
'                                                 ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostIP.
'                                                                         ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
'                                                                                             ^^^^^ reference local 5
         End If
     End If
 End Property
'           ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
 
 '=========================================================================
 ' Control events
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend_Click().
 Private Sub cmdSend_Click()
'            ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend.
'            ^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend_Click().
'                          kind Method
'                          display_name cmdSend_Click
'                          signature_documentation
'                          > Private Sub cmdSend_Click()
'                          relationship scip-vb6 vbp VB . CommandButton#Click(). reference
'        ⌄ enclosing_range_start local 10
     Dim eType       As UcsAsyncSocketTypeEnum
'        ^^^^^ definition local 10
'              kind Variable
'              display_name eType
'              signature_documentation
'              > Dim eType As UcsAsyncSocketTypeEnum
'              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend_Click().
'                       ^^^^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#
'                                            ⌃ enclosing_range_end local 10
     
     On Error GoTo EH
'                  ^^ reference local 9
     If m_bReconnect Then
'       ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_bReconnect.
         '--- note: m_bReconnect cleared in OnConnect event
         eType = m_oSocket.SockOpt(ucsSsoType)
'        ^^^^^ reference local 10
'                ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                          ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#SockOpt.
'                                  ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketOptionNameEnum#ucsSsoType.
         m_oSocket.Close_
'        ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#Close_().
         If Not m_oSocket.Create(SocketType:=eType) Then
'               ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                         ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#Create().
'                                            ^^^^^ reference local 10
             GoTo QH
'                 ^^ reference local 8
         End If
         If Not m_oSocket.Connect(m_sHostAddress, m_lHostPort) Then
'               ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                         ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#Connect().
'                                 ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
'                                                 ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
             GoTo QH
'                 ^^ reference local 8
         End If
         '--- note: delay-send msg -> OnConnect event
         GoTo QH
'             ^^ reference local 8
     End If
     If LenB(txtMsg.Text) <> 0 Then
'            ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
         RtbAppendLine rchChat, "[me]: " & txtMsg.Text, ForeColor:=DARK_BLUE
'        ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                      ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#rchChat.
'                                          ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
'                                                                  ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#DARK_BLUE.
         If Not m_oSocket.SendText(txtMsg.Text, m_sHostIP, m_lHostPort) Then
'               ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                         ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#SendText().
'                                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
'                                               ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostIP.
'                                                          ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
             RtbAppendLine rchChat, Printf("Error %1 while sending" & vbCrLf & "%2", m_oSocket.LastError, m_oSocket.GetErrorDescription(m_oSocket.LastError)), _
'            ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                          ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#rchChat.
'                                   ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                                                    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                                                                                              ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#LastError.
'                                                                                                         ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                                                                                                                   ^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#GetErrorDescription().
'                                                                                                                                       ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                                                                                                                                                 ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#LastError.
                 ForeColor:=DARK_RED
'                           ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#DARK_RED.
             GoTo QH
'                 ^^ reference local 8
         End If
         txtMsg.Text = vbNullString
'        ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
     End If
     txtMsg.SetFocus
'    ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
 QH:
'^^ definition local 8
'   display_name QH
'   signature_documentation
'   > QH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend_Click().
     Exit Sub
 EH:
'^^ definition local 9
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend_Click().
     MsgBox Err.Description, vbCritical, "Send"
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend_Click().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnResolve().
 Private Sub m_oSocket_OnResolve(IpAddress As String)
'            ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'            ^^^^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnResolve().
'                                kind Method
'                                display_name m_oSocket_OnResolve
'                                signature_documentation
'                                > Private Sub m_oSocket_OnResolve(IpAddress As String)
'                                relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnResolve(). reference
'                                ^^^^^^^^^ definition local 11
'                                          kind Parameter
'                                          display_name IpAddress
'                                          signature_documentation
'                                          > IpAddress As String
'                                          enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnResolve().
     m_sHostIP = IpAddress
'    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostIP.
'                ^^^^^^^^^ reference local 11
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnResolve().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnConnect().
 Private Sub m_oSocket_OnConnect()
'            ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'            ^^^^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnConnect().
'                                kind Method
'                                display_name m_oSocket_OnConnect
'                                signature_documentation
'                                > Private Sub m_oSocket_OnConnect()
'                                relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnConnect(). reference
     On Error GoTo EH
'                  ^^ reference local 13
     RtbAppendLine rchChat, Printf("Connected to %1", pvPeerName)
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#rchChat.
'                           ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                     ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
     If Not m_oSocket.SendText(Chr$(1) & m_sUserName, m_sHostIP, m_lHostPort) Then
'           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                     ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#SendText().
'                                        ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sUserName.
'                                                     ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostIP.
'                                                                ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
         GoTo QH
'             ^^ reference local 12
     End If
     If m_bReconnect Then
'       ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_bReconnect.
         '--- msg send delayed until connected
         m_bReconnect = False
'        ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_bReconnect.
         DoEvents
         cmdSend.Value = True
'        ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend.
     End If
 QH:
'^^ definition local 12
'   display_name QH
'   signature_documentation
'   > QH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnConnect().
     Exit Sub
 EH:
'^^ definition local 13
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnConnect().
     MsgBox Err.Description, vbCritical, "OnConnect"
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnConnect().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnReceive().
 Private Sub m_oSocket_OnReceive()
'            ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'            ^^^^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnReceive().
'                                kind Method
'                                display_name m_oSocket_OnReceive
'                                signature_documentation
'                                > Private Sub m_oSocket_OnReceive()
'                                relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnReceive(). reference
'        ⌄ enclosing_range_start local 15
     Dim sMsg            As String
'        ^^^^ definition local 15
'             kind Variable
'             display_name sMsg
'             signature_documentation
'             > Dim sMsg As String
'             enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnReceive().
'                                ⌃ enclosing_range_end local 15
     
     On Error GoTo EH
'                  ^^ reference local 14
     sMsg = m_oSocket.ReceiveText()
'    ^^^^ reference local 15
'           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                     ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#ReceiveText().
     Do While LenB(sMsg) <> 0
'                  ^^^^ reference local 15
         RtbAppendLine rchChat, sMsg, HorAlign:=IIf(Left$(sMsg, 1) = "[", rtfRight, rtfCenter), _
'        ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                      ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#rchChat.
'                               ^^^^ reference local 15
'                                                         ^^^^ reference local 15
             ForeColor:=IIf(Left$(sMsg, 1) = "[", DARK_GREEN, vbButtonShadow)
'                                 ^^^^ reference local 15
'                                                 ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#DARK_GREEN.
         sMsg = m_oSocket.ReceiveText()
'        ^^^^ reference local 15
'               ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                         ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#ReceiveText().
     Loop
     Exit Sub
 EH:
'^^ definition local 14
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnReceive().
     MsgBox Err.Description, vbCritical, "OnReceive"
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnReceive().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnClose().
 Private Sub m_oSocket_OnClose() '--- raised only for TCP
'            ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'            ^^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnClose().
'                              kind Method
'                              display_name m_oSocket_OnClose
'                              signature_documentation
'                              > Private Sub m_oSocket_OnClose()
'                              relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnClose(). reference
     On Error GoTo EH
'                  ^^ reference local 16
     RtbAppendLine rchChat, "Disconnected from " & m_sHostAddress
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#rchChat.
'                                                  ^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostAddress.
     m_bReconnect = True
'    ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_bReconnect.
     Exit Sub
 EH:
'^^ definition local 16
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnClose().
     MsgBox Err.Description, vbCritical, "OnClose"
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnClose().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnError().
 Private Sub m_oSocket_OnError(ByVal ErrorCode As Long, ByVal EventMask As UcsAsyncSocketEventMaskEnum)
'            ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'            ^^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnError().
'                              kind Method
'                              display_name m_oSocket_OnError
'                              signature_documentation
'                              > Private Sub m_oSocket_OnError(ByVal ErrorCode As Long, ByVal EventMask As UcsAsyncSocketEventMaskEnum)
'                              relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnError(). reference
'                                    ^^^^^^^^^ definition local 17
'                                              kind Parameter
'                                              display_name ErrorCode
'                                              signature_documentation
'                                              > ByVal ErrorCode As Long
'                                              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnError().
'                                                             ^^^^^^^^^ definition local 18
'                                                                       kind Parameter
'                                                                       display_name EventMask
'                                                                       signature_documentation
'                                                                       > ByVal EventMask As UcsAsyncSocketEventMaskEnum
'                                                                       enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnError().
'                                                                          ^^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketEventMaskEnum#
     On Error GoTo EH
'                  ^^ reference local 19
     Select Case EventMask
'                ^^^^^^^^^ reference local 18
     Case ucsSfdConnect
'         ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketEventMaskEnum#ucsSfdConnect.
         RtbAppendLine rchChat, Printf("Error %1 connecting to %2" & vbCrLf & "%3", ErrorCode, pvPeerName, m_oSocket.GetErrorDescription(ErrorCode)), _
'        ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                      ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#rchChat.
'                               ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                                                   ^^^^^^^^^ reference local 17
'                                                                                              ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#pvPeerName.
'                                                                                                          ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                                                                                                                    ^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#GetErrorDescription().
'                                                                                                                                        ^^^^^^^^^ reference local 17
             ForeColor:=DARK_RED
'                       ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#DARK_RED.
         m_bReconnect = True
'        ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_bReconnect.
     Case Else
         RtbAppendLine rchChat, Printf("Error %1: %2", ErrorCode, m_oSocket.GetErrorDescription(ErrorCode)), _
'        ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                      ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#rchChat.
'                               ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                      ^^^^^^^^^ reference local 17
'                                                                 ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'                                                                           ^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#GetErrorDescription().
'                                                                                               ^^^^^^^^^ reference local 17
             ForeColor:=DARK_RED
'                       ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#DARK_RED.
     End Select
     Exit Sub
 EH:
'^^ definition local 19
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnError().
     MsgBox Err.Description, vbCritical, "OnError"
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket_OnError().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#Form_Unload().
 Private Sub Form_Unload(Cancel As Integer)
'            ^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#Form_Unload().
'                        kind Method
'                        display_name Form_Unload
'                        signature_documentation
'                        > Private Sub Form_Unload(Cancel As Integer)
'                        relationship scip-vb6 vbp VB . Form#Unload(). reference
'                        ^^^^^^ definition local 20
'                               kind Parameter
'                               display_name Cancel
'                               signature_documentation
'                               > Cancel As Integer
'                               enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#Form_Unload().
     On Error GoTo EH
'                  ^^ reference local 21
     '--- note: explicitly send disconnect event (esp. for UDP clients)
     m_oSocket.SendText Chr$(2), m_sHostIP, m_lHostPort
'    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_oSocket.
'              ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#SendText().
'                                ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_sHostIP.
'                                           ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#m_lHostPort.
     Exit Sub
 EH:
'^^ definition local 21
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmClient#Form_Unload().
     MsgBox Err.Description, vbCritical, "Unload"
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#Form_Unload().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmClient#Form_Resize().
 Private Sub Form_Resize()
'            ^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmClient#Form_Resize().
'                        kind Method
'                        display_name Form_Resize
'                        signature_documentation
'                        > Private Sub Form_Resize()
'                        relationship scip-vb6 vbp VB . Form#Resize(). reference
     On Error Resume Next
     txtMsg.Move 0, ScaleHeight - txtMsg.Height, ScaleWidth - cmdSend.Width - 84
'    ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
'                                 ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
'                                                             ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend.
     cmdSend.Move ScaleWidth - cmdSend.Width - 42, txtMsg.Top
'    ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend.
'                              ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#cmdSend.
'                                                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
     rchChat.Move 0, 0, ScaleWidth, txtMsg.Top - 42
'    ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#rchChat.
'                                   ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmClient#txtMsg.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#Form_Resize().
 
 
 
'⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmClient#
