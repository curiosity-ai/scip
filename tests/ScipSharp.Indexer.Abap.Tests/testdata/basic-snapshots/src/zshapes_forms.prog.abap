 FORM print USING io_shape TYPE REF TO zcl_base.
*     ^^^^^ definition scip-abap abap basic . ZSHAPES_FORMS/PRINT().
*           kind Function
*           display_name print
*           documentation
*           > ```abap
*           > FORM print USING io_shape TYPE REF TO zcl_base.
*           > ```
*                 ^^^^^^^^ definition scip-abap abap basic . ZSHAPES_FORMS/PRINT().(IO_SHAPE)
*                          kind Parameter
*                          display_name io_shape
*                          documentation
*                          > ```abap
*                          > FORM print USING io_shape TYPE REF TO zcl_base.
*                          > ```
*                                      ^^^^^^^^ reference scip-abap abap basic . ZCL_BASE#
   DATA lv_text TYPE string.
*       ^^^^^^^ definition local 1
*               kind Variable
*               display_name lv_text
*               documentation
*               > ```abap
*               > DATA lv_text TYPE string.
*               > ```
   lv_text = io_shape->describe( ).
*  ^^^^^^^ reference local 1
*            ^^^^^^^^ reference scip-abap abap basic . ZSHAPES_FORMS/PRINT().(IO_SHAPE)
*                      ^^^^^^^^ reference scip-abap abap basic . ZCL_BASE#DESCRIBE().
   WRITE / lv_text.
*          ^^^^^^^ reference local 1
   gv_count = gv_count + 1.
*  ^^^^^^^^ reference scip-abap abap basic . ZSHAPES/GV_COUNT.
*             ^^^^^^^^ reference scip-abap abap basic . ZSHAPES/GV_COUNT.
 ENDFORM.
 
