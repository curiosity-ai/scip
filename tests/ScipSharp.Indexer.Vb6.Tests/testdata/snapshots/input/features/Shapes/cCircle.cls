VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "cCircle"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = True
Option Explicit
Implements IShape

'' Raised when the radius changes
Public Event Changed(ByVal OldRadius As Double)

Private m_dblRadius As Double

Private Sub Class_Initialize()
    m_dblRadius = 1
End Sub

'' The radius of the circle
Public Property Get Radius() As Double
Attribute Radius.VB_Description = "Radius in twips"
    Radius = m_dblRadius
End Property

Public Property Let Radius(ByVal Value As Double)
    Dim old As Double
    old = m_dblRadius
    m_dblRadius = Value
    RaiseEvent Changed(old)
End Property

Public Property Get Kind() As ShapeKind
    Kind = skCircle
End Property

Private Function IShape_Area() As Double
    IShape_Area = 3.14159 * Me.Radius ^ 2
End Function

Private Property Get IShape_Kind() As ShapeKind
    IShape_Kind = Kind
End Property
