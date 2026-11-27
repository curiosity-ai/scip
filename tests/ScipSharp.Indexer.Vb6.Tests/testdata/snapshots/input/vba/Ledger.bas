Attribute VB_Name = "Ledger"
Option Explicit

' Café prices, stored as Windows-1252 text
Public Const TAUX_TVA As Double = 0.2
Private prixCafé As Currency

#If VBA7 Then
Private Declare PtrSafe Function GetTickCount Lib "kernel32" () As Long
#Else
Private Declare Function GetTickCount Lib "kernel32" () As Long
#End If

Public Function PrixTTC(ByVal montant As Currency) As Currency
    prixCafé = montant
    PrixTTC = prixCafé * (1 + TAUX_TVA)
End Function

Sub Recalculer()
    Dim acct As New Account
    acct.Deposit PrixTTC(2.5@)
    Debug.Print "Solde: "; acct.Balance; " €"
End Sub
