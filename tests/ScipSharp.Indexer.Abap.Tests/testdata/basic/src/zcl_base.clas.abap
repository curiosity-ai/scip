CLASS zcl_base DEFINITION PUBLIC ABSTRACT CREATE PUBLIC.
  PUBLIC SECTION.
    INTERFACES zif_shape.
    CONSTANTS c_pi TYPE f VALUE '3.14159'.
    METHODS describe RETURNING VALUE(rv_text) TYPE string.
  PROTECTED SECTION.
    DATA mv_name TYPE string.
ENDCLASS.

CLASS zcl_base IMPLEMENTATION.
  METHOD describe.
    rv_text = |{ mv_name }: { zif_shape~area( ) }|.
  ENDMETHOD.
ENDCLASS.
