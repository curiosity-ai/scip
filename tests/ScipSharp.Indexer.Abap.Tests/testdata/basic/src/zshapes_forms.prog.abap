FORM print USING io_shape TYPE REF TO zcl_base.
  DATA lv_text TYPE string.
  lv_text = io_shape->describe( ).
  WRITE / lv_text.
  gv_count = gv_count + 1.
ENDFORM.
