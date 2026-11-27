'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#
 VERSION 5.00
 Object = "{8405D0DF-9FDD-4829-AEAD-8E2B0A18FEA4}#1.0#0"; "Inked.dll"
 Begin VB.Form frmServer 
'         ^^^^ reference scip-vb6 vbp VB . Form#
'              ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#
    Caption         =   "Server"
    ClientHeight    =   4032
    ClientLeft      =   108
    ClientTop       =   456
    ClientWidth     =   5880
    LinkTopic       =   "Form2"
    ScaleHeight     =   4032
    ScaleWidth      =   5880
    StartUpPosition =   3  'Windows Default
    Begin INKEDLibCtl.InkEdit rchLog 
'                     ^^^^^^^ reference scip-vb6 vbp INKEDLibCtl . InkEdit#
'                             ^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
'                                    kind Field
'                                    display_name rchLog
'                                    signature_documentation
'                                    > rchLog As INKEDLibCtl.InkEdit
       Height          =   3456
       Left            =   0
       OleObjectBlob   =   "frmServer.frx":0000
       TabIndex        =   1
       Top             =   504
       Width           =   5388
    End
    Begin VB.Label labClientsCount 
'            ^^^^^ reference scip-vb6 vbp VB . Label#
'                  ^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#labClientsCount.
'                                  kind Field
'                                  display_name labClientsCount
'                                  signature_documentation
'                                  > labClientsCount As VB.Label
'                                  documentation
'                                  > Caption: 0 clients
       Caption         =   "0 clients"
       Height          =   348
       Left            =   168
       TabIndex        =   0
       Top             =   168
       Width           =   2532
    End
 End
 Attribute VB_Name = "frmServer"
'                     ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#
'                               kind Class
'                               display_name frmServer
'                               signature_documentation
'                               > Form frmServer As VB.Form
'                               documentation
'                               > Caption: Server
 Attribute VB_GlobalNameSpace = False
 Attribute VB_Creatable = False
 Attribute VB_PredeclaredId = True
 Attribute VB_Exposed = False
 Option Explicit
 DefObj A-Z
 
 '=========================================================================
 ' Constants and member variables
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
 Private m_oSocket               As cAsyncSocket
'        ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                  kind Field
'                  display_name m_oSocket
'                  signature_documentation
'                  > Private m_oSocket As cAsyncSocket
'                                   ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
'                                              ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
 Attribute m_oSocket.VB_VarHelpID = -1
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink.
 Private WithEvents m_oTcpSink   As cAsyncSocket
'                   ^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink.
'                              kind Field
'                              display_name m_oTcpSink
'                              signature_documentation
'                              > Private WithEvents m_oTcpSink As cAsyncSocket
'                                   ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
'                                              ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink.
 Attribute m_oTcpSink.VB_VarHelpID = -1
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink.
 Private WithEvents m_oUdpSink   As cAsyncSocket
'                   ^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink.
'                              kind Field
'                              display_name m_oUdpSink
'                              signature_documentation
'                              > Private WithEvents m_oUdpSink As cAsyncSocket
'                                   ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
'                                              ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink.
 Attribute m_oUdpSink.VB_VarHelpID = -1
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
 Private m_cClients              As Collection
'        ^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
'                   kind Field
'                   display_name m_cClients
'                   signature_documentation
'                   > Private m_cClients As Collection
'                                            ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#m_lCount.
 Private m_lCount                As Long
'        ^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#m_lCount.
'                 kind Field
'                 display_name m_lCount
'                 signature_documentation
'                 > Private m_lCount As Long
'                                      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#m_lCount.
 
 '=========================================================================
 ' Methods
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#Init().
 Public Function Init(ByVal lListenPort As Long, ByVal eSocketType As UcsAsyncSocketTypeEnum) As Boolean
'                ^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#Init().
'                     kind Method
'                     display_name Init
'                     signature_documentation
'                     > Public Function Init(ByVal lListenPort As Long, ByVal eSocketType As UcsAsyncSocketTypeEnum) As Boolean
'                           ^^^^^^^^^^^ definition local 0
'                                       kind Parameter
'                                       display_name lListenPort
'                                       signature_documentation
'                                       > ByVal lListenPort As Long
'                                       enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#Init().
'                                                      ^^^^^^^^^^^ definition local 1
'                                                                  kind Parameter
'                                                                  display_name eSocketType
'                                                                  signature_documentation
'                                                                  > ByVal eSocketType As UcsAsyncSocketTypeEnum
'                                                                  enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#Init().
'                                                                     ^^^^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#
'        ⌄ enclosing_range_start local 4
     Dim sAddr           As String
'        ^^^^^ definition local 4
'              kind Variable
'              display_name sAddr
'              signature_documentation
'              > Dim sAddr As String
'              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#Init().
'                                ⌃ enclosing_range_end local 4
'        ⌄ enclosing_range_start local 5
     Dim lPort           As Long
'        ^^^^^ definition local 5
'              kind Variable
'              display_name lPort
'              signature_documentation
'              > Dim lPort As Long
'              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#Init().
'                              ⌃ enclosing_range_end local 5
     
     On Error GoTo EH
'                  ^^ reference local 3
     Terminate
'    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#Terminate().
     '--- init member vars
     Set m_cClients = New Collection
'        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
     Caption = IIf(eSocketType = ucsSckStream, "TCP", "UDP") & " Chat Server"
'                  ^^^^^^^^^^^ reference local 1
'                                ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#ucsSckStream.
     Set m_oSocket = New cAsyncSocket
'        ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                        ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
     If Not m_oSocket.Create(SocketPort:=lListenPort, SocketType:=eSocketType) Then
'           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                     ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#Create().
'                                        ^^^^^^^^^^^ reference local 0
'                                                                 ^^^^^^^^^^^ reference local 1
         GoTo QH
'             ^^ reference local 2
     End If
     If eSocketType = ucsSckStream Then
'       ^^^^^^^^^^^ reference local 1
'                     ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#ucsSckStream.
         Set m_oTcpSink = m_oSocket
'            ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink.
'                         ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
         If Not m_oSocket.Listen() Then
'               ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                         ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#Listen().
             GoTo QH
'                 ^^ reference local 2
         End If
     Else
         Set m_oUdpSink = m_oSocket
'            ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink.
'                         ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
     End If
     If Not m_oSocket.GetSockName(sAddr, lPort) Then
'           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                     ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#GetSockName().
'                                 ^^^^^ reference local 4
'                                        ^^^^^ reference local 5
         GoTo QH
'             ^^ reference local 2
     End If
     RtbAppendLine rchLog, Printf("Listening on %1:%2/%3...", sAddr, lPort, IIf(eSocketType = ucsSckStream, "tcp", "udp"))
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
'                          ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                             ^^^^^ reference local 4
'                                                                    ^^^^^ reference local 5
'                                                                               ^^^^^^^^^^^ reference local 1
'                                                                                             ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketTypeEnum#ucsSckStream.
     Show
     '--- success
     Init = True
'    ^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#Init().
     Exit Function
 QH:
'^^ definition local 2
'   display_name QH
'   signature_documentation
'   > QH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#Init().
     Err.Raise vbObjectError, , Printf("Error %1: %2", m_oSocket.LastError, m_oSocket.GetErrorDescription(m_oSocket.LastError))
'                               ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                      ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                                                                ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#LastError.
'                                                                           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                                                                                     ^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#GetErrorDescription().
'                                                                                                         ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                                                                                                                   ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#LastError.
 EH:
'^^ definition local 3
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#Init().
     MsgBox Err.Description, vbCritical, "Init"
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#Init().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#Terminate().
 Public Sub Terminate()
'           ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#Terminate().
'                     kind Method
'                     display_name Terminate
'                     signature_documentation
'                     > Public Sub Terminate()
     On Error GoTo EH
'                  ^^ reference local 6
     Set m_cClients = Nothing
'        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
     Set m_oSocket = Nothing
'        ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
     Set m_oTcpSink = Nothing
'        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink.
     Set m_oUdpSink = Nothing
'        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink.
     Exit Sub
 EH:
'^^ definition local 6
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#Terminate().
     MsgBox Err.Description, vbCritical, "Terminate"
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#Terminate().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#Form_Unload().
 Private Sub Form_Unload(Cancel As Integer)
'            ^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#Form_Unload().
'                        kind Method
'                        display_name Form_Unload
'                        signature_documentation
'                        > Private Sub Form_Unload(Cancel As Integer)
'                        relationship scip-vb6 vbp VB . Form#Unload(). reference
'                        ^^^^^^ definition local 7
'                               kind Parameter
'                               display_name Cancel
'                               signature_documentation
'                               > Cancel As Integer
'                               enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#Form_Unload().
     Terminate
'    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#Terminate().
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#Form_Unload().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#Form_Resize().
 Private Sub Form_Resize()
'            ^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#Form_Resize().
'                        kind Method
'                        display_name Form_Resize
'                        signature_documentation
'                        > Private Sub Form_Resize()
'                        relationship scip-vb6 vbp VB . Form#Resize(). reference
     On Error Resume Next
     rchLog.Move 0, rchLog.Top, ScaleWidth, ScaleHeight - rchLog.Top
'    ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
'                   ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
'                                                         ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#Form_Resize().
 
 '= common ================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
 Private Sub pvClientSend(sMsg As String, Optional SkipID As String)
'            ^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
'                         kind Method
'                         display_name pvClientSend
'                         signature_documentation
'                         > Private Sub pvClientSend(sMsg As String, Optional SkipID As String)
'                         ^^^^ definition local 8
'                              kind Parameter
'                              display_name sMsg
'                              signature_documentation
'                              > sMsg As String
'                              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
'                                                  ^^^^^^ definition local 9
'                                                         kind Parameter
'                                                         display_name SkipID
'                                                         signature_documentation
'                                                         > Optional SkipID As String
'                                                         enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
'        ⌄ enclosing_range_start local 11
     Dim oClient         As cClientInfo
'        ^^^^^^^ definition local 11
'                kind Variable
'                display_name oClient
'                signature_documentation
'                > Dim oClient As cClientInfo
'                enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
'                           ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#
'                                     ⌃ enclosing_range_end local 11
     
     On Error GoTo EH
'                  ^^ reference local 10
     For Each oClient In m_cClients
'             ^^^^^^^ reference local 11
'                        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
         If oClient.ID <> SkipID Then
'           ^^^^^^^ reference local 11
'                   ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'                         ^^^^^^ reference local 9
             If Not oClient.Socket Is Nothing Then
'                   ^^^^^^^ reference local 11
'                           ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
                 oClient.Socket.SendText sMsg
'                ^^^^^^^ reference local 11
'                        ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
'                               ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#SendText().
'                                        ^^^^ reference local 8
             Else
                 m_oUdpSink.SendText sMsg, oClient.Address, oClient.Port
'                ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink.
'                           ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#SendText().
'                                    ^^^^ reference local 8
'                                          ^^^^^^^ reference local 11
'                                                  ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Address.
'                                                           ^^^^^^^ reference local 11
'                                                                   ^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Port.
             End If
         End If
     Next
     Exit Sub
 EH:
'^^ definition local 10
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
     RtbAppendLine rchLog, "Critical: " & Err.Description & " (ClientSend)"
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
 Private Sub pvClientReceive(baBuffer() As Byte, oClient As cClientInfo, sKey As String)
'            ^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
'                            kind Method
'                            display_name pvClientReceive
'                            signature_documentation
'                            > Private Sub pvClientReceive(baBuffer() As Byte, oClient As cClientInfo, sKey As String)
'                            ^^^^^^^^ definition local 12
'                                     kind Parameter
'                                     display_name baBuffer
'                                     signature_documentation
'                                     > baBuffer() As Byte
'                                     enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
'                                                ^^^^^^^ definition local 13
'                                                        kind Parameter
'                                                        display_name oClient
'                                                        signature_documentation
'                                                        > oClient As cClientInfo
'                                                        enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
'                                                           ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#
'                                                                        ^^^^ definition local 14
'                                                                             kind Parameter
'                                                                             display_name sKey
'                                                                             signature_documentation
'                                                                             > sKey As String
'                                                                             enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
'        ⌄ enclosing_range_start local 16
     Dim sMsg            As String
'        ^^^^ definition local 16
'             kind Variable
'             display_name sMsg
'             signature_documentation
'             > Dim sMsg As String
'             enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
'                                ⌃ enclosing_range_end local 16
'        ⌄ enclosing_range_start local 17
     Dim sUserName       As String
'        ^^^^^^^^^ definition local 17
'                  kind Variable
'                  display_name sUserName
'                  signature_documentation
'                  > Dim sUserName As String
'                  enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
'                                ⌃ enclosing_range_end local 17
     
     On Error GoTo EH
'                  ^^ reference local 15
     sMsg = m_oSocket.FromTextArray(baBuffer)
'    ^^^^ reference local 16
'           ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                     ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#FromTextArray().
'                                   ^^^^^^^^ reference local 12
     If baBuffer(0) = 1 Then
'       ^^^^^^^^ reference local 12
         If Len(sMsg) > 1 Then
'               ^^^^ reference local 16
             sUserName = Mid$(sMsg, 2)
'            ^^^^^^^^^ reference local 17
'                             ^^^^ reference local 16
         Else
             sUserName = "Anonymous " & oClient.ID
'            ^^^^^^^^^ reference local 17
'                                       ^^^^^^^ reference local 13
'                                               ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
         End If
         If oClient.UserName <> sUserName Then
'           ^^^^^^^ reference local 13
'                   ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
'                               ^^^^^^^^^ reference local 17
             oClient.UserName = sUserName
'            ^^^^^^^ reference local 13
'                    ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
'                               ^^^^^^^^^ reference local 17
             RtbAppendLine rchLog, Printf("Client %1 set name to %2", oClient.ID, oClient.UserName)
'            ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                          ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
'                                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                                     ^^^^^^^ reference local 13
'                                                                             ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'                                                                                 ^^^^^^^ reference local 13
'                                                                                         ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
         End If
         pvClientSend Printf("%1 connected from %2", sUserName, oClient.Address & ":" & oClient.Port)
'        ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
'                     ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                    ^^^^^^^^^ reference local 17
'                                                               ^^^^^^^ reference local 13
'                                                                       ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Address.
'                                                                                       ^^^^^^^ reference local 13
'                                                                                               ^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Port.
     ElseIf baBuffer(0) = 2 Then
'           ^^^^^^^^ reference local 12
         frTcpClose sKey
'        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#frTcpClose().
'                   ^^^^ reference local 14
     Else
         pvClientSend Printf("[%1]: %2", oClient.UserName, sMsg), oClient.ID
'        ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
'                     ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                        ^^^^^^^ reference local 13
'                                                ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
'                                                          ^^^^ reference local 16
'                                                                 ^^^^^^^ reference local 13
'                                                                         ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
     End If
     Exit Sub
 EH:
'^^ definition local 15
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
     RtbAppendLine rchLog, "Critical: " & Err.Description & " (ClientReceive)"
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
 
 '= TCP clients ===========================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink_OnAccept().
 Private Sub m_oTcpSink_OnAccept()
'            ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink.
'            ^^^^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink_OnAccept().
'                                kind Method
'                                display_name m_oTcpSink_OnAccept
'                                signature_documentation
'                                > Private Sub m_oTcpSink_OnAccept()
'                                relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnAccept(). reference
'        ⌄ enclosing_range_start local 19
     Dim oSocket         As cAsyncSocket
'        ^^^^^^^ definition local 19
'                kind Variable
'                display_name oSocket
'                signature_documentation
'                > Dim oSocket As cAsyncSocket
'                enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink_OnAccept().
'                           ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
'                                      ⌃ enclosing_range_end local 19
'        ⌄ enclosing_range_start local 20
     Dim oClient         As cClientInfo
'        ^^^^^^^ definition local 20
'                kind Variable
'                display_name oClient
'                signature_documentation
'                > Dim oClient As cClientInfo
'                enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink_OnAccept().
'                           ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#
'                                     ⌃ enclosing_range_end local 20
'        ⌄ enclosing_range_start local 21
     Dim sKey            As String
'        ^^^^ definition local 21
'             kind Variable
'             display_name sKey
'             signature_documentation
'             > Dim sKey As String
'             enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink_OnAccept().
'                                ⌃ enclosing_range_end local 21
     
     On Error GoTo EH
'                  ^^ reference local 18
     Set oSocket = New cAsyncSocket
'        ^^^^^^^ reference local 19
'                      ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
     m_oSocket.Accept oSocket
'    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'              ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#Accept().
'                     ^^^^^^^ reference local 19
     m_lCount = m_lCount + 1
'    ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_lCount.
'               ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_lCount.
     sKey = "#" & m_lCount
'    ^^^^ reference local 21
'                 ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_lCount.
     Set oClient = New cClientInfo
'        ^^^^^^^ reference local 20
'                      ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#
     oClient.Init sKey, oSocket, Me
'    ^^^^^^^ reference local 20
'            ^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Init().
'                 ^^^^ reference local 21
'                       ^^^^^^^ reference local 19
     m_cClients.Add oClient, sKey
'    ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
'                   ^^^^^^^ reference local 20
'                            ^^^^ reference local 21
     RtbAppendLine rchLog, Printf("Client %1 connected from %2", oClient.ID, oClient.Address & ":" & oClient.Port)
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
'                          ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                                ^^^^^^^ reference local 20
'                                                                        ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'                                                                            ^^^^^^^ reference local 20
'                                                                                    ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Address.
'                                                                                                    ^^^^^^^ reference local 20
'                                                                                                            ^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Port.
     labClientsCount.Caption = Printf("%1 clients", m_cClients.Count)
'    ^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#labClientsCount.
'                              ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                   ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
     Exit Sub
 EH:
'^^ definition local 18
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink_OnAccept().
     RtbAppendLine rchLog, "Critical: " & Err.Description & " (OnAccept)"
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#m_oTcpSink_OnAccept().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#frTcpReceive().
 Friend Sub frTcpReceive(sKey As String, oSocket As cAsyncSocket)
'           ^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#frTcpReceive().
'                        kind Method
'                        display_name frTcpReceive
'                        signature_documentation
'                        > Friend Sub frTcpReceive(sKey As String, oSocket As cAsyncSocket)
'                        ^^^^ definition local 22
'                             kind Parameter
'                             display_name sKey
'                             signature_documentation
'                             > sKey As String
'                             enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpReceive().
'                                        ^^^^^^^ definition local 23
'                                                kind Parameter
'                                                display_name oSocket
'                                                signature_documentation
'                                                > oSocket As cAsyncSocket
'                                                enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpReceive().
'                                                   ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
'        ⌄ enclosing_range_start local 25
     Dim baBuffer()      As Byte
'        ^^^^^^^^ definition local 25
'                 kind Variable
'                 display_name baBuffer
'                 signature_documentation
'                 > Dim baBuffer() As Byte
'                 enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpReceive().
'                              ⌃ enclosing_range_end local 25
'        ⌄ enclosing_range_start local 26
     Dim oClient         As cClientInfo
'        ^^^^^^^ definition local 26
'                kind Variable
'                display_name oClient
'                signature_documentation
'                > Dim oClient As cClientInfo
'                enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpReceive().
'                           ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#
'                                     ⌃ enclosing_range_end local 26
     
     On Error GoTo EH
'                  ^^ reference local 24
     If SearchCollection(m_cClients, sKey, RetVal:=oClient) Then
'       ^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
'                        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
'                                    ^^^^ reference local 22
'                                                  ^^^^^^^ reference local 26
         Do While oSocket.ReceiveArray(baBuffer)
'                 ^^^^^^^ reference local 23
'                         ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#ReceiveArray().
'                                      ^^^^^^^^ reference local 25
             If UBound(baBuffer) < 0 Then
'                      ^^^^^^^^ reference local 25
                 Exit Do
             End If
             pvClientReceive baBuffer, oClient, sKey
'            ^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
'                            ^^^^^^^^ reference local 25
'                                      ^^^^^^^ reference local 26
'                                               ^^^^ reference local 22
         Loop
     End If
     Exit Sub
 EH:
'^^ definition local 24
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpReceive().
     RtbAppendLine rchLog, "Critical: " & Err.Description & " (TcpReceive)"
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#frTcpReceive().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#frTcpClose().
 Friend Sub frTcpClose(sKey As String)
'           ^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#frTcpClose().
'                      kind Method
'                      display_name frTcpClose
'                      signature_documentation
'                      > Friend Sub frTcpClose(sKey As String)
'                      ^^^^ definition local 27
'                           kind Parameter
'                           display_name sKey
'                           signature_documentation
'                           > sKey As String
'                           enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpClose().
'        ⌄ enclosing_range_start local 29
     Dim oClient         As cClientInfo
'        ^^^^^^^ definition local 29
'                kind Variable
'                display_name oClient
'                signature_documentation
'                > Dim oClient As cClientInfo
'                enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpClose().
'                           ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#
'                                     ⌃ enclosing_range_end local 29
     
     On Error GoTo EH
'                  ^^ reference local 28
     If SearchCollection(m_cClients, sKey, RetVal:=oClient) Then
'       ^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
'                        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
'                                    ^^^^ reference local 27
'                                                  ^^^^^^^ reference local 29
         m_cClients.Remove sKey
'        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
'                          ^^^^ reference local 27
         pvClientSend Printf("%1 disconnected", oClient.UserName)
'        ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#pvClientSend().
'                     ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                               ^^^^^^^ reference local 29
'                                                       ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
         RtbAppendLine rchLog, Printf("Client %1 disconnected", oClient.ID)
'        ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                      ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
'                              ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                               ^^^^^^^ reference local 29
'                                                                       ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
         labClientsCount.Caption = Printf("%1 clients", m_cClients.Count)
'        ^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#labClientsCount.
'                                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                       ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
     End If
     Exit Sub
 EH:
'^^ definition local 28
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpClose().
     RtbAppendLine rchLog, "Critical: " & Err.Description & " (TcpClose)"
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#frTcpClose().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
 Friend Sub frTcpError(sKey As String, oSocket As cAsyncSocket, ByVal ErrorCode As Long, ByVal EventMask As UcsAsyncSocketEventMaskEnum)
'           ^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
'                      kind Method
'                      display_name frTcpError
'                      signature_documentation
'                      > Friend Sub frTcpError(sKey As String, oSocket As cAsyncSocket, ByVal ErrorCode As Long, ByVal EventMask As UcsAsyncSocketEventMaskEnum)
'                      ^^^^ definition local 30
'                           kind Parameter
'                           display_name sKey
'                           signature_documentation
'                           > sKey As String
'                           enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
'                                      ^^^^^^^ definition local 31
'                                              kind Parameter
'                                              display_name oSocket
'                                              signature_documentation
'                                              > oSocket As cAsyncSocket
'                                              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
'                                                 ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
'                                                                     ^^^^^^^^^ definition local 32
'                                                                               kind Parameter
'                                                                               display_name ErrorCode
'                                                                               signature_documentation
'                                                                               > ByVal ErrorCode As Long
'                                                                               enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
'                                                                                              ^^^^^^^^^ definition local 33
'                                                                                                        kind Parameter
'                                                                                                        display_name EventMask
'                                                                                                        signature_documentation
'                                                                                                        > ByVal EventMask As UcsAsyncSocketEventMaskEnum
'                                                                                                        enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
'                                                                                                           ^^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketEventMaskEnum#
'        ⌄ enclosing_range_start local 35
     Dim oClient         As cClientInfo
'        ^^^^^^^ definition local 35
'                kind Variable
'                display_name oClient
'                signature_documentation
'                > Dim oClient As cClientInfo
'                enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
'                           ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#
'                                     ⌃ enclosing_range_end local 35
     
     On Error GoTo EH
'                  ^^ reference local 34
     If SearchCollection(m_cClients, sKey, RetVal:=oClient) Then
'       ^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
'                        ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
'                                    ^^^^ reference local 30
'                                                  ^^^^^^^ reference local 35
         RtbAppendLine rchLog, Printf("Client %1 error %2 during %3" & vbCrLf & "%4", oClient.ID, ErrorCode, EventMask, _
'        ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                      ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
'                              ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                                                     ^^^^^^^ reference local 35
'                                                                                             ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'                                                                                                 ^^^^^^^^^ reference local 32
'                                                                                                            ^^^^^^^^^ reference local 33
             oSocket.GetErrorDescription(ErrorCode))
'            ^^^^^^^ reference local 31
'                    ^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#GetErrorDescription().
'                                        ^^^^^^^^^ reference local 32
     End If
     Exit Sub
 EH:
'^^ definition local 34
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
     RtbAppendLine rchLog, "Critical: " & Err.Description & " (TcpError)"
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
 
 '= UDP clients ===========================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink_OnReceive().
 Private Sub m_oUdpSink_OnReceive()
'            ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink.
'            ^^^^^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink_OnReceive().
'                                 kind Method
'                                 display_name m_oUdpSink_OnReceive
'                                 signature_documentation
'                                 > Private Sub m_oUdpSink_OnReceive()
'                                 relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnReceive(). reference
'        ⌄ enclosing_range_start local 37
     Dim baBuffer()      As Byte
'        ^^^^^^^^ definition local 37
'                 kind Variable
'                 display_name baBuffer
'                 signature_documentation
'                 > Dim baBuffer() As Byte
'                 enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink_OnReceive().
'                              ⌃ enclosing_range_end local 37
'        ⌄ enclosing_range_start local 38
     Dim lPort           As Long
'        ^^^^^ definition local 38
'              kind Variable
'              display_name lPort
'              signature_documentation
'              > Dim lPort As Long
'              enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink_OnReceive().
'                              ⌃ enclosing_range_end local 38
'        ⌄ enclosing_range_start local 39
     Dim sAddress        As String
'        ^^^^^^^^ definition local 39
'                 kind Variable
'                 display_name sAddress
'                 signature_documentation
'                 > Dim sAddress As String
'                 enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink_OnReceive().
'                                ⌃ enclosing_range_end local 39
'        ⌄ enclosing_range_start local 40
     Dim oClient         As cClientInfo
'        ^^^^^^^ definition local 40
'                kind Variable
'                display_name oClient
'                signature_documentation
'                > Dim oClient As cClientInfo
'                enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink_OnReceive().
'                           ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#
'                                     ⌃ enclosing_range_end local 40
'        ⌄ enclosing_range_start local 41
     Dim sKey            As String
'        ^^^^ definition local 41
'             kind Variable
'             display_name sKey
'             signature_documentation
'             > Dim sKey As String
'             enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink_OnReceive().
'                                ⌃ enclosing_range_end local 41
     
     On Error GoTo EH
'                  ^^ reference local 36
     If m_oSocket.ReceiveArray(baBuffer, sAddress, lPort) Then
'       ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_oSocket.
'                 ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#ReceiveArray().
'                              ^^^^^^^^ reference local 37
'                                        ^^^^^^^^ reference local 39
'                                                  ^^^^^ reference local 38
         sKey = sAddress & ":" & lPort
'        ^^^^ reference local 41
'               ^^^^^^^^ reference local 39
'                                ^^^^^ reference local 38
         If Not SearchCollection(m_cClients, sKey, RetVal:=oClient) Then
'               ^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
'                                ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
'                                            ^^^^ reference local 41
'                                                          ^^^^^^^ reference local 40
             Set oClient = New cClientInfo
'                ^^^^^^^ reference local 40
'                              ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#
             m_lCount = m_lCount + 1
'            ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_lCount.
'                       ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_lCount.
             oClient.ID = "#" & m_lCount
'            ^^^^^^^ reference local 40
'                    ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'                               ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_lCount.
             oClient.UserName = "Anonymous " & oClient.ID
'            ^^^^^^^ reference local 40
'                    ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
'                                              ^^^^^^^ reference local 40
'                                                      ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
             oClient.Address = sAddress
'            ^^^^^^^ reference local 40
'                    ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Address.
'                              ^^^^^^^^ reference local 39
             oClient.Port = lPort
'            ^^^^^^^ reference local 40
'                    ^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Port.
'                           ^^^^^ reference local 38
             m_cClients.Add oClient, sKey
'            ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#m_cClients.
'                           ^^^^^^^ reference local 40
'                                    ^^^^ reference local 41
         End If
         pvClientReceive baBuffer, oClient, sKey
'        ^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#pvClientReceive().
'                        ^^^^^^^^ reference local 37
'                                  ^^^^^^^ reference local 40
'                                           ^^^^ reference local 41
     End If
     Exit Sub
 EH:
'^^ definition local 36
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink_OnReceive().
     RtbAppendLine rchLog, "Critical: " & Err.Description & " (OnReceive)"
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                  ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#rchLog.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#m_oUdpSink_OnReceive().
 
 
'⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 frmServer#
