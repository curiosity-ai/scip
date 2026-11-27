 CLASS zcl_base DEFINITION PUBLIC ABSTRACT CREATE PUBLIC.
*      ^^^^^^^^ definition scip-abap abap basic . ZCL_BASE#
*               kind Class
*               display_name zcl_base
*               documentation
*               > ```abap
*               > CLASS zcl_base DEFINITION PUBLIC ABSTRACT CREATE PUBLIC.
*               > ```
*               relationship scip-abap abap basic . ZIF_SHAPE# implementation reference
   PUBLIC SECTION.
     INTERFACES zif_shape.
*               ^^^^^^^^^ reference scip-abap abap basic . ZIF_SHAPE#
     CONSTANTS c_pi TYPE f VALUE '3.14159'.
*              ^^^^ definition scip-abap abap basic . ZCL_BASE#C_PI.
*                   kind Constant
*                   display_name c_pi
*                   documentation
*                   > ```abap
*                   > CONSTANTS c_pi TYPE f VALUE '3.14159'.
*                   > ```
     METHODS describe RETURNING VALUE(rv_text) TYPE string.
*            ^^^^^^^^ definition scip-abap abap basic . ZCL_BASE#DESCRIBE().
*                     kind Method
*                     display_name describe
*                     documentation
*                     > ```abap
*                     > METHODS describe RETURNING VALUE(rv_text) TYPE string.
*                     > ```
*                                     ^^^^^^^ definition scip-abap abap basic . ZCL_BASE#DESCRIBE().(RV_TEXT)
*                                             kind Parameter
*                                             display_name rv_text
*                                             documentation
*                                             > ```abap
*                                             > METHODS describe RETURNING VALUE(rv_text) TYPE string.
*                                             > ```
   PROTECTED SECTION.
     DATA mv_name TYPE string.
*         ^^^^^^^ definition scip-abap abap basic . ZCL_BASE#MV_NAME.
*                 kind Field
*                 display_name mv_name
*                 documentation
*                 > ```abap
*                 > DATA mv_name TYPE string.
*                 > ```
 ENDCLASS.
 
 CLASS zcl_base IMPLEMENTATION.
*      ^^^^^^^^ reference scip-abap abap basic . ZCL_BASE#
   METHOD describe.
*         ^^^^^^^^ reference scip-abap abap basic . ZCL_BASE#DESCRIBE().
     rv_text = |{ mv_name }: { zif_shape~area( ) }|.
*    ^^^^^^^ reference scip-abap abap basic . ZCL_BASE#DESCRIBE().(RV_TEXT)
*                 ^^^^^^^ reference scip-abap abap basic . ZCL_BASE#MV_NAME.
*                              ^^^^^^^^^ reference scip-abap abap basic . ZIF_SHAPE#
*                                        ^^^^ reference scip-abap abap basic . ZIF_SHAPE#AREA().
   ENDMETHOD.
 ENDCLASS.
 
