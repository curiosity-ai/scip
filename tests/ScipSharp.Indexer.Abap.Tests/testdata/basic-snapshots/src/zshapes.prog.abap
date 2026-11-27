 REPORT zshapes.
*       ^^^^^^^ definition scip-abap abap basic . ZSHAPES/
*               kind Namespace
*               display_name ZSHAPES
 
 TYPES ty_count TYPE i.
*      ^^^^^^^^ definition scip-abap abap basic . ZSHAPES/TY_COUNT#
*               kind Type
*               display_name ty_count
*               documentation
*               > ```abap
*               > TYPES ty_count TYPE i.
*               > ```
 DATA gv_count TYPE ty_count.
*     ^^^^^^^^ definition scip-abap abap basic . ZSHAPES/GV_COUNT.
*              kind Variable
*              display_name gv_count
*              documentation
*              > ```abap
*              > DATA gv_count TYPE ty_count.
*              > ```
*                   ^^^^^^^^ reference scip-abap abap basic . ZSHAPES/TY_COUNT#
 DATA gs_row TYPE zshape_row.
*     ^^^^^^ definition scip-abap abap basic . ZSHAPES/GS_ROW.
*            kind Variable
*            display_name gs_row
*            documentation
*            > ```abap
*            > DATA gs_row TYPE zshape_row.
*            > ```
*                 ^^^^^^^^^^ reference scip-abap abap basic . ZSHAPE_ROW#
 
 INCLUDE zshapes_forms.
*        ^^^^^^^^^^^^^ reference scip-abap abap basic . ZSHAPES_FORMS/
 
 START-OF-SELECTION.
   DATA(lo_circle) = NEW zcl_circle( iv_radius = 2 ).
*       ^^^^^^^^^ definition scip-abap abap basic . ZSHAPES/LO_CIRCLE.
*                 kind Variable
*                 display_name lo_circle
*                 documentation
*                 > ```abap
*                 > DATA(lo_circle) = NEW zcl_circle( iv_radius = 2 ).
*                 > ```
*                        ^^^^^^^^^^ reference scip-abap abap basic . ZCL_CIRCLE#
   PERFORM print USING lo_circle.
*          ^^^^^ reference scip-abap abap basic . ZSHAPES_FORMS/PRINT().
*                      ^^^^^^^^^ reference scip-abap abap basic . ZSHAPES/LO_CIRCLE.
   gv_count = gv_count + 1.
*  ^^^^^^^^ reference scip-abap abap basic . ZSHAPES/GV_COUNT.
*             ^^^^^^^^ reference scip-abap abap basic . ZSHAPES/GV_COUNT.
   CALL FUNCTION 'Z_SHAPE_LOG'
*                 ^^^^^^^^^^^ reference scip-abap abap basic . Z_SHAPE_LOG().
     EXPORTING
       iv_text = lo_circle->describe( ).
*                ^^^^^^^^^ reference scip-abap abap basic . ZSHAPES/LO_CIRCLE.
*                           ^^^^^^^^ reference scip-abap abap basic . ZCL_BASE#DESCRIBE().
   SELECT SINGLE * FROM zshape_tab INTO @DATA(ls_tab).
*                                             ^^^^^^ definition scip-abap abap basic . ZSHAPES/LS_TAB.
*                                                    kind Variable
*                                                    display_name ls_tab
*                                                    documentation
*                                                    > ```abap
*                                                    > SELECT SINGLE * FROM zshape_tab INTO @DATA(ls_tab).
*                                                    > ```
   DATA lo_alv TYPE REF TO cl_salv_table.
*       ^^^^^^ definition scip-abap abap basic . ZSHAPES/LO_ALV.
*              kind Variable
*              display_name lo_alv
*              documentation
*              > ```abap
*              > DATA lo_alv TYPE REF TO cl_salv_table.
*              > ```
*                          ^^^^^^^^^^^^^ reference scip-abap abap . . CL_SALV_TABLE#
 
