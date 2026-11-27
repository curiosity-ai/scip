        $SET SOURCEFORMAT"FREE"
  *> COBOL 2002 object orientation (basics) and a user-defined function.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 ACCOUNT#
  identification division.
  class-id. Account inherits Base.
*>          ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 ACCOUNT#
*>                  kind Class
*>                  display_name Account
*>                  documentation
*>                  > ```cobol
*>                  > class-id. Account inherits Base
*>                  > ```
  environment division.
  configuration section.
  repository.
      class Base.
  object.
  data division.
  working-storage section.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 ACCOUNT#BALANCE.
  01 balance pic s9(9)v99 value 0.
*>   ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 ACCOUNT#BALANCE.
*>           kind Variable
*>           display_name balance
*>           documentation
*>           > ```cobol
*>           > 01 balance pic s9(9)v99 value 0
*>           > ```
*>                               ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 ACCOUNT#BALANCE.
  procedure division.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().
  method-id. deposit.
*>           ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().
*>                   kind Method
*>                   display_name deposit
*>                   documentation
*>                   > ```cobol
*>                   > method-id. deposit
*>                   > ```
  data division.
  linkage section.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().AMOUNT.
  01 amount pic s9(9)v99.
*>   ^^^^^^ definition scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().AMOUNT.
*>          kind Variable
*>          display_name amount
*>          documentation
*>          > ```cobol
*>          > 01 amount pic s9(9)v99
*>          > ```
*>                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().AMOUNT.
  procedure division using amount.
*>                         ^^^^^^ reference scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().AMOUNT.
      add amount to balance
*>        ^^^^^^ reference scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().AMOUNT.
*>                  ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 ACCOUNT#BALANCE.
      goback.
  end method deposit.
*>           ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().
*>                  ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().
  end object.
  end class Account.
*>          ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 ACCOUNT#
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 ACCOUNT#
  
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 DOUBLE-IT().
  identification division.
  function-id. double-it.
*>             ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 DOUBLE-IT().
*>                       kind Function
*>                       display_name double-it
*>                       documentation
*>                       > ```cobol
*>                       > function-id. double-it
*>                       > ```
  data division.
  linkage section.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 DOUBLE-IT().X.
  01 x pic 9(4).
*>   ^ definition scip-cobol cobol fixture 0.0.0 DOUBLE-IT().X.
*>     kind Variable
*>     display_name x
*>     documentation
*>     > ```cobol
*>     > 01 x pic 9(4)
*>     > ```
*>             ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 DOUBLE-IT().X.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 DOUBLE-IT().R.
  01 r pic 9(5).
*>   ^ definition scip-cobol cobol fixture 0.0.0 DOUBLE-IT().R.
*>     kind Variable
*>     display_name r
*>     documentation
*>     > ```cobol
*>     > 01 r pic 9(5)
*>     > ```
*>             ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 DOUBLE-IT().R.
  procedure division using x returning r.
*>                         ^ reference scip-cobol cobol fixture 0.0.0 DOUBLE-IT().X.
*>                                     ^ reference scip-cobol cobol fixture 0.0.0 DOUBLE-IT().R.
      compute r = x * 2
*>            ^ reference scip-cobol cobol fixture 0.0.0 DOUBLE-IT().R.
*>                ^ reference scip-cobol cobol fixture 0.0.0 DOUBLE-IT().X.
      goback.
  end function double-it.
*>             ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 DOUBLE-IT().
*>                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 DOUBLE-IT().
  
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CLIENT/
  identification division.
  program-id. client.
*>            ^^^^^^ definition scip-cobol cobol fixture 0.0.0 CLIENT/
*>                   kind Module
*>                   display_name client
*>                   documentation
*>                   > ```cobol
*>                   > program-id. client
*>                   > ```
  data division.
  working-storage section.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CLIENT/ACCT.
  01 acct object reference.
*>   ^^^^ definition scip-cobol cobol fixture 0.0.0 CLIENT/ACCT.
*>        kind Variable
*>        display_name acct
*>        documentation
*>        > ```cobol
*>        > 01 acct object reference
*>        > ```
*>                        ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CLIENT/ACCT.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CLIENT/N.
  01 n pic 9(5).
*>   ^ definition scip-cobol cobol fixture 0.0.0 CLIENT/N.
*>     kind Variable
*>     display_name n
*>     documentation
*>     > ```cobol
*>     > 01 n pic 9(5)
*>     > ```
*>             ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CLIENT/N.
  procedure division.
      invoke acct "deposit" using n
*>           ^^^^ reference scip-cobol cobol fixture 0.0.0 CLIENT/ACCT.
*>                 ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 ACCOUNT#DEPOSIT().
*>                                ^ reference scip-cobol cobol fixture 0.0.0 CLIENT/N.
      compute n = function double-it(n) + function length(n)
*>            ^ reference scip-cobol cobol fixture 0.0.0 CLIENT/N.
*>                         ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 DOUBLE-IT().
*>                                   ^ reference scip-cobol cobol fixture 0.0.0 CLIENT/N.
*>                                                        ^ reference scip-cobol cobol fixture 0.0.0 CLIENT/N.
      goback.
  end program client.
*>            ^^^^^^ reference scip-cobol cobol fixture 0.0.0 CLIENT/
*>                  ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CLIENT/
  
