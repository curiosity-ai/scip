'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#
 Attribute VB_Name = "modMain"
'                     ^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#
'                             kind Module
'                             display_name modMain
'                             signature_documentation
'                             > Module modMain
 '=========================================================================
 ' Shared declarations of the feature tour
 '=========================================================================
 Option Explicit
 
 '--- Win32 API
 #If Win64 Then
 Private Declare PtrSafe Function GetTickCount Lib "kernel32" () As LongLong
 #Else
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#GetTickCount().
 Public Declare Function GetTickCount Lib "kernel32" () As Long
'                        ^^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#GetTickCount().
'                                     kind Function
'                                     display_name GetTickCount
'                                     signature_documentation
'                                     > Public Declare Function GetTickCount Lib "kernel32" () As Long
'                                                             ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#GetTickCount().
 #End If
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#CopyMemory().
 Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" ( _
'                    ^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#CopyMemory().
'                               kind Function
'                               display_name CopyMemory
'                               signature_documentation
'                               > Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)
     Destination As Any, _
'    ^^^^^^^^^^^ definition local 0
'                kind Parameter
'                display_name Destination
'                signature_documentation
'                > Destination As Any
'                enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#CopyMemory().
     Source As Any, _
'    ^^^^^^ definition local 1
'           kind Parameter
'           display_name Source
'           signature_documentation
'           > Source As Any
'           enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#CopyMemory().
     ByVal Length As Long)
'          ^^^^^^ definition local 2
'                 kind Parameter
'                 display_name Length
'                 signature_documentation
'                 > ByVal Length As Long
'                 enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#CopyMemory().
'                        ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#CopyMemory().
 
 ''' A point in twips
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#
 Public Type POINTAPI
'            ^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#
'                     kind Struct
'                     display_name POINTAPI
'                     signature_documentation
'                     > Public Type POINTAPI
'                     documentation
'                     > A point in twips
'    ⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#X.
     X As Long   ' horizontal
'    ^ definition scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#X.
'      kind Field
'      display_name X
'      signature_documentation
'      > X As Long
'      documentation
'      > horizontal
'            ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#X.
'    ⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#Y.
     Y As Long   ' vertical
'    ^ definition scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#Y.
'      kind Field
'      display_name Y
'      signature_documentation
'      > Y As Long
'      documentation
'      > vertical
'            ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#Y.
 End Type
'       ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#
 Public Enum ShapeKind
'            ^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#
'                      kind Enum
'                      display_name ShapeKind
'                      signature_documentation
'                      > Public Enum ShapeKind
'    ⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#skCircle.
     skCircle = 1
'    ^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#skCircle.
'             kind EnumMember
'             display_name skCircle
'             signature_documentation
'             > skCircle = 1
'               ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#skCircle.
'    ⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#skSquare.
     skSquare = &H2
'    ^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#skSquare.
'             kind EnumMember
'             display_name skSquare
'             signature_documentation
'             > skSquare = &H2
'                 ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#skSquare.
'    ⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#_Last.
     [_Last] = skSquare + 1
'     ^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#_Last.
'           kind EnumMember
'           display_name _Last
'           signature_documentation
'           > [_Last] = skSquare + 1
'              ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#skSquare.
'                         ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#_Last.
 End Enum
'       ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#APP_NAME.
 Public Const APP_NAME As String = "Feature tour"
'             ^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#APP_NAME.
'                      kind Constant
'                      display_name APP_NAME
'                      signature_documentation
'                      > Public Const APP_NAME As String = "Feature tour"
'                                               ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#APP_NAME.
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#RELEASE_DATE.
 Public Const RELEASE_DATE As Date = #1/2/2003#
'             ^^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#RELEASE_DATE.
'                          kind Constant
'                          display_name RELEASE_DATE
'                          signature_documentation
'                          > Public Const RELEASE_DATE As Date = #1/2/2003#
'                                             ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#RELEASE_DATE.
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#g_Origin.
 Global g_Origin As POINTAPI
'       ^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#g_Origin.
'                kind Variable
'                display_name g_Origin
'                signature_documentation
'                > Global g_Origin As POINTAPI
'                   ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#
'                          ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#g_Origin.
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#g_Shapes.
 Public g_Shapes As Collection
'       ^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#g_Shapes.
'                kind Variable
'                display_name g_Shapes
'                signature_documentation
'                > Public g_Shapes As Collection
'                            ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#g_Shapes.
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#m_Count.
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#m_Name.
 Private m_Count%, m_Name$
'        ^^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#m_Count.
'                kind Variable
'                display_name m_Count
'                signature_documentation
'                > Private m_Count%
'                  ^^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#m_Name.
'                         kind Variable
'                         display_name m_Name
'                         signature_documentation
'                         > Private m_Name$
'               ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#m_Count.
'                        ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#m_Name.
 
 #Const USE_TRACE = USE_LOG
 #If USE_TRACE Then
 '' Writes a trace line (only with USE_LOG)
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#Trace().
 Public Sub Trace(ByVal Msg As String)
'           ^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#Trace().
'                 kind Function
'                 display_name Trace
'                 signature_documentation
'                 > Public Sub Trace(ByVal Msg As String)
'                 documentation
'                 > Writes a trace line (only with USE_LOG)
'                       ^^^ definition local 3
'                           kind Parameter
'                           display_name Msg
'                           signature_documentation
'                           > ByVal Msg As String
'                           enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Trace().
     Debug.Print Format$(Now, "hh:nn:ss"); " "; Msg
'                                               ^^^ reference local 3
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#Trace().
 #End If
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#Main().
 Public Sub Main()
'           ^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#Main().
'                kind Function
'                display_name Main
'                signature_documentation
'                > Public Sub Main()
'        ⌄ enclosing_range_start local 5
     Dim c As cCircle
'        ^ definition local 5
'          kind Variable
'          display_name c
'          signature_documentation
'          > Dim c As cCircle
'          enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Main().
'             ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#
'                   ⌃ enclosing_range_end local 5
'        ⌄ enclosing_range_start local 6
'                        ⌄ enclosing_range_start local 7
     Dim pt As POINTAPI, i As Integer
'        ^^ definition local 6
'           kind Variable
'           display_name pt
'           signature_documentation
'           > Dim pt As POINTAPI
'           enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Main().
'              ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#
'                        ^ definition local 7
'                          kind Variable
'                          display_name i
'                          signature_documentation
'                          > Dim i As Integer
'                          enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Main().
'                     ⌃ enclosing_range_end local 6
'                                   ⌃ enclosing_range_end local 7
'           ⌄ enclosing_range_start local 8
     Static started As Boolean
'           ^^^^^^^ definition local 8
'                   kind Variable
'                   display_name started
'                   signature_documentation
'                   > Static started As Boolean
'                   enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Main().
'                            ⌃ enclosing_range_end local 8
 
     On Error GoTo EH
'                  ^^ reference local 4
     Set g_Shapes = New Collection
'        ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#g_Shapes.
     Set c = New cCircle
'        ^ reference local 5
'                ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#
     c.Radius = 10: g_Shapes.Add c
'    ^ reference local 5
'      ^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#Radius.
'                   ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#g_Shapes.
'                                ^ reference local 5
     pt.X = 1: pt.Y = 2
'    ^^ reference local 6
'       ^ reference scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#X.
'              ^^ reference local 6
'                 ^ reference scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#Y.
     g_Origin = pt
'    ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#g_Origin.
'               ^^ reference local 6
     With g_Origin
'         ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#g_Origin.
         .X = .X + 1
'         ^ reference scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#X.
'              ^ reference scip-vb6 vbp Features 1.2.3 modMain#POINTAPI#X.
     End With
     For i = 1 To 3
'        ^ reference local 7
         m_Count = m_Count + Area(c)
'        ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#m_Count.
'                  ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#m_Count.
'                            ^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#Area().
'                                 ^ reference local 5
     Next i
'         ^ reference local 7
     Rem a Rem comment is ignored
     If c.Kind = skCircle Then Trace APP_NAME & _
'       ^ reference local 5
'         ^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#Kind.
'                ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#skCircle.
'                              ^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#Trace().
'                                    ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#APP_NAME.
         " started"
     started = True
'    ^^^^^^^ reference local 8
     frmMain.Show
'    ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 frmMain#
     m_Name = Twice(GetTickCount)
'    ^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#m_Name.
'             ^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#Twice().
'                   ^^^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#GetTickCount().
     Exit Sub
 EH:
'^^ definition local 4
'   display_name EH
'   signature_documentation
'   > EH:
'   enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Main().
     MsgBox Err.Description, vbCritical, APP_NAME
'                                        ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#APP_NAME.
     Resume Next
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#Main().
 
 '' Area of any shape
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#Area().
 Public Function Area(ByVal Shape As IShape) As Double
'                ^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#Area().
'                     kind Function
'                     display_name Area
'                     signature_documentation
'                     > Public Function Area(ByVal Shape As IShape) As Double
'                     documentation
'                     > Area of any shape
'                           ^^^^^ definition local 9
'                                 kind Parameter
'                                 display_name Shape
'                                 signature_documentation
'                                 > ByVal Shape As IShape
'                                 enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Area().
'                                    ^^^^^^ reference scip-vb6 vbp Features 1.2.3 IShape#
     Area = Shape.Area()
'    ^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#Area().
'           ^^^^^ reference local 9
'                 ^^^^ reference scip-vb6 vbp Features 1.2.3 IShape#Area().
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#Area().
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 modMain#Twice().
 Private Function Twice(ByVal v As Long, Optional ByVal factor As Long = 2) As Long
'                 ^^^^^ definition scip-vb6 vbp Features 1.2.3 modMain#Twice().
'                       kind Function
'                       display_name Twice
'                       signature_documentation
'                       > Private Function Twice(ByVal v As Long, Optional ByVal factor As Long = 2) As Long
'                             ^ definition local 10
'                               kind Parameter
'                               display_name v
'                               signature_documentation
'                               > ByVal v As Long
'                               enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Twice().
'                                                       ^^^^^^ definition local 11
'                                                              kind Parameter
'                                                              display_name factor
'                                                              signature_documentation
'                                                              > Optional ByVal factor As Long
'                                                              enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Twice().
'        ⌄ enclosing_range_start local 12
     Dim result() As Long
'        ^^^^^^ definition local 12
'               kind Variable
'               display_name result
'               signature_documentation
'               > Dim result() As Long
'               enclosing_symbol scip-vb6 vbp Features 1.2.3 modMain#Twice().
'                       ⌃ enclosing_range_end local 12
     ReDim result(0 To factor)
'          ^^^^^^ reference local 12
'                      ^^^^^^ reference local 11
     ReDim Preserve result(0 To factor + 1)
'                   ^^^^^^ reference local 12
'                               ^^^^^^ reference local 11
     result(0) = v * factor
'    ^^^^^^ reference local 12
'                ^ reference local 10
'                    ^^^^^^ reference local 11
     Twice = result(0)
'    ^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#Twice().
'            ^^^^^^ reference local 12
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#Twice().
 
'⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 modMain#
