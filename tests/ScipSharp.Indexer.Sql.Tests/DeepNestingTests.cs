namespace ScipSharp.Indexer.Sql.Tests;

/// <summary>
/// Pathological inputs that used to overflow the stack (which aborts the process): long left-deep JOIN / UNION chains
/// and deeply nested parentheses or subqueries. Run on a thread with a small stack so they reproduce deterministically.
/// </summary>
public class DeepNestingTests
{
    private static Index IndexOnSmallStack(string sql)
    {
        Index? index = null;
        Exception? error = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
                }
                catch (Exception e)
                {
                    error = e;
                }
            },
            maxStackSize: 512 * 1024);
        thread.Start();
        thread.Join();
        return error is null ? index! : throw new InvalidOperationException("indexing failed", error);
    }

    private static int RefCount(Index index, string symbol) =>
        index.Documents.Single().Occurrences.Count(o => (o.SymbolRoles & (int)SymbolRole.Definition) == 0 && TestSupport.Short(o.Symbol) == symbol);

    [Test]
    public async Task LongJoinChainIsBoundIteratively()
    {
        const int n = 8_000;
        var sql = "CREATE TABLE t (a int);\nSELECT 1 FROM t" + string.Concat(Enumerable.Repeat(" JOIN t ON true", n)) + ";\nSELECT a FROM t z;";
        var index = IndexOnSmallStack(sql);
        await Assert.That(RefCount(index, "public/t#")).IsEqualTo(n + 2);
        await Assert.That(RefCount(index, "public/t#a.")).IsEqualTo(1);
    }

    [Test]
    public async Task LongUnionChainIsBoundIteratively()
    {
        const int n = 8_000;
        var sql = "CREATE TABLE t (a int);\n" + string.Join(" UNION ALL ", Enumerable.Repeat("SELECT a FROM t", n)) + " ORDER BY a;";
        var index = IndexOnSmallStack(sql);
        await Assert.That(RefCount(index, "public/t#")).IsEqualTo(n);
        await Assert.That(RefCount(index, "public/t#a.")).IsEqualTo(n + 1); // + ORDER BY a
    }

    [Test]
    public async Task DeepNestingSkipsOnlyTheStatement()
    {
        const int n = 50_000;
        var sql = "CREATE TABLE t (a int);\n"
            + "SELECT " + new string('(', n) + "1" + new string(')', n) + ";\n"
            + "SELECT * FROM t WHERE a IN " + string.Concat(Enumerable.Repeat("(SELECT a FROM t WHERE a IN ", n)) + "(1)" + new string(')', n) + ";\n"
            + "SELECT 1 FROM " + string.Concat(Enumerable.Repeat("(SELECT * FROM ", n)) + "t" + string.Concat(Enumerable.Repeat(") x", n)) + ";\n"
            + "SELECT " + string.Concat(Enumerable.Repeat("f(", n)) + "1" + new string(')', n) + ";\n"
            + "SELECT a FROM t AS last_one;";
        var index = IndexOnSmallStack(sql);
        await Assert.That(TestSupport.SymbolsAt(index, sql, "last_one", definitions: true)).IsEquivalentTo(new[] { "local" });
    }
}
