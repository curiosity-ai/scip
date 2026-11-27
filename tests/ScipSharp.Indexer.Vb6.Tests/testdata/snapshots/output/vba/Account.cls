'⌄ enclosing_range_start scip-vb6 vbp vba . Account#
 VERSION 1.0 CLASS
 BEGIN
   MultiUse = -1  'True
 END
 Attribute VB_Name = "Account"
'                     ^^^^^^^ definition scip-vb6 vbp vba . Account#
'                             kind Class
'                             display_name Account
'                             signature_documentation
'                             > Class Account
 Attribute VB_GlobalNameSpace = False
 Attribute VB_Creatable = False
 Attribute VB_PredeclaredId = False
 Attribute VB_Exposed = False
 Option Explicit
 
'⌄ enclosing_range_start scip-vb6 vbp vba . Account#mBalance.
 Private mBalance As Currency
'        ^^^^^^^^ definition scip-vb6 vbp vba . Account#mBalance.
'                 kind Field
'                 display_name mBalance
'                 signature_documentation
'                 > Private mBalance As Currency
'                           ⌃ enclosing_range_end scip-vb6 vbp vba . Account#mBalance.
 
'⌄ enclosing_range_start scip-vb6 vbp vba . Account#Deposit().
 Public Sub Deposit(ByVal amount As Currency)
'           ^^^^^^^ definition scip-vb6 vbp vba . Account#Deposit().
'                   kind Method
'                   display_name Deposit
'                   signature_documentation
'                   > Public Sub Deposit(ByVal amount As Currency)
'                         ^^^^^^ definition local 0
'                                kind Parameter
'                                display_name amount
'                                signature_documentation
'                                > ByVal amount As Currency
'                                enclosing_symbol scip-vb6 vbp vba . Account#Deposit().
     If amount <= 0 Then Err.Raise 5, "Account", "Montant négatif"
'       ^^^^^^ reference local 0
     mBalance = mBalance + amount
'    ^^^^^^^^ reference scip-vb6 vbp vba . Account#mBalance.
'               ^^^^^^^^ reference scip-vb6 vbp vba . Account#mBalance.
'                          ^^^^^^ reference local 0
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp vba . Account#Deposit().
 
'⌄ enclosing_range_start scip-vb6 vbp vba . Account#Balance.
 Public Property Get Balance() As Currency
'                    ^^^^^^^ definition scip-vb6 vbp vba . Account#Balance.
'                            kind Property
'                            display_name Balance
'                            signature_documentation
'                            > Public Property Get Balance() As Currency
     Balance = mBalance
'    ^^^^^^^ reference scip-vb6 vbp vba . Account#Balance.
'              ^^^^^^^^ reference scip-vb6 vbp vba . Account#mBalance.
 End Property
'           ⌃ enclosing_range_end scip-vb6 vbp vba . Account#Balance.
 
'⌃ enclosing_range_end scip-vb6 vbp vba . Account#
