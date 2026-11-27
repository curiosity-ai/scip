        * DCLGEN-style host structure.
*>^ definition scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/
*>  kind File
*>  display_name DCLCUST.cpy
*>  documentation
*>  > copybook `copylib/DCLCUST.cpy`
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.
         01  DCLCUSTOMER.
*>           ^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.
*>                       kind Struct
*>                       display_name DCLCUSTOMER
*>                       documentation
*>                       > ```cobol
*>                       > 01 DCLCUSTOMER
*>                       > ```
*>                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.DCL-ORDER-ID.
             10 DCL-ORDER-ID         PIC S9(9) COMP.
*>              ^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.DCL-ORDER-ID.
*>                           kind Field
*>                           display_name DCL-ORDER-ID
*>                           documentation
*>                           > ```cobol
*>                           > 10 DCL-ORDER-ID PIC S9(9) COMP
*>                           > ```
*>                                                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.DCL-ORDER-ID.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.DCL-NAME.
             10 DCL-NAME             PIC X(40).
*>              ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.DCL-NAME.
*>                       kind Field
*>                       display_name DCL-NAME
*>                       documentation
*>                       > ```cobol
*>                       > 10 DCL-NAME PIC X(40)
*>                       > ```
*>                                            ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.DCL-NAME.
  
