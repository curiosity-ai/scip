CREATE TABLE departments (
  department_id   NUMBER(4) CONSTRAINT dept_pk PRIMARY KEY,
  department_name VARCHAR2(30) NOT NULL
);

CREATE TABLE employees (
  employee_id   NUMBER(6) PRIMARY KEY,
  last_name     VARCHAR2(25) NOT NULL,
  salary        NUMBER(8,2),
  department_id NUMBER(4) REFERENCES departments
);

CREATE SEQUENCE employees_seq START WITH 1000;

CREATE OR REPLACE PACKAGE emp_pkg AS
  g_raise_pct NUMBER := 5;
  FUNCTION total_salary(p_dept IN NUMBER) RETURN NUMBER;
  PROCEDURE raise_salary(p_emp IN employees.employee_id%TYPE);
END emp_pkg;
/

CREATE OR REPLACE PACKAGE BODY emp_pkg AS
  FUNCTION total_salary(p_dept IN NUMBER) RETURN NUMBER IS
    v_total NUMBER := 0;
  BEGIN
    SELECT SUM(e.salary) INTO v_total FROM employees e WHERE e.department_id = p_dept;
    RETURN v_total;
  END total_salary;

  PROCEDURE raise_salary(p_emp IN employees.employee_id%TYPE) IS
    r_emp employees%ROWTYPE;
    CURSOR c_peers IS SELECT employee_id, salary FROM employees WHERE department_id = r_emp.department_id;
  BEGIN
    SELECT * INTO r_emp FROM employees WHERE employee_id = p_emp;
    FOR peer IN c_peers LOOP
      UPDATE employees SET salary = peer.salary * (1 + g_raise_pct / 100) WHERE employee_id = peer.employee_id;
    END LOOP;
  END raise_salary;
END emp_pkg;
/

CREATE OR REPLACE TRIGGER employees_bi
BEFORE INSERT ON employees
FOR EACH ROW
BEGIN
  :NEW.employee_id := employees_seq.NEXTVAL;
END;
/

BEGIN
  emp_pkg.raise_salary(100);
  DBMS_OUTPUT.PUT_LINE(emp_pkg.total_salary(10));
END;
/
