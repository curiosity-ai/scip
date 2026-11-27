REPORT zshapes.

TYPES ty_count TYPE i.
DATA gv_count TYPE ty_count.
DATA gs_row TYPE zshape_row.

INCLUDE zshapes_forms.

START-OF-SELECTION.
  DATA(lo_circle) = NEW zcl_circle( iv_radius = 2 ).
  PERFORM print USING lo_circle.
  gv_count = gv_count + 1.
  CALL FUNCTION 'Z_SHAPE_LOG'
    EXPORTING
      iv_text = lo_circle->describe( ).
  SELECT SINGLE * FROM zshape_tab INTO @DATA(ls_tab).
  DATA lo_alv TYPE REF TO cl_salv_table.
