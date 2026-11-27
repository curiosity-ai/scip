VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "Account"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

Private mBalance As Currency

Public Sub Deposit(ByVal amount As Currency)
    If amount <= 0 Then Err.Raise 5, "Account", "Montant négatif"
    mBalance = mBalance + amount
End Sub

Public Property Get Balance() As Currency
    Balance = mBalance
End Property
