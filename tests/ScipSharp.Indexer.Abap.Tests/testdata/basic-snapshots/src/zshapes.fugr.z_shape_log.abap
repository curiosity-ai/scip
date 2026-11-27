 FUNCTION z_shape_log.
*         ^^^^^^^^^^^ definition scip-abap abap basic . Z_SHAPE_LOG().
*                     kind Function
*                     display_name z_shape_log
*                     documentation
*                     > ```abap
*                     > FUNCTION z_shape_log.
*                     > ```
   APPEND iv_text TO gt_log.
*         ^^^^^^^ reference scip-abap abap basic . Z_SHAPE_LOG().(IV_TEXT)
*                    ^^^^^^ reference scip-abap abap basic . SAPLZSHAPES/GT_LOG.
 ENDFUNCTION.
 
