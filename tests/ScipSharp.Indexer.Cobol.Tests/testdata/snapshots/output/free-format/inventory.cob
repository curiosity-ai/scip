  >>SOURCE FORMAT IS FREE
  *> Free-format source: no sequence area, lower case, floating comments.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/
  identification division.
  program-id. inventory.
*>            ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/
*>                      kind Module
*>                      display_name inventory
*>                      documentation
*>                      > ```cobol
*>                      > program-id. inventory
*>                      > ```
  data division.
  working-storage section.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.
  01 item-table.
*>   ^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.
*>              kind Struct
*>              display_name item-table
*>              documentation
*>              > ```cobol
*>              > 01 item-table
*>              > ```
*>             ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.
*>   ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM-COUNT.
     05 item-count      pic 9(3) value 0.
*>      ^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM-COUNT.
*>                 kind Field
*>                 display_name item-count
*>                 documentation
*>                 > ```cobol
*>                 > 05 item-count pic 9(3) value 0
*>                 > ```
*>                                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM-COUNT.
*>   ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.
     05 item occurs 1 to 100 times depending on item-count
*>      ^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.
*>           kind Struct
*>           display_name item
*>           documentation
*>           > ```cobol
*>           > 05 item occurs 1 to 100 times depending on item-count ascending key is item-code indexed by item-ix
*>           > ```
*>                                              ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM-COUNT.
           ascending key is item-code
*>                          ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-CODE.
*>                    ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
           indexed by item-ix.
*>                    ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
*>                            kind Variable
*>                            display_name item-ix
*>                            documentation
*>                            > ```cobol
*>                            > INDEXED BY item-ix (index of item)
*>                            > ```
*>                          ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
*>                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.
*>      ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-CODE.
        10 item-code    pic x(8).
*>         ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-CODE.
*>                   kind Field
*>                   display_name item-code
*>                   documentation
*>                   > ```cobol
*>                   > 10 item-code pic x(8)
*>                   > ```
*>                              ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-CODE.
*>      ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-QTY.
        10 item-qty     pic s9(5) comp.
*>         ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-QTY.
*>                  kind Field
*>                  display_name item-qty
*>                  documentation
*>                  > ```cobol
*>                  > 10 item-qty pic s9(5) comp
*>                  > ```
*>                                    ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-QTY.
*>      ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-PRICE.
        10 item-price   pic 9(5)v99.
*>         ^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-PRICE.
*>                    kind Field
*>                    display_name item-price
*>                    documentation
*>                    > ```cobol
*>                    > 10 item-price pic 9(5)v99
*>                    > ```
*>                                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-PRICE.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/WS-SEARCH-CODE.
  01 ws-search-code     pic x(8).   *> what we look for
*>   ^^^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/WS-SEARCH-CODE.
*>                  kind Variable
*>                  display_name ws-search-code
*>                  documentation
*>                  > ```cobol
*>                  > 01 ws-search-code pic x(8)
*>                  > ```
*>                              ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/WS-SEARCH-CODE.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/WS-TOTAL.
  01 ws-total           pic 9(9)v99 value zero.
*>   ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/WS-TOTAL.
*>            kind Variable
*>            display_name ws-total
*>            documentation
*>            > ```cobol
*>            > 01 ws-total pic 9(9)v99 value zero
*>            > ```
*>                                            ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/WS-TOTAL.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.
  01 ws-flags.
*>   ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.
*>            kind Struct
*>            display_name ws-flags
*>            documentation
*>            > ```cobol
*>            > 01 ws-flags
*>            > ```
*>           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.
*>   ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.WS-FOUND-FLAG.
     05 ws-found-flag   pic x value 'N'.
*>      ^^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.WS-FOUND-FLAG.
*>                    kind Field
*>                    display_name ws-found-flag
*>                    documentation
*>                    > ```cobol
*>                    > 05 ws-found-flag pic x value 'N'
*>                    > ```
*>                                     ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.WS-FOUND-FLAG.
*>      ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.WS-FOUND-FLAG.ITEM-FOUND.
        88 item-found   value 'Y' false 'N'.
*>         ^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.WS-FOUND-FLAG.ITEM-FOUND.
*>                    kind Constant
*>                    display_name item-found
*>                    documentation
*>                    > ```cobol
*>                    > 88 item-found value 'Y' false 'N'
*>                    > ```
*>                                         ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.WS-FOUND-FLAG.ITEM-FOUND.
  procedure division.
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/MAIN-PARA().
  main-para.
*>^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/MAIN-PARA().
*>          kind Function
*>          display_name main-para
*>          documentation
*>          > ```cobol
*>          > main-para.
*>          > ```
      move "ABC" to ws-search-code
*>                  ^^^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/WS-SEARCH-CODE.
      perform find-item
*>            ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/FIND-ITEM().
      if item-found
*>       ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.WS-FOUND-FLAG.ITEM-FOUND.
         compute ws-total = item-qty (item-ix) * item-price (item-ix)
*>               ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/WS-TOTAL.
*>                          ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-QTY.
*>                                    ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
*>                                               ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-PRICE.
*>                                                           ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
         display "total: " ws-total
*>                         ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/WS-TOTAL.
      end-if
      perform varying item-ix from 1 by 1 until item-ix > item-count
*>                    ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
*>                                              ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
*>                                                        ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM-COUNT.
          add item-qty (item-ix) to ws-total
*>            ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-QTY.
*>                      ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
*>                                  ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/WS-TOTAL.
      end-perform
      goback.
*>          ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/MAIN-PARA().
  
*>⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 INVENTORY/FIND-ITEM().
  find-item.
*>^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 INVENTORY/FIND-ITEM().
*>          kind Function
*>          display_name find-item
*>          documentation
*>          > ```cobol
*>          > find-item.
*>          > ```
      set item-ix to 1
*>        ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
      search all item
*>               ^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.
          at end set item-found to false
*>                   ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.WS-FOUND-FLAG.ITEM-FOUND.
          when item-code (item-ix) = ws-search-code
*>             ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-CODE.
*>                        ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/ITEM-TABLE.ITEM.ITEM-IX.
*>                                   ^^^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/WS-SEARCH-CODE.
               set item-found to true
*>                 ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/WS-FLAGS.WS-FOUND-FLAG.ITEM-FOUND.
      end-search.
*>              ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/FIND-ITEM().
  end program inventory.
*>            ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 INVENTORY/
*>                     ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 INVENTORY/
  
