using ScipSharp.Indexer.Sql.Lexing;

namespace ScipSharp.Indexer.Sql.Syntax;

/// <summary>An identifier: <see cref="Text"/> is the display form (unquoted), <see cref="Key"/> the dialect-normalised identity.</summary>
public sealed record Ident(Token Token, string Text, string Key, bool Quoted)
{
    public override string ToString() => Text;
}

/// <summary>A possibly qualified name (<c>db.schema.table</c>, <c>table.column</c>, ...).</summary>
public sealed record QName(IReadOnlyList<Ident> Parts)
{
    public Ident Last => Parts[^1];

    public bool IsTemplate => Parts.Count > 0 && Parts[^1].Token.Kind == TokenKind.Template;

    public override string ToString() => string.Join('.', Parts.Select(p => p.Text));
}

// ---------------------------------------------------------------- expressions

/// <summary>
/// An expression, kept flat: only the parts that matter for name resolution (name chains, function calls, nested
/// queries, variables, stars) are retained, in source order.
/// </summary>
public sealed class Expr(List<ExprItem> items)
{
    public List<ExprItem> Items { get; } = items;

    public static Expr Empty => new([]);
}

public abstract class ExprItem;

/// <summary>A (dotted) name in expression position: a column, <c>alias.column</c>, a variable, a record field, ...</summary>
public sealed class NameRef(QName name) : ExprItem
{
    public QName Name { get; } = name;
}

/// <summary><c>*</c> or <c>t.*</c>.</summary>
public sealed class StarRef(QName? qualifier, Token star) : ExprItem
{
    public QName? Qualifier { get; } = qualifier;

    public Token Star { get; } = star;
}

public sealed class FuncCall(QName name, List<Expr> args) : ExprItem
{
    public QName Name { get; } = name;

    public List<Expr> Args { get; } = args;

    /// <summary>FILTER / WITHIN GROUP / OVER (...) expressions.</summary>
    public List<Expr> Extra { get; } = [];

    /// <summary><c>OVER w</c>.</summary>
    public Ident? WindowName { get; set; }

    /// <summary>Number of arguments (literals included), used for overload resolution.</summary>
    public int ArgCount { get; set; }

    /// <summary>The first argument when it is a string literal (<c>nextval('seq')</c>).</summary>
    public Token? FirstStringArg { get; set; }
}

/// <summary>A reference to a catalog object of a known kind (<c>NEXT VALUE FOR seq</c>, <c>nextval('seq')</c>).</summary>
public sealed class ObjectRef(QName name, ObjectKind kind) : ExprItem
{
    public QName Name { get; } = name;

    public ObjectKind Kind { get; } = kind;
}

public sealed class SubqueryExpr(Query query) : ExprItem
{
    public Query Query { get; } = query;
}

/// <summary><c>@x</c>, <c>:x</c>, <c>$1</c>; <see cref="Field"/> for <c>:new.col</c> / <c>@t.col</c>.</summary>
public sealed class VarRef(Token token, Ident? field) : ExprItem
{
    public Token Token { get; } = token;

    public Ident? Field { get; } = field;
}

/// <summary>A user-defined type used in a cast or declaration (<c>x::mood</c>, <c>CAST(x AS mood)</c>).</summary>
public sealed class TypeRef(QName name) : ExprItem
{
    public QName Name { get; } = name;
}

// ---------------------------------------------------------------- queries

public sealed class Cte(Ident name, List<Ident>? columns, Query? query, Stmt? dml)
{
    public Ident Name { get; } = name;

    public List<Ident>? Columns { get; } = columns;

    public Query? Query { get; } = query;

    /// <summary>A data-modifying CTE (PostgreSQL <c>WITH x AS (INSERT ... RETURNING ...)</c>).</summary>
    public Stmt? Dml { get; } = dml;
}

public abstract class Query
{
    public List<Cte> With { get; } = [];

    public bool Recursive { get; set; }

    public List<Expr> OrderBy { get; } = [];

    /// <summary>LIMIT / OFFSET / FETCH / FOR UPDATE OF / OPTION ... expressions.</summary>
    public List<Expr> Tail { get; } = [];
}

public sealed class SelectItem(Expr expr, Ident? alias, StarRef? star)
{
    public Expr Expr { get; } = expr;

    public Ident? Alias { get; } = alias;

    public StarRef? Star { get; } = star;

    /// <summary>T-SQL <c>SELECT @v = expr</c> or <c>SELECT alias = expr</c>.</summary>
    public Token? AssignTarget { get; init; }
}

public sealed class SelectQuery : Query
{
    public List<Expr> DistinctOn { get; } = [];

    public List<Expr> Top { get; } = [];

    public List<SelectItem> Items { get; } = [];

    /// <summary><c>SELECT ... INTO</c> targets: variables (procedural code) or a new table.</summary>
    public List<Expr> Into { get; } = [];

    public List<TableRef> From { get; } = [];

    public Expr? Where { get; set; }

    public List<Expr> GroupBy { get; } = [];

    public Expr? Having { get; set; }

    public List<(Ident Name, Expr Spec)> Windows { get; } = [];

    /// <summary>QUALIFY, CONNECT BY, START WITH, PIVOT and other clauses resolved in the select scope.</summary>
    public List<Expr> Misc { get; } = [];
}

public sealed class SetOpQuery(Query left, Query right, string op) : Query
{
    public Query Left { get; } = left;

    public Query Right { get; } = right;

    public string Op { get; } = op;
}

public sealed class ValuesQuery(List<Expr> rows) : Query
{
    public List<Expr> Rows { get; } = rows;

    /// <summary>Number of columns in the first row (for VALUES used as a derived table).</summary>
    public int Arity { get; init; }
}

// ---------------------------------------------------------------- table references

public abstract class TableRef
{
    public Ident? Alias { get; set; }

    public List<Ident>? ColumnAliases { get; set; }

    /// <summary>Table hints, TABLESAMPLE, FOR SYSTEM_TIME, ... expressions resolved in the outer scope.</summary>
    public List<Expr> Misc { get; } = [];
}

public sealed class NamedTableRef(QName name) : TableRef
{
    public QName Name { get; } = name;
}

public sealed class DerivedTableRef(Query query, bool lateral) : TableRef
{
    public Query Query { get; } = query;

    public bool Lateral { get; } = lateral;
}

public sealed class FunctionTableRef(FuncCall call) : TableRef
{
    public FuncCall Call { get; } = call;
}

/// <summary>A table variable (T-SQL <c>@t</c>).</summary>
public sealed class VariableTableRef(Token token) : TableRef
{
    public Token Token { get; } = token;
}

public sealed class JoinRef(TableRef left, TableRef right, Expr? on, List<Ident>? usingColumns) : TableRef
{
    public TableRef Left { get; } = left;

    public TableRef Right { get; } = right;

    public Expr? On { get; } = on;

    public List<Ident>? Using { get; } = usingColumns;
}

// ---------------------------------------------------------------- statements

public abstract class Stmt
{
    /// <summary>First and last (inclusive) token of the statement.</summary>
    public Token First { get; set; } = null!;

    public Token Last { get; set; } = null!;

    /// <summary>Position in the project-wide replay order (set by the indexer).</summary>
    public int Ordinal { get; set; }

    /// <summary>T-SQL batch number (statements separated by <c>GO</c>); variables are batch-scoped.</summary>
    public int Batch { get; set; }

    /// <summary>Index of the top-level statement span this statement came from (anonymous-block variable scope).</summary>
    public int Span { get; set; }
}

/// <summary>PostgreSQL <c>DO $$ ... $$</c> anonymous block.</summary>
public sealed class AnonymousBlockStmt(List<Stmt> body) : Stmt
{
    public List<Stmt> Body { get; } = body;
}

public enum ObjectKind
{
    Unknown,
    Database,
    Schema,
    Table,
    View,
    MaterializedView,
    Column,
    Index,
    Constraint,
    Sequence,
    Type,
    Domain,
    Function,
    Procedure,
    Trigger,
    Package,
}

public sealed class CreateSchemaStmt(Ident name, bool isDatabase) : Stmt
{
    public Ident Name { get; } = name;

    public bool IsDatabase { get; } = isDatabase;

    public List<Stmt> Elements { get; } = [];
}

/// <summary><c>USE db</c>, <c>SET search_path</c>, <c>ALTER SESSION SET CURRENT_SCHEMA</c>.</summary>
public sealed class SetSchemaStmt(List<Ident> schemas, bool isUse) : Stmt
{
    public List<Ident> Schemas { get; } = schemas;

    public bool IsUse { get; } = isUse;
}

public enum ConstraintKind
{
    PrimaryKey,
    Unique,
    ForeignKey,
    Check,
    Index,
    Exclude,
    Other,
}

public sealed class ConstraintDef(Ident? name, ConstraintKind kind) : Stmt
{
    public Ident? Name { get; } = name;

    public ConstraintKind Kind { get; } = kind;

    public List<Ident> Columns { get; } = [];

    public QName? RefTable { get; set; }

    public List<Ident> RefColumns { get; } = [];

    public List<Expr> Exprs { get; } = [];
}

public sealed class ColumnDef(Ident name) : Stmt
{
    public Ident Name { get; } = name;

    public string TypeText { get; set; } = "";

    public QName? TypeName { get; set; }

    public List<ConstraintDef> Constraints { get; } = [];

    public List<Expr> Exprs { get; } = [];

    /// <summary>MySQL <c>COMMENT 'x'</c>.</summary>
    public string? Comment { get; set; }
}

public sealed class CreateTableStmt(QName name) : Stmt
{
    public QName Name { get; } = name;

    public bool Temporary { get; set; }

    public List<ColumnDef> Columns { get; } = [];

    public List<ConstraintDef> Constraints { get; } = [];

    public Query? AsQuery { get; set; }

    public List<Ident>? AsColumns { get; set; }

    public List<QName> Parents { get; } = [];

    public string? Comment { get; set; }

    public List<Expr> Misc { get; } = [];

    /// <summary>True for a T-SQL table type or a PL/SQL/PostgreSQL composite type (attributes are columns).</summary>
    public ObjectKind Kind { get; set; } = ObjectKind.Table;
}

public sealed class CreateViewStmt(QName name, bool materialized) : Stmt
{
    public QName Name { get; } = name;

    public bool Materialized { get; } = materialized;

    public List<Ident>? Columns { get; set; }

    public Query? Query { get; set; }
}

public sealed class CreateIndexStmt(Ident? name, QName? table) : Stmt
{
    public Ident? Name { get; } = name;

    public QName? Table { get; } = table;

    public List<Expr> Exprs { get; } = [];

    public bool Unique { get; set; }
}

public sealed class CreateSequenceStmt(QName name) : Stmt
{
    public QName Name { get; } = name;

    public QName? OwnedBy { get; set; }
}

public sealed class CreateTypeStmt(QName name, ObjectKind kind) : Stmt
{
    public QName Name { get; } = name;

    public ObjectKind Kind { get; } = kind;

    public bool IsEnum { get; set; }

    public List<Token> EnumLabels { get; } = [];

    public List<ColumnDef> Attributes { get; } = [];

    public string BaseType { get; set; } = "";

    public List<Expr> Exprs { get; } = [];
}

public sealed class ParamDef(Ident? name, string typeText, string mode) : Stmt
{
    public Ident? Name { get; } = name;

    public string TypeText { get; } = typeText;

    public string Mode { get; } = mode;

    public QName? TypeName { get; set; }

    public Expr? Default { get; set; }
}

public sealed class VarDecl(Ident name) : Stmt
{
    public Ident Name { get; } = name;

    public string TypeText { get; set; } = "";

    public QName? TypeName { get; set; }

    public Expr? Default { get; set; }

    public Query? Cursor { get; set; }

    public List<ParamDef> CursorParams { get; } = [];

    public List<ColumnDef> TableColumns { get; } = [];

    /// <summary><c>x t.col%TYPE</c> / <c>r t%ROWTYPE</c> anchor.</summary>
    public QName? Anchor { get; set; }

    public bool IsException { get; set; }
}

public sealed class DeclareStmt(List<VarDecl> vars) : Stmt
{
    public List<VarDecl> Vars { get; } = vars;

    /// <summary>Nested routines and other declarations of a PL/SQL declaration section.</summary>
    public List<Stmt> Members { get; } = [];
}

public sealed class CreateRoutineStmt(QName name, ObjectKind kind) : Stmt
{
    public QName Name { get; } = name;

    public ObjectKind Kind { get; } = kind;

    public List<ParamDef> Params { get; } = [];

    public string ReturnsText { get; set; } = "";

    public QName? ReturnsType { get; set; }

    public List<ColumnDef> ReturnsTable { get; } = [];

    public Ident? ReturnsTableVariable { get; set; }

    /// <summary>Body statements (procedural code or SQL).</summary>
    public List<Stmt>? Body { get; set; }

    public string Language { get; set; } = "";

    /// <summary>Declared in a PL/SQL package specification (no body).</summary>
    public bool SpecOnly { get; set; }
}

public sealed class CreateTriggerStmt(QName name) : Stmt
{
    public QName Name { get; } = name;

    public QName? Table { get; set; }

    public List<Ident> OfColumns { get; } = [];

    public Expr? When { get; set; }

    public FuncCall? Executes { get; set; }

    public List<Stmt>? Body { get; set; }

    public List<(Ident Name, bool IsNew)> Referencing { get; } = [];
}

public sealed class CreatePackageStmt(QName name, bool isBody) : Stmt
{
    public QName Name { get; } = name;

    public bool IsBody { get; } = isBody;

    public List<Stmt> Members { get; } = [];
}

public enum AlterKind
{
    AddColumn,
    DropColumn,
    RenameColumn,
    RenameTable,
    AddConstraint,
    DropConstraint,
    RenameConstraint,
    AlterColumn,
    Other,
}

public sealed class AlterAction(AlterKind kind) : Stmt
{
    public AlterKind Kind { get; } = kind;

    public ColumnDef? Column { get; set; }

    public Ident? Name { get; set; }

    public Ident? NewName { get; set; }

    public QName? NewTable { get; set; }

    public ConstraintDef? Constraint { get; set; }

    public List<Expr> Exprs { get; } = [];
}

public sealed class AlterTableStmt(QName name) : Stmt
{
    public QName Name { get; } = name;

    public List<AlterAction> Actions { get; } = [];
}

/// <summary>ALTER VIEW / INDEX / SEQUENCE / FUNCTION / SCHEMA ... (optionally RENAME TO).</summary>
public sealed class AlterObjectStmt(ObjectKind kind, QName name) : Stmt
{
    public ObjectKind Kind { get; } = kind;

    public QName Name { get; } = name;

    public QName? RenameTo { get; set; }

    public List<Expr> Exprs { get; } = [];

    /// <summary><c>ALTER TYPE t ADD VALUE 'x'</c> labels.</summary>
    public List<Token> AddedLabels { get; } = [];
}

public sealed class RenameTableStmt : Stmt
{
    public List<(QName From, QName To)> Pairs { get; } = [];
}

public sealed class DropStmt(ObjectKind kind) : Stmt
{
    public ObjectKind Kind { get; } = kind;

    public List<QName> Names { get; } = [];

    /// <summary>Parameter types of dropped routines (parallel to <see cref="Names"/>; null when not given).</summary>
    public List<List<string>?> Signatures { get; } = [];

    public QName? OnTable { get; set; }
}

public sealed class CommentOnStmt(ObjectKind kind, QName target, string text) : Stmt
{
    public ObjectKind Kind { get; } = kind;

    public QName Target { get; } = target;

    public string Text { get; } = text;
}

public sealed class QueryStmt(Query query) : Stmt
{
    public Query Query { get; } = query;
}

public sealed class SetItem(List<Expr> targets, Expr value)
{
    public List<Expr> Targets { get; } = targets;

    public Expr Value { get; } = value;
}

public sealed class InsertStmt(TableRef target) : Stmt
{
    public TableRef Target { get; } = target;

    public List<Cte> With { get; } = [];

    public List<Ident>? Columns { get; set; }

    /// <summary>MySQL 8 <c>VALUES (...) AS new</c> row alias.</summary>
    public Ident? RowAlias { get; set; }

    public Query? Source { get; set; }

    public List<SetItem> Sets { get; } = [];

    public List<Ident> ConflictColumns { get; } = [];

    public List<Expr> ConflictExprs { get; } = [];

    public List<SetItem> ConflictSets { get; } = [];

    public List<Expr> Returning { get; } = [];
}

public sealed class UpdateStmt : Stmt
{
    public List<Cte> With { get; } = [];

    public List<TableRef> Targets { get; } = [];

    public List<SetItem> Sets { get; } = [];

    public List<TableRef> From { get; } = [];

    public Expr? Where { get; set; }

    public List<Expr> Returning { get; } = [];
}

public sealed class DeleteStmt : Stmt
{
    public List<Cte> With { get; } = [];

    /// <summary>Explicit targets of <c>DELETE a FROM t a JOIN ...</c> (T-SQL, MySQL); otherwise the first FROM table.</summary>
    public List<QName> Targets { get; } = [];

    public List<TableRef> From { get; } = [];

    public Expr? Where { get; set; }

    public List<Expr> Returning { get; } = [];
}

public sealed class MergeClause(Expr? condition, string action) : Stmt
{
    public Expr? Condition { get; } = condition;

    public string Action { get; } = action;

    public List<SetItem> Sets { get; } = [];

    public List<Ident>? InsertColumns { get; set; }

    public Expr? Values { get; set; }
}

public sealed class MergeStmt(TableRef target, TableRef? source, Expr? on) : Stmt
{
    public List<Cte> With { get; } = [];

    public TableRef Target { get; } = target;

    public TableRef? Source { get; } = source;

    public Expr? On { get; } = on;

    public List<MergeClause> Clauses { get; } = [];

    public List<Expr> Returning { get; } = [];
}

/// <summary><c>CALL p(...)</c>, <c>EXEC p @a = 1</c>, <c>PERFORM f(...)</c>.</summary>
public sealed class CallStmt(QName name, List<Expr> args) : Stmt
{
    public QName Name { get; } = name;

    public List<Expr> Args { get; } = args;

    /// <summary>T-SQL named arguments (<c>@param = value</c>).</summary>
    public List<Token> NamedArgs { get; } = [];
}

/// <summary><c>FOR r IN query LOOP</c> (PL/pgSQL, PL/SQL cursor FOR loops).</summary>
public sealed class ForLoopStmt(Ident variable) : Stmt
{
    public Ident Variable { get; } = variable;

    public Query? Query { get; set; }

    public Expr? Range { get; set; }
}

/// <summary>Statements that only reference tables (TRUNCATE, LOCK, GRANT ... ON, ANALYZE, VACUUM, ...).</summary>
public sealed class TableRefsStmt : Stmt
{
    public List<QName> Tables { get; } = [];

    /// <summary>Column lists (parallel to <see cref="Tables"/>; empty when none).</summary>
    public List<List<Ident>> Columns { get; } = [];

    public List<Expr> Exprs { get; } = [];
}

/// <summary>Anything else: the expressions (and nested queries) it contains are still resolved.</summary>
public sealed class GenericStmt : Stmt
{
    public List<Expr> Exprs { get; } = [];

    /// <summary>Assignment targets (<c>x := ...</c>, <c>SET @x = ...</c>).</summary>
    public List<Expr> Targets { get; } = [];
}
