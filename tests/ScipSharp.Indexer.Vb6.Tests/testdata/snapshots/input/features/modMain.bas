Attribute VB_Name = "modMain"
'=========================================================================
' Shared declarations of the feature tour
'=========================================================================
Option Explicit

'--- Win32 API
#If Win64 Then
Private Declare PtrSafe Function GetTickCount Lib "kernel32" () As LongLong
#Else
Public Declare Function GetTickCount Lib "kernel32" () As Long
#End If
Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" ( _
    Destination As Any, _
    Source As Any, _
    ByVal Length As Long)

''' A point in twips
Public Type POINTAPI
    X As Long   ' horizontal
    Y As Long   ' vertical
End Type

Public Enum ShapeKind
    skCircle = 1
    skSquare = &H2
    [_Last] = skSquare + 1
End Enum

Public Const APP_NAME As String = "Feature tour"
Public Const RELEASE_DATE As Date = #1/2/2003#
Global g_Origin As POINTAPI
Public g_Shapes As Collection
Private m_Count%, m_Name$

#Const USE_TRACE = USE_LOG
#If USE_TRACE Then
'' Writes a trace line (only with USE_LOG)
Public Sub Trace(ByVal Msg As String)
    Debug.Print Format$(Now, "hh:nn:ss"); " "; Msg
End Sub
#End If

Public Sub Main()
    Dim c As cCircle
    Dim pt As POINTAPI, i As Integer
    Static started As Boolean

    On Error GoTo EH
    Set g_Shapes = New Collection
    Set c = New cCircle
    c.Radius = 10: g_Shapes.Add c
    pt.X = 1: pt.Y = 2
    g_Origin = pt
    With g_Origin
        .X = .X + 1
    End With
    For i = 1 To 3
        m_Count = m_Count + Area(c)
    Next i
    Rem a Rem comment is ignored
    If c.Kind = skCircle Then Trace APP_NAME & _
        " started"
    started = True
    frmMain.Show
    m_Name = Twice(GetTickCount)
    Exit Sub
EH:
    MsgBox Err.Description, vbCritical, APP_NAME
    Resume Next
End Sub

'' Area of any shape
Public Function Area(ByVal Shape As IShape) As Double
    Area = Shape.Area()
End Function

Private Function Twice(ByVal v As Long, Optional ByVal factor As Long = 2) As Long
    Dim result() As Long
    ReDim result(0 To factor)
    ReDim Preserve result(0 To factor + 1)
    result(0) = v * factor
    Twice = result(0)
End Function
