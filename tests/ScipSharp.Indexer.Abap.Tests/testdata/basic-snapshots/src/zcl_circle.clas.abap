 CLASS zcl_circle DEFINITION PUBLIC INHERITING FROM zcl_base CREATE PUBLIC.
*      ^^^^^^^^^^ definition scip-abap abap basic . ZCL_CIRCLE#
*                 kind Class
*                 display_name zcl_circle
*                 documentation
*                 > ```abap
*                 > CLASS zcl_circle DEFINITION PUBLIC INHERITING FROM zcl_base CREATE PUBLIC.
*                 > ```
*                 relationship scip-abap abap basic . ZCL_BASE# implementation reference
*                                                   ^^^^^^^^ reference scip-abap abap basic . ZCL_BASE#
   PUBLIC SECTION.
     "! Creates a circle
     "! @parameter iv_radius | radius
     METHODS constructor IMPORTING iv_radius TYPE f.
*            ^^^^^^^^^^^ definition scip-abap abap basic . ZCL_CIRCLE#CONSTRUCTOR().
*                        kind Constructor
*                        display_name constructor
*                        documentation
*                        > ```abap
*                        > "! Creates a circle
*                        > "! @parameter iv_radius | radius
*                        > METHODS constructor IMPORTING iv_radius TYPE f.
*                        > ```
*                                  ^^^^^^^^^ definition scip-abap abap basic . ZCL_CIRCLE#CONSTRUCTOR().(IV_RADIUS)
*                                            kind Parameter
*                                            display_name iv_radius
*                                            documentation
*                                            > ```abap
*                                            > "! Creates a circle
*                                            > "! @parameter iv_radius | radius
*                                            > METHODS constructor IMPORTING iv_radius TYPE f.
*                                            > ```
     METHODS zif_shape~area REDEFINITION.
*            ^^^^^^^^^ reference scip-abap abap basic . ZIF_SHAPE#
*                      ^^^^ reference scip-abap abap basic . ZIF_SHAPE#AREA().
   PRIVATE SECTION.
     DATA mv_radius TYPE f.
*         ^^^^^^^^^ definition scip-abap abap basic . ZCL_CIRCLE#MV_RADIUS.
*                   kind Field
*                   display_name mv_radius
*                   documentation
*                   > ```abap
*                   > DATA mv_radius TYPE f.
*                   > ```
 ENDCLASS.
 
 CLASS zcl_circle IMPLEMENTATION.
*      ^^^^^^^^^^ reference scip-abap abap basic . ZCL_CIRCLE#
   METHOD constructor.
*         ^^^^^^^^^^^ reference scip-abap abap basic . ZCL_CIRCLE#CONSTRUCTOR().
     super->constructor( ).
     mv_radius = iv_radius.
*    ^^^^^^^^^ reference scip-abap abap basic . ZCL_CIRCLE#MV_RADIUS.
*                ^^^^^^^^^ reference scip-abap abap basic . ZCL_CIRCLE#CONSTRUCTOR().(IV_RADIUS)
     mv_name = 'circle'.
*    ^^^^^^^ reference scip-abap abap basic . ZCL_BASE#MV_NAME.
   ENDMETHOD.
 
   METHOD zif_shape~area.
*         ^^^^^^^^^ reference scip-abap abap basic . ZIF_SHAPE#
*                   ^^^^ reference scip-abap abap basic . ZIF_SHAPE#AREA().
     DATA(lv_square) = mv_radius * mv_radius.
*         ^^^^^^^^^ definition local 0
*                   kind Variable
*                   display_name lv_square
*                   documentation
*                   > ```abap
*                   > DATA(lv_square) = mv_radius * mv_radius.
*                   > ```
*                      ^^^^^^^^^ reference scip-abap abap basic . ZCL_CIRCLE#MV_RADIUS.
*                                  ^^^^^^^^^ reference scip-abap abap basic . ZCL_CIRCLE#MV_RADIUS.
     rv_area = c_pi * lv_square.
*    ^^^^^^^ reference scip-abap abap basic . ZIF_SHAPE#AREA().(RV_AREA)
*              ^^^^ reference scip-abap abap basic . ZCL_BASE#C_PI.
*                     ^^^^^^^^^ reference local 0
   ENDMETHOD.
 ENDCLASS.
 
