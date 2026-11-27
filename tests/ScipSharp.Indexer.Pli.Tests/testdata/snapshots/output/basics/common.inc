   /* Shared declarations, included by payroll.pli                  */
     DCL LIMIT FIXED DEC(7,2) STATIC INIT(100.00);
//       ^^^^^ definition scip-pli pli basics . PAYROLL().LIMIT.
//             kind Variable
//             display_name LIMIT
//             documentation
//             > ```pli
//             > DCL LIMIT FIXED DEC(7, 2) STATIC INIT(100.00)
//             > ```
     DCL LOG_LEVEL FIXED BIN(15) EXTERNAL;
//       ^^^^^^^^^ definition scip-pli pli basics . LOG_LEVEL.
//                 kind Variable
//                 display_name LOG_LEVEL
//                 documentation
//                 > ```pli
//                 > DCL LOG_LEVEL FIXED BIN(15) EXTERNAL
//                 > ```
  
