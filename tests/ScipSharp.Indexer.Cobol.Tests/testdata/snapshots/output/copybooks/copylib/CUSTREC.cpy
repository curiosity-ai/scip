        * Customer record layout, copied under a level-01 item.
*>^ definition scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/
*>  kind File
*>  display_name CUSTREC.cpy
*>  documentation
*>  > copybook `copylib/CUSTREC.cpy`
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-ID.
             05 CUST-ID              PIC 9(8).
*>              ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-ID.
*>                      kind Field
*>                      display_name CUST-ID
*>                      documentation
*>                      > ```cobol
*>                      > 05 CUST-ID PIC 9(8)
*>                      > ```
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-ID.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-NAME.
             05 CUST-NAME            PIC X(40).
*>              ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-NAME.
*>                        kind Field
*>                        display_name CUST-NAME
*>                        documentation
*>                        > ```cobol
*>                        > 05 CUST-NAME PIC X(40)
*>                        > ```
*>                                            ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-NAME.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-BALANCE.
             05 CUST-BALANCE         PIC S9(7)V99 COMP-3.
*>              ^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-BALANCE.
*>                           kind Field
*>                           display_name CUST-BALANCE
*>                           documentation
*>                           > ```cobol
*>                           > 05 CUST-BALANCE PIC S9(7)V99 COMP-3
*>                           > ```
*>                                                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-BALANCE.
  
