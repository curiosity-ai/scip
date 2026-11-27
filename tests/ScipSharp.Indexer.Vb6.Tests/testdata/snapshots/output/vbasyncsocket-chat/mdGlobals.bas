'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 mdGlobals#
 Attribute VB_Name = "mdGlobals"
'                     ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 mdGlobals#
'                               kind Module
'                               display_name mdGlobals
'                               signature_documentation
'                               > Module mdGlobals
 Option Explicit
 DefObj A-Z
 
 '=========================================================================
 ' API
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 mdGlobals#WM_VSCROLL.
 Private Const WM_VSCROLL                        As Long = &H115
'              ^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 mdGlobals#WM_VSCROLL.
'                         kind Constant
'                         display_name WM_VSCROLL
'                         signature_documentation
'                         > Private Const WM_VSCROLL As Long = &H115
'                                                              ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 mdGlobals#WM_VSCROLL.
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 mdGlobals#SB_BOTTOM.
 Private Const SB_BOTTOM                         As Long = 7
'              ^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 mdGlobals#SB_BOTTOM.
'                        kind Constant
'                        display_name SB_BOTTOM
'                        signature_documentation
'                        > Private Const SB_BOTTOM As Long = 7
'                                                          ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 mdGlobals#SB_BOTTOM.
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 mdGlobals#SendMessage().
 Private Declare Function SendMessage Lib "user32" Alias "SendMessageW" (ByVal hWnd As Long, ByVal wMsg As Long, ByVal wParam As Long, lParam As Any) As Long
'                         ^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 mdGlobals#SendMessage().
'                                     kind Function
'                                     display_name SendMessage
'                                     signature_documentation
'                                     > Private Declare Function SendMessage Lib "user32" Alias "SendMessageW" (ByVal hWnd As Long, ByVal wMsg As Long, ByVal wParam As Long, lParam As Any) As Long
'                                                                              ^^^^ definition local 0
'                                                                                   kind Parameter
'                                                                                   display_name hWnd
'                                                                                   signature_documentation
'                                                                                   > ByVal hWnd As Long
'                                                                                   enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#SendMessage().
'                                                                                                  ^^^^ definition local 1
'                                                                                                       kind Parameter
'                                                                                                       display_name wMsg
'                                                                                                       signature_documentation
'                                                                                                       > ByVal wMsg As Long
'                                                                                                       enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#SendMessage().
'                                                                                                                      ^^^^^^ definition local 2
'                                                                                                                             kind Parameter
'                                                                                                                             display_name wParam
'                                                                                                                             signature_documentation
'                                                                                                                             > ByVal wParam As Long
'                                                                                                                             enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#SendMessage().
'                                                                                                                                      ^^^^^^ definition local 3
'                                                                                                                                             kind Parameter
'                                                                                                                                             display_name lParam
'                                                                                                                                             signature_documentation
'                                                                                                                                             > lParam As Any
'                                                                                                                                             enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#SendMessage().
'                                                                                                                                                           ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 mdGlobals#SendMessage().
 
 '=========================================================================
 ' Functions
 '=========================================================================
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
 Public Function RtbAppendLine( _
'                ^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
'                              kind Function
'                              display_name RtbAppendLine
'                              signature_documentation
'                              > Public Function RtbAppendLine(rchCtl As InkEdit, sText As String, Optional ByVal HorAlign As SelAlignmentConstants = rtfLeft, Optional ByVal ForeColor As OLE_COLOR = vbWindowText)
             rchCtl As InkEdit, _
'            ^^^^^^ definition local 4
'                   kind Parameter
'                   display_name rchCtl
'                   signature_documentation
'                   > rchCtl As InkEdit
'                   enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
             sText As String, _
'            ^^^^^ definition local 5
'                  kind Parameter
'                  display_name sText
'                  signature_documentation
'                  > sText As String
'                  enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
             Optional ByVal HorAlign As SelAlignmentConstants = rtfLeft, _
'                           ^^^^^^^^ definition local 6
'                                    kind Parameter
'                                    display_name HorAlign
'                                    signature_documentation
'                                    > Optional ByVal HorAlign As SelAlignmentConstants
'                                    enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
             Optional ByVal ForeColor As OLE_COLOR = vbWindowText)
'                           ^^^^^^^^^ definition local 7
'                                     kind Parameter
'                                     display_name ForeColor
'                                     signature_documentation
'                                     > Optional ByVal ForeColor As OLE_COLOR
'                                     enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
     rchCtl.SelStart = &H7FFFFFFF
'    ^^^^^^ reference local 4
     rchCtl.SelAlignment = HorAlign
'    ^^^^^^ reference local 4
'                          ^^^^^^^^ reference local 6
     rchCtl.SelColor = ForeColor
'    ^^^^^^ reference local 4
'                      ^^^^^^^^^ reference local 7
     rchCtl.SelText = sText & vbCrLf
'    ^^^^^^ reference local 4
'                     ^^^^^ reference local 5
     rchCtl.SelStart = &H7FFFFFFF
'    ^^^^^^ reference local 4
     Call SendMessage(rchCtl.hWnd, WM_VSCROLL, SB_BOTTOM, ByVal 0&)
'         ^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#SendMessage().
'                     ^^^^^^ reference local 4
'                                  ^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#WM_VSCROLL.
'                                              ^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#SB_BOTTOM.
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 mdGlobals#RtbAppendLine().
 
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
 Public Function Printf(ByVal sText As String, ParamArray A() As Variant) As String
'                ^^^^^^ definition scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                       kind Function
'                       display_name Printf
'                       signature_documentation
'                       > Public Function Printf(ByVal sText As String, ParamArray A() As Variant) As String
'                             ^^^^^ definition local 8
'                                   kind Parameter
'                                   display_name sText
'                                   signature_documentation
'                                   > ByVal sText As String
'                                   enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                                         ^ definition local 9
'                                                           kind Parameter
'                                                           display_name A
'                                                           signature_documentation
'                                                           > ParamArray A() As Variant
'                                                           enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'          ⌄ enclosing_range_start local 10
     Const LNG_PRIVATE   As Long = &HE1B6 '-- U+E000 to U+F8FF - Private Use Area (PUA)
'          ^^^^^^^^^^^ definition local 10
'                      kind Constant
'                      display_name LNG_PRIVATE
'                      signature_documentation
'                      > Const LNG_PRIVATE As Long = &HE1B6
'                      enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                                       ⌃ enclosing_range_end local 10
'        ⌄ enclosing_range_start local 11
     Dim lIdx            As Long
'        ^^^^ definition local 11
'             kind Variable
'             display_name lIdx
'             signature_documentation
'             > Dim lIdx As Long
'             enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                              ⌃ enclosing_range_end local 11
     
     For lIdx = UBound(A) To LBound(A) Step -1
'        ^^^^ reference local 11
'                      ^ reference local 9
'                                   ^ reference local 9
         sText = Replace(sText, "%" & (lIdx - LBound(A) + 1), Replace(A(lIdx), "%", ChrW$(LNG_PRIVATE)))
'        ^^^^^ reference local 8
'                        ^^^^^ reference local 8
'                                      ^^^^ reference local 11
'                                                    ^ reference local 9
'                                                                     ^ reference local 9
'                                                                       ^^^^ reference local 11
'                                                                                         ^^^^^^^^^^^ reference local 10
     Next
     Printf = Replace(sText, ChrW$(LNG_PRIVATE), "%")
'    ^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
'                     ^^^^^ reference local 8
'                                  ^^^^^^^^^^^ reference local 10
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 mdGlobals#Printf().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
 Public Function SearchCollection(ByVal pCol As Object, Index As Variant, Optional RetVal As Variant) As Boolean
'                ^^^^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
'                                 kind Function
'                                 display_name SearchCollection
'                                 signature_documentation
'                                 > Public Function SearchCollection(ByVal pCol As Object, Index As Variant, Optional RetVal As Variant) As Boolean
'                                       ^^^^ definition local 12
'                                            kind Parameter
'                                            display_name pCol
'                                            signature_documentation
'                                            > ByVal pCol As Object
'                                            enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
'                                                       ^^^^^ definition local 13
'                                                             kind Parameter
'                                                             display_name Index
'                                                             signature_documentation
'                                                             > Index As Variant
'                                                             enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
'                                                                                  ^^^^^^ definition local 14
'                                                                                         kind Parameter
'                                                                                         display_name RetVal
'                                                                                         signature_documentation
'                                                                                         > Optional RetVal As Variant
'                                                                                         enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
     On Error GoTo QH
'                  ^^ reference local 15
     AssignVariant RetVal, pCol.Item(Index)
'    ^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#AssignVariant().
'                  ^^^^^^ reference local 14
'                          ^^^^ reference local 12
'                                    ^^^^^ reference local 13
     SearchCollection = True
'    ^^^^^^^^^^^^^^^^ reference scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
 QH:
'^^ definition local 15
'   display_name QH
'   signature_documentation
'   > QH:
'   enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 mdGlobals#SearchCollection().
 
'⌄ enclosing_range_start scip-vb6 vbp Project1 1.0.0 mdGlobals#AssignVariant().
 Public Sub AssignVariant(vDest As Variant, vSrc As Variant)
'           ^^^^^^^^^^^^^ definition scip-vb6 vbp Project1 1.0.0 mdGlobals#AssignVariant().
'                         kind Function
'                         display_name AssignVariant
'                         signature_documentation
'                         > Public Sub AssignVariant(vDest As Variant, vSrc As Variant)
'                         ^^^^^ definition local 16
'                               kind Parameter
'                               display_name vDest
'                               signature_documentation
'                               > vDest As Variant
'                               enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#AssignVariant().
'                                           ^^^^ definition local 17
'                                                kind Parameter
'                                                display_name vSrc
'                                                signature_documentation
'                                                > vSrc As Variant
'                                                enclosing_symbol scip-vb6 vbp Project1 1.0.0 mdGlobals#AssignVariant().
     If IsObject(vSrc) Then
'                ^^^^ reference local 17
         Set vDest = vSrc
'            ^^^^^ reference local 16
'                    ^^^^ reference local 17
     Else
         vDest = vSrc
'        ^^^^^ reference local 16
'                ^^^^ reference local 17
     End If
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 mdGlobals#AssignVariant().
 
'⌃ enclosing_range_end scip-vb6 vbp Project1 1.0.0 mdGlobals#
