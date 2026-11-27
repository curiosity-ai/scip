'⌄ enclosing_range_start scip-vb6 vbp vba . Ledger#
 Attribute VB_Name = "Ledger"
'                     ^^^^^^ definition scip-vb6 vbp vba . Ledger#
'                            kind Module
'                            display_name Ledger
'                            signature_documentation
'                            > Module Ledger
 Option Explicit
 
 ' Café prices, stored as Windows-1252 text
'⌄ enclosing_range_start scip-vb6 vbp vba . Ledger#TAUX_TVA.
 Public Const TAUX_TVA As Double = 0.2
'             ^^^^^^^^ definition scip-vb6 vbp vba . Ledger#TAUX_TVA.
'                      kind Constant
'                      display_name TAUX_TVA
'                      signature_documentation
'                      > Public Const TAUX_TVA As Double = 0.2
'                      documentation
'                      > Café prices, stored as Windows-1252 text
'                                    ⌃ enclosing_range_end scip-vb6 vbp vba . Ledger#TAUX_TVA.
'⌄ enclosing_range_start scip-vb6 vbp vba . Ledger#`prixCafé`.
 Private prixCafé As Currency
'        ^^^^^^^^ definition scip-vb6 vbp vba . Ledger#`prixCafé`.
'                 kind Variable
'                 display_name prixCafé
'                 signature_documentation
'                 > Private prixCafé As Currency
'                           ⌃ enclosing_range_end scip-vb6 vbp vba . Ledger#`prixCafé`.
 
 #If VBA7 Then
'⌄ enclosing_range_start scip-vb6 vbp vba . Ledger#GetTickCount().
 Private Declare PtrSafe Function GetTickCount Lib "kernel32" () As Long
'                                 ^^^^^^^^^^^^ definition scip-vb6 vbp vba . Ledger#GetTickCount().
'                                              kind Function
'                                              display_name GetTickCount
'                                              signature_documentation
'                                              > Private Declare PtrSafe Function GetTickCount Lib "kernel32" () As Long
'                                                                      ⌃ enclosing_range_end scip-vb6 vbp vba . Ledger#GetTickCount().
 #Else
 Private Declare Function GetTickCount Lib "kernel32" () As Long
 #End If
 
'⌄ enclosing_range_start scip-vb6 vbp vba . Ledger#PrixTTC().
 Public Function PrixTTC(ByVal montant As Currency) As Currency
'                ^^^^^^^ definition scip-vb6 vbp vba . Ledger#PrixTTC().
'                        kind Function
'                        display_name PrixTTC
'                        signature_documentation
'                        > Public Function PrixTTC(ByVal montant As Currency) As Currency
'                              ^^^^^^^ definition local 0
'                                      kind Parameter
'                                      display_name montant
'                                      signature_documentation
'                                      > ByVal montant As Currency
'                                      enclosing_symbol scip-vb6 vbp vba . Ledger#PrixTTC().
     prixCafé = montant
'    ^^^^^^^^ reference scip-vb6 vbp vba . Ledger#`prixCafé`.
'               ^^^^^^^ reference local 0
     PrixTTC = prixCafé * (1 + TAUX_TVA)
'    ^^^^^^^ reference scip-vb6 vbp vba . Ledger#PrixTTC().
'              ^^^^^^^^ reference scip-vb6 vbp vba . Ledger#`prixCafé`.
'                              ^^^^^^^^ reference scip-vb6 vbp vba . Ledger#TAUX_TVA.
 End Function
'           ⌃ enclosing_range_end scip-vb6 vbp vba . Ledger#PrixTTC().
 
'⌄ enclosing_range_start scip-vb6 vbp vba . Ledger#Recalculer().
 Sub Recalculer()
'    ^^^^^^^^^^ definition scip-vb6 vbp vba . Ledger#Recalculer().
'               kind Function
'               display_name Recalculer
'               signature_documentation
'               > Sub Recalculer()
'        ⌄ enclosing_range_start local 1
     Dim acct As New Account
'        ^^^^ definition local 1
'             kind Variable
'             display_name acct
'             signature_documentation
'             > Dim acct As New Account
'             enclosing_symbol scip-vb6 vbp vba . Ledger#Recalculer().
'                    ^^^^^^^ reference scip-vb6 vbp vba . Account#
'                          ⌃ enclosing_range_end local 1
     acct.Deposit PrixTTC(2.5@)
'    ^^^^ reference local 1
'         ^^^^^^^ reference scip-vb6 vbp vba . Account#Deposit().
'                 ^^^^^^^ reference scip-vb6 vbp vba . Ledger#PrixTTC().
     Debug.Print "Solde: "; acct.Balance; " €"
'                           ^^^^ reference local 1
'                                ^^^^^^^ reference scip-vb6 vbp vba . Account#Balance.
 End Sub
'      ⌃ enclosing_range_end scip-vb6 vbp vba . Ledger#Recalculer().
 
'⌃ enclosing_range_end scip-vb6 vbp vba . Ledger#
