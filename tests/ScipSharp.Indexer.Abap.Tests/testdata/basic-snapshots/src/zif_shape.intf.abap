 INTERFACE zif_shape PUBLIC.
*          ^^^^^^^^^ definition scip-abap abap basic . ZIF_SHAPE#
*                    kind Interface
*                    display_name zif_shape
*                    documentation
*                    > ```abap
*                    > INTERFACE zif_shape PUBLIC.
*                    > ```
   "! Area of the shape
   METHODS area RETURNING VALUE(rv_area) TYPE f.
*          ^^^^ definition scip-abap abap basic . ZIF_SHAPE#AREA().
*               kind Method
*               display_name area
*               documentation
*               > ```abap
*               > "! Area of the shape
*               > METHODS area RETURNING VALUE(rv_area) TYPE f.
*               > ```
*                               ^^^^^^^ definition scip-abap abap basic . ZIF_SHAPE#AREA().(RV_AREA)
*                                       kind Parameter
*                                       display_name rv_area
*                                       documentation
*                                       > ```abap
*                                       > "! Area of the shape
*                                       > METHODS area RETURNING VALUE(rv_area) TYPE f.
*                                       > ```
 ENDINTERFACE.
 
