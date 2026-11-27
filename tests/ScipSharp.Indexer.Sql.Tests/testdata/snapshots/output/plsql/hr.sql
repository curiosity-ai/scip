--⌄ enclosing_range_start scip-sql . plsql . default/DEPARTMENTS#
  CREATE TABLE departments (
--             ^^^^^^^^^^^ definition scip-sql . plsql . default/DEPARTMENTS#
--                         kind Class
--                         display_name departments
--                         documentation
--                         > ```sql
--                         > TABLE default.departments
--                         > ```
--  ⌄ enclosing_range_start scip-sql . plsql . default/DEPARTMENTS#DEPARTMENT_ID.
--                            ⌄ enclosing_range_start scip-sql . plsql . default/DEPARTMENTS#DEPT_PK:
    department_id   NUMBER(4) CONSTRAINT dept_pk PRIMARY KEY,
--  ^^^^^^^^^^^^^ definition scip-sql . plsql . default/DEPARTMENTS#DEPARTMENT_ID.
--                kind Field
--                display_name department_id
--                documentation
--                > ```sql
--                > department_id NUMBER(4) CONSTRAINT dept_pk PRIMARY KEY
--                > ```
--                                       ^^^^^^^ definition scip-sql . plsql . default/DEPARTMENTS#DEPT_PK:
--                                               kind Key
--                                               display_name dept_pk
--                                               documentation
--                                               > ```sql
--                                               > CONSTRAINT dept_pk PRIMARY KEY
--                                               > ```
--                                                         ⌃ enclosing_range_end scip-sql . plsql . default/DEPARTMENTS#DEPARTMENT_ID.
--                                                         ⌃ enclosing_range_end scip-sql . plsql . default/DEPARTMENTS#DEPT_PK:
--  ⌄ enclosing_range_start scip-sql . plsql . default/DEPARTMENTS#DEPARTMENT_NAME.
    department_name VARCHAR2(30) NOT NULL
--  ^^^^^^^^^^^^^^^ definition scip-sql . plsql . default/DEPARTMENTS#DEPARTMENT_NAME.
--                  kind Field
--                  display_name department_name
--                  documentation
--                  > ```sql
--                  > department_name VARCHAR2(30) NOT NULL
--                  > ```
--                                      ⌃ enclosing_range_end scip-sql . plsql . default/DEPARTMENTS#DEPARTMENT_NAME.
  );
--⌃ enclosing_range_end scip-sql . plsql . default/DEPARTMENTS#
  
--⌄ enclosing_range_start scip-sql . plsql . default/EMPLOYEES#
  CREATE TABLE employees (
--             ^^^^^^^^^ definition scip-sql . plsql . default/EMPLOYEES#
--                       kind Class
--                       display_name employees
--                       documentation
--                       > ```sql
--                       > TABLE default.employees
--                       > ```
--  ⌄ enclosing_range_start scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
    employee_id   NUMBER(6) PRIMARY KEY,
--  ^^^^^^^^^^^ definition scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
--              kind Field
--              display_name employee_id
--              documentation
--              > ```sql
--              > employee_id NUMBER(6) PRIMARY KEY
--              > ```
--                                    ⌃ enclosing_range_end scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
--  ⌄ enclosing_range_start scip-sql . plsql . default/EMPLOYEES#LAST_NAME.
    last_name     VARCHAR2(25) NOT NULL,
--  ^^^^^^^^^ definition scip-sql . plsql . default/EMPLOYEES#LAST_NAME.
--            kind Field
--            display_name last_name
--            documentation
--            > ```sql
--            > last_name VARCHAR2(25) NOT NULL
--            > ```
--                                    ⌃ enclosing_range_end scip-sql . plsql . default/EMPLOYEES#LAST_NAME.
--  ⌄ enclosing_range_start scip-sql . plsql . default/EMPLOYEES#SALARY.
    salary        NUMBER(8,2),
--  ^^^^^^ definition scip-sql . plsql . default/EMPLOYEES#SALARY.
--         kind Field
--         display_name salary
--         documentation
--         > ```sql
--         > salary NUMBER(8,2)
--         > ```
--                          ⌃ enclosing_range_end scip-sql . plsql . default/EMPLOYEES#SALARY.
--  ⌄ enclosing_range_start scip-sql . plsql . default/EMPLOYEES#DEPARTMENT_ID.
    department_id NUMBER(4) REFERENCES departments
--  ^^^^^^^^^^^^^ definition scip-sql . plsql . default/EMPLOYEES#DEPARTMENT_ID.
--                kind Field
--                display_name department_id
--                documentation
--                > ```sql
--                > department_id NUMBER(4) REFERENCES departments
--                > ```
--                relationship scip-sql . plsql . default/DEPARTMENTS#DEPARTMENT_ID. reference
--                                     ^^^^^^^^^^^ reference scip-sql . plsql . default/DEPARTMENTS#
--                                               ⌃ enclosing_range_end scip-sql . plsql . default/EMPLOYEES#DEPARTMENT_ID.
  );
--⌃ enclosing_range_end scip-sql . plsql . default/EMPLOYEES#
  
--⌄ enclosing_range_start scip-sql . plsql . default/EMPLOYEES_SEQ#
  CREATE SEQUENCE employees_seq START WITH 1000;
--                ^^^^^^^^^^^^^ definition scip-sql . plsql . default/EMPLOYEES_SEQ#
--                              kind Object
--                              display_name employees_seq
--                              documentation
--                              > ```sql
--                              > SEQUENCE default.employees_seq
--                              > ```
--                                            ⌃ enclosing_range_end scip-sql . plsql . default/EMPLOYEES_SEQ#
  
--⌄ enclosing_range_start scip-sql . plsql . default/EMP_PKG/
  CREATE OR REPLACE PACKAGE emp_pkg AS
--                          ^^^^^^^ definition scip-sql . plsql . default/EMP_PKG/
--                                  kind Package
--                                  display_name emp_pkg
--                                  documentation
--                                  > ```sql
--                                  > PACKAGE default.emp_pkg
--                                  > ```
--  ⌄ enclosing_range_start scip-sql . plsql . default/EMP_PKG/G_RAISE_PCT.
    g_raise_pct NUMBER := 5;
--  ^^^^^^^^^^^ definition scip-sql . plsql . default/EMP_PKG/G_RAISE_PCT.
--              kind Variable
--              display_name g_raise_pct
--              documentation
--              > ```sql
--              > DECLARE g_raise_pct NUMBER
--              > ```
--                        ⌃ enclosing_range_end scip-sql . plsql . default/EMP_PKG/G_RAISE_PCT.
--  ⌄ enclosing_range_start scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).
    FUNCTION total_salary(p_dept IN NUMBER) RETURN NUMBER;
--           ^^^^^^^^^^^^ definition scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).
--                        kind Function
--                        display_name total_salary
--                        documentation
--                        > ```sql
--                        > FUNCTION emp_pkg.total_salary(IN p_dept NUMBER) RETURNS NUMBER
--                        > ```
--                        ^^^^^^ definition scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).(P_DEPT)
--                               kind Parameter
--                               display_name p_dept
--                               documentation
--                               > ```sql
--                               > IN p_dept NUMBER
--                               > ```
--                                                      ⌃ enclosing_range_end scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).
--  ⌄ enclosing_range_start scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).
    PROCEDURE raise_salary(p_emp IN employees.employee_id%TYPE);
--            ^^^^^^^^^^^^ definition scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).
--                         kind Method
--                         display_name raise_salary
--                         documentation
--                         > ```sql
--                         > PROCEDURE emp_pkg.raise_salary(IN p_emp employees.employee_id%TYPE)
--                         > ```
--                         ^^^^^ definition scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).(P_EMP)
--                               kind Parameter
--                               display_name p_emp
--                               documentation
--                               > ```sql
--                               > IN p_emp employees.employee_id%TYPE
--                               > ```
--                                  ^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#
--                                            ^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
--                                                            ⌃ enclosing_range_end scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).
  END emp_pkg;
--          ⌃ enclosing_range_end scip-sql . plsql . default/EMP_PKG/
  /
  
--⌄ enclosing_range_start scip-sql . plsql . default/EMP_PKG/
  CREATE OR REPLACE PACKAGE BODY emp_pkg AS
--                               ^^^^^^^ definition scip-sql . plsql . default/EMP_PKG/
--                                       kind Package
--                                       display_name emp_pkg
--                                       documentation
--                                       > ```sql
--                                       > PACKAGE default.emp_pkg
--                                       > ```
--  ⌄ enclosing_range_start scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).
    FUNCTION total_salary(p_dept IN NUMBER) RETURN NUMBER IS
--           ^^^^^^^^^^^^ definition scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).
--                        kind Function
--                        display_name total_salary
--                        documentation
--                        > ```sql
--                        > FUNCTION emp_pkg.total_salary(IN p_dept NUMBER) RETURNS NUMBER
--                        > ```
--                        ^^^^^^ definition scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).(P_DEPT)
--                               kind Parameter
--                               display_name p_dept
--                               documentation
--                               > ```sql
--                               > IN p_dept NUMBER
--                               > ```
--    ⌄ enclosing_range_start local 0
      v_total NUMBER := 0;
--    ^^^^^^^ definition local 0
--            kind Variable
--            display_name v_total
--            documentation
--            > ```sql
--            > DECLARE v_total NUMBER
--            > ```
--                      ⌃ enclosing_range_end local 0
    BEGIN
      SELECT SUM(e.salary) INTO v_total FROM employees e WHERE e.department_id = p_dept;
--               ^ reference local 1
--                 ^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#SALARY.
--                              ^^^^^^^ reference local 0
--                                           ^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#
--                                                     ^ definition local 1
--                                                       kind Variable
--                                                       display_name e
--                                                       documentation
--                                                       > ```sql
--                                                       > e: employees
--                                                       > ```
--                                                             ^ reference local 1
--                                                               ^^^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#DEPARTMENT_ID.
--                                                                               ^^^^^^ reference scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).(P_DEPT)
      RETURN v_total;
--           ^^^^^^^ reference local 0
    END total_salary;
--                 ⌃ enclosing_range_end scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).
  
--  ⌄ enclosing_range_start scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).
    PROCEDURE raise_salary(p_emp IN employees.employee_id%TYPE) IS
--            ^^^^^^^^^^^^ definition scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).
--                         kind Method
--                         display_name raise_salary
--                         documentation
--                         > ```sql
--                         > PROCEDURE emp_pkg.raise_salary(IN p_emp employees.employee_id%TYPE)
--                         > ```
--                         ^^^^^ definition scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).(P_EMP)
--                               kind Parameter
--                               display_name p_emp
--                               documentation
--                               > ```sql
--                               > IN p_emp employees.employee_id%TYPE
--                               > ```
--                                  ^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#
--                                            ^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
--    ⌄ enclosing_range_start local 2
      r_emp employees%ROWTYPE;
--    ^^^^^ definition local 2
--          kind Variable
--          display_name r_emp
--          documentation
--          > ```sql
--          > DECLARE r_emp employees%ROWTYPE
--          > ```
--          ^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#
--                          ⌃ enclosing_range_end local 2
--    ⌄ enclosing_range_start local 3
      CURSOR c_peers IS SELECT employee_id, salary FROM employees WHERE department_id = r_emp.department_id;
--           ^^^^^^^ definition local 3
--                   kind Variable
--                   display_name c_peers
--                   documentation
--                   > ```sql
--                   > DECLARE c_peers cursor
--                   > ```
--                             ^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
--                                          ^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#SALARY.
--                                                      ^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#
--                                                                      ^^^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#DEPARTMENT_ID.
--                                                                                      ^^^^^ reference local 2
--                                                                                            ^^^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#DEPARTMENT_ID.
--                                                                                                        ⌃ enclosing_range_end local 3
    BEGIN
      SELECT * INTO r_emp FROM employees WHERE employee_id = p_emp;
--                  ^^^^^ reference local 2
--                             ^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#
--                                             ^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
--                                                           ^^^^^ reference scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).(P_EMP)
      FOR peer IN c_peers LOOP
--        ^^^^ definition local 4
--             kind Variable
--             display_name peer
--             documentation
--             > ```sql
--             > FOR peer IN ...
--             > ```
--                ^^^^^^^ reference local 3
        UPDATE employees SET salary = peer.salary * (1 + g_raise_pct / 100) WHERE employee_id = peer.employee_id;
--             ^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#
--                           ^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#SALARY.
--                                    ^^^^ reference local 4
--                                         ^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#SALARY.
--                                                       ^^^^^^^^^^^ reference scip-sql . plsql . default/EMP_PKG/G_RAISE_PCT.
--                                                                                ^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
--                                                                                              ^^^^ reference local 4
--                                                                                                   ^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
      END LOOP;
    END raise_salary;
--                 ⌃ enclosing_range_end scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).
  END emp_pkg;
--          ⌃ enclosing_range_end scip-sql . plsql . default/EMP_PKG/
  /
  
--⌄ enclosing_range_start scip-sql . plsql . default/EMPLOYEES#EMPLOYEES_BI:
  CREATE OR REPLACE TRIGGER employees_bi
--                          ^^^^^^^^^^^^ definition scip-sql . plsql . default/EMPLOYEES#EMPLOYEES_BI:
--                                       kind Event
--                                       display_name employees_bi
--                                       documentation
--                                       > ```sql
--                                       > TRIGGER employees_bi ON default.employees
--                                       > ```
  BEFORE INSERT ON employees
--                 ^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#
  FOR EACH ROW
  BEGIN
    :NEW.employee_id := employees_seq.NEXTVAL;
--       ^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES#EMPLOYEE_ID.
--                      ^^^^^^^^^^^^^ reference scip-sql . plsql . default/EMPLOYEES_SEQ#
  END;
--  ⌃ enclosing_range_end scip-sql . plsql . default/EMPLOYEES#EMPLOYEES_BI:
  /
  
  BEGIN
    emp_pkg.raise_salary(100);
--  ^^^^^^^ reference scip-sql . plsql . default/EMP_PKG/
--          ^^^^^^^^^^^^ reference scip-sql . plsql . default/EMP_PKG/RAISE_SALARY(`employees.employee_id%type`).
    DBMS_OUTPUT.PUT_LINE(emp_pkg.total_salary(10));
--                       ^^^^^^^ reference scip-sql . plsql . default/EMP_PKG/
--                               ^^^^^^^^^^^^ reference scip-sql . plsql . default/EMP_PKG/TOTAL_SALARY(number).
  END;
  /
  
