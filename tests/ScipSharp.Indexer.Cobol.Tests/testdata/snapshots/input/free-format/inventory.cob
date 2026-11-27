>>SOURCE FORMAT IS FREE
*> Free-format source: no sequence area, lower case, floating comments.
identification division.
program-id. inventory.
data division.
working-storage section.
01 item-table.
   05 item-count      pic 9(3) value 0.
   05 item occurs 1 to 100 times depending on item-count
         ascending key is item-code
         indexed by item-ix.
      10 item-code    pic x(8).
      10 item-qty     pic s9(5) comp.
      10 item-price   pic 9(5)v99.
01 ws-search-code     pic x(8).   *> what we look for
01 ws-total           pic 9(9)v99 value zero.
01 ws-flags.
   05 ws-found-flag   pic x value 'N'.
      88 item-found   value 'Y' false 'N'.
procedure division.
main-para.
    move "ABC" to ws-search-code
    perform find-item
    if item-found
       compute ws-total = item-qty (item-ix) * item-price (item-ix)
       display "total: " ws-total
    end-if
    perform varying item-ix from 1 by 1 until item-ix > item-count
        add item-qty (item-ix) to ws-total
    end-perform
    goback.

find-item.
    set item-ix to 1
    search all item
        at end set item-found to false
        when item-code (item-ix) = ws-search-code
             set item-found to true
    end-search.
end program inventory.
