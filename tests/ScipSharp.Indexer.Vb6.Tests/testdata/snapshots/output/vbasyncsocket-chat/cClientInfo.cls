'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#
 VERSION 1.0 CLASS
 BEGIN
   MultiUse = -1  'True
   Persistable = 0  'NotPersistable
   DataBindingBehavior = 0  'vbNone
   DataSourceBehavior  = 0  'vbNone
   MTSTransactionMode  = 0  'NotAnMTSObject
 END
 Attribute VB_Name = "cClientInfo"
'                     ^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#
'                                 kind Class
'                                 display_name cClientInfo
'                                 signature_documentation
'                                 > Class cClientInfo
 Attribute VB_GlobalNameSpace = False
 Attribute VB_Creatable = True
 Attribute VB_PredeclaredId = False
 Attribute VB_Exposed = False
 Option Explicit
 DefObj A-Z
 
 '=========================================================================
 ' Constants and member variables
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
 Public ID                       As String
'       ^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'          kind Field
'          display_name ID
'          signature_documentation
'          > Public ID As String
'                                        ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
 Public UserName                 As String
'       ^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
'                kind Field
'                display_name UserName
'                signature_documentation
'                > Public UserName As String
'                                        ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#Address.
 Public Address                  As String
'       ^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#Address.
'               kind Field
'               display_name Address
'               signature_documentation
'               > Public Address As String
'                                        ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#Address.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#Port.
 Public Port                     As Long
'       ^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#Port.
'            kind Field
'            display_name Port
'            signature_documentation
'            > Public Port As Long
'                                      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#Port.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
 Public WithEvents Socket        As cAsyncSocket
'                  ^^^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
'                         kind Field
'                         display_name Socket
'                         signature_documentation
'                         > Public WithEvents Socket As cAsyncSocket
'                                   ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
'                                              ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
 Attribute Socket.VB_VarHelpID = -1
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#m_oParent.
 Private m_oParent               As frmServer
'        ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#m_oParent.
'                  kind Field
'                  display_name m_oParent
'                  signature_documentation
'                  > Private m_oParent As frmServer
'                                   ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#
'                                           ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#m_oParent.
 
 '=========================================================================
 ' Methods
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#Init().
 Public Function Init(sID As String, oSocket As cAsyncSocket, oParent As frmServer)
'                ^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#Init().
'                     kind Method
'                     display_name Init
'                     signature_documentation
'                     > Public Function Init(sID As String, oSocket As cAsyncSocket, oParent As frmServer)
'                     ^^^ definition local 0
'                         kind Parameter
'                         display_name sID
'                         signature_documentation
'                         > sID As String
'                         enclosing_symbol scip-vb6 vbp Project1 1.0.0 cClientInfo#Init().
'                                    ^^^^^^^ definition local 1
'                                            kind Parameter
'                                            display_name oSocket
'                                            signature_documentation
'                                            > oSocket As cAsyncSocket
'                                            enclosing_symbol scip-vb6 vbp Project1 1.0.0 cClientInfo#Init().
'                                               ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#
'                                                             ^^^^^^^ definition local 2
'                                                                     kind Parameter
'                                                                     display_name oParent
'                                                                     signature_documentation
'                                                                     > oParent As frmServer
'                                                                     enclosing_symbol scip-vb6 vbp Project1 1.0.0 cClientInfo#Init().
'                                                                        ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#
     ID = sID
'    ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'         ^^^ reference local 0
     UserName = "Anonymous " & sID
'    ^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#UserName.
'                              ^^^ reference local 0
     oSocket.GetPeerName Address, Port
'    ^^^^^^^ reference local 1
'            ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#GetPeerName().
'                        ^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Address.
'                                 ^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Port.
     Set Socket = oSocket
'        ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
'                 ^^^^^^^ reference local 1
     Set m_oParent = oParent
'        ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#m_oParent.
'                    ^^^^^^^ reference local 2
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#Init().
 
 '=========================================================================
 ' Events
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnClose().
 Private Sub Socket_OnClose()
'            ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
'            ^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnClose().
'                           kind Method
'                           display_name Socket_OnClose
'                           signature_documentation
'                           > Private Sub Socket_OnClose()
'                           relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnClose(). reference
     m_oParent.frTcpClose ID
'    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#m_oParent.
'              ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#frTcpClose().
'                         ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnClose().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnReceive().
 Private Sub Socket_OnReceive()
'            ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
'            ^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnReceive().
'                             kind Method
'                             display_name Socket_OnReceive
'                             signature_documentation
'                             > Private Sub Socket_OnReceive()
'                             relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnReceive(). reference
     m_oParent.frTcpReceive ID, Socket
'    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#m_oParent.
'              ^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#frTcpReceive().
'                           ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'                               ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnReceive().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnError().
 Private Sub Socket_OnError(ByVal ErrorCode As Long, ByVal EventMask As UcsAsyncSocketEventMaskEnum)
'            ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
'            ^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnError().
'                           kind Method
'                           display_name Socket_OnError
'                           signature_documentation
'                           > Private Sub Socket_OnError(ByVal ErrorCode As Long, ByVal EventMask As UcsAsyncSocketEventMaskEnum)
'                           relationship scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnError(). reference
'                                 ^^^^^^^^^ definition local 3
'                                           kind Parameter
'                                           display_name ErrorCode
'                                           signature_documentation
'                                           > ByVal ErrorCode As Long
'                                           enclosing_symbol scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnError().
'                                                          ^^^^^^^^^ definition local 4
'                                                                    kind Parameter
'                                                                    display_name EventMask
'                                                                    signature_documentation
'                                                                    > ByVal EventMask As UcsAsyncSocketEventMaskEnum
'                                                                    enclosing_symbol scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnError().
'                                                                       ^^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cAsyncSocket#UcsAsyncSocketEventMaskEnum#
     m_oParent.frTcpError ID, Socket, ErrorCode, EventMask
'    ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#m_oParent.
'              ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 frmServer#frTcpError().
'                         ^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#ID.
'                             ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket.
'                                     ^^^^^^^^^ reference local 3
'                                                ^^^^^^^^^ reference local 4
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#Socket_OnError().
 
 
'⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 cClientInfo#
