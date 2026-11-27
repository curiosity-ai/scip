CLASS zcl_circle DEFINITION PUBLIC INHERITING FROM zcl_base CREATE PUBLIC.
  PUBLIC SECTION.
    "! Creates a circle
    "! @parameter iv_radius | radius
    METHODS constructor IMPORTING iv_radius TYPE f.
    METHODS zif_shape~area REDEFINITION.
  PRIVATE SECTION.
    DATA mv_radius TYPE f.
ENDCLASS.

CLASS zcl_circle IMPLEMENTATION.
  METHOD constructor.
    super->constructor( ).
    mv_radius = iv_radius.
    mv_name = 'circle'.
  ENDMETHOD.

  METHOD zif_shape~area.
    DATA(lv_square) = mv_radius * mv_radius.
    rv_area = c_pi * lv_square.
  ENDMETHOD.
ENDCLASS.
