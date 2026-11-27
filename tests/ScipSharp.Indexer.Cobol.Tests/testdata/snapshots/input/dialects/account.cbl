      $SET SOURCEFORMAT"FREE"
*> COBOL 2002 object orientation (basics) and a user-defined function.
identification division.
class-id. Account inherits Base.
environment division.
configuration section.
repository.
    class Base.
object.
data division.
working-storage section.
01 balance pic s9(9)v99 value 0.
procedure division.
method-id. deposit.
data division.
linkage section.
01 amount pic s9(9)v99.
procedure division using amount.
    add amount to balance
    goback.
end method deposit.
end object.
end class Account.

identification division.
function-id. double-it.
data division.
linkage section.
01 x pic 9(4).
01 r pic 9(5).
procedure division using x returning r.
    compute r = x * 2
    goback.
end function double-it.

identification division.
program-id. client.
data division.
working-storage section.
01 acct object reference.
01 n pic 9(5).
procedure division.
    invoke acct "deposit" using n
    compute n = function double-it(n) + function length(n)
    goback.
end program client.
