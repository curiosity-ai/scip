'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 cCircle#
 VERSION 1.0 CLASS
 BEGIN
   MultiUse = -1  'True
 END
 Attribute VB_Name = "cCircle"
'                     ^^^^^^^ definition scip-vb6 vbp Features 1.2.3 cCircle#
'                             kind Class
'                             display_name cCircle
'                             signature_documentation
'                             > Class cCircle
'                             > Implements IShape
'                             relationship scip-vb6 vbp Features 1.2.3 IShape# implementation
 Attribute VB_GlobalNameSpace = False
 Attribute VB_Creatable = True
 Attribute VB_PredeclaredId = False
 Attribute VB_Exposed = True
 Option Explicit
 Implements IShape
'           ^^^^^^ reference scip-vb6 vbp Features 1.2.3 IShape#
 
 '' Raised when the radius changes
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 cCircle#Changed().
 Public Event Changed(ByVal OldRadius As Double)
'             ^^^^^^^ definition scip-vb6 vbp Features 1.2.3 cCircle#Changed().
'                     kind Event
'                     display_name Changed
'                     signature_documentation
'                     > Public Event Changed(ByVal OldRadius As Double)
'                     documentation
'                     > Raised when the radius changes
'                           ^^^^^^^^^ definition local 0
'                                     kind Parameter
'                                     display_name OldRadius
'                                     signature_documentation
'                                     > ByVal OldRadius As Double
'                                     enclosing_symbol scip-vb6 vbp Features 1.2.3 cCircle#Changed().
'                                              ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 cCircle#Changed().
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 cCircle#m_dblRadius.
 Private m_dblRadius As Double
'        ^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 cCircle#m_dblRadius.
'                    kind Field
'                    display_name m_dblRadius
'                    signature_documentation
'                    > Private m_dblRadius As Double
'                            ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 cCircle#m_dblRadius.
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 cCircle#Class_Initialize().
 Private Sub Class_Initialize()
'            ^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 cCircle#Class_Initialize().
'                             kind Method
'                             display_name Class_Initialize
'                             signature_documentation
'                             > Private Sub Class_Initialize()
     m_dblRadius = 1
'    ^^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#m_dblRadius.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 cCircle#Class_Initialize().
 
 '' The radius of the circle
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 cCircle#Radius.
 Public Property Get Radius() As Double
'                    ^^^^^^ definition scip-vb6 vbp Features 1.2.3 cCircle#Radius.
'                           kind Property
'                           display_name Radius
'                           signature_documentation
'                           > Public Property Get Radius() As Double
'                           > Public Property Let Radius(ByVal Value As Double)
'                           documentation
'                           > Radius in twips
'                           documentation
'                           > The radius of the circle
 Attribute Radius.VB_Description = "Radius in twips"
     Radius = m_dblRadius
'    ^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#Radius.
'             ^^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#m_dblRadius.
 End Property
'           ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 cCircle#Radius.
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 cCircle#Radius.
 Public Property Let Radius(ByVal Value As Double)
'                    ^^^^^^ definition scip-vb6 vbp Features 1.2.3 cCircle#Radius.
'                           kind Property
'                           display_name Radius
'                           signature_documentation
'                           > Public Property Get Radius() As Double
'                           > Public Property Let Radius(ByVal Value As Double)
'                           documentation
'                           > Radius in twips
'                           documentation
'                           > The radius of the circle
'                                 ^^^^^ definition local 1
'                                       kind Parameter
'                                       display_name Value
'                                       signature_documentation
'                                       > ByVal Value As Double
'                                       enclosing_symbol scip-vb6 vbp Features 1.2.3 cCircle#Radius.
'        ⌄ enclosing_range_start local 2
     Dim old As Double
'        ^^^ definition local 2
'            kind Variable
'            display_name old
'            signature_documentation
'            > Dim old As Double
'            enclosing_symbol scip-vb6 vbp Features 1.2.3 cCircle#Radius.
'                    ⌃ enclosing_range_end local 2
     old = m_dblRadius
'    ^^^ reference local 2
'          ^^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#m_dblRadius.
     m_dblRadius = Value
'    ^^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#m_dblRadius.
'                  ^^^^^ reference local 1
     RaiseEvent Changed(old)
'               ^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#Changed().
'                       ^^^ reference local 2
 End Property
'           ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 cCircle#Radius.
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 cCircle#Kind.
 Public Property Get Kind() As ShapeKind
'                    ^^^^ definition scip-vb6 vbp Features 1.2.3 cCircle#Kind.
'                         kind Property
'                         display_name Kind
'                         signature_documentation
'                         > Public Property Get Kind() As ShapeKind
'                              ^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#
     Kind = skCircle
'    ^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#Kind.
'           ^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#skCircle.
 End Property
'           ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 cCircle#Kind.
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 cCircle#IShape_Area().
 Private Function IShape_Area() As Double
'                 ^^^^^^ reference scip-vb6 vbp Features 1.2.3 IShape#
'                 ^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 cCircle#IShape_Area().
'                             kind Method
'                             display_name IShape_Area
'                             signature_documentation
'                             > Private Function IShape_Area() As Double
'                             relationship scip-vb6 vbp Features 1.2.3 IShape#Area(). implementation
     IShape_Area = 3.14159 * Me.Radius ^ 2
'    ^^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#IShape_Area().
'                               ^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#Radius.
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 cCircle#IShape_Area().
 
'⌄ enclosing_range_start scip-vb6 vbp Features 1.2.3 cCircle#IShape_Kind.
 Private Property Get IShape_Kind() As ShapeKind
'                     ^^^^^^ reference scip-vb6 vbp Features 1.2.3 IShape#
'                     ^^^^^^^^^^^ definition scip-vb6 vbp Features 1.2.3 cCircle#IShape_Kind.
'                                 kind Property
'                                 display_name IShape_Kind
'                                 signature_documentation
'                                 > Private Property Get IShape_Kind() As ShapeKind
'                                 relationship scip-vb6 vbp Features 1.2.3 IShape#Kind. implementation
'                                      ^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 modMain#ShapeKind#
     IShape_Kind = Kind
'    ^^^^^^^^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#IShape_Kind.
'                  ^^^^ reference scip-vb6 vbp Features 1.2.3 cCircle#Kind.
 End Property
'           ⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 cCircle#IShape_Kind.
 
'⌃ enclosing_range_end scip-vb6 vbp Features 1.2.3 cCircle#
