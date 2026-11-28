namespace ScipSharp.Indexing;

/// <summary>
/// Runs work on a dedicated thread with a large stack.
/// </summary>
/// <remarks>
/// Parsers and tree walks recurse once per nesting level of the source (a long <c>a + b + c ...</c>, deeply nested
/// blocks), and running out of stack kills the process: a <see cref="StackOverflowException"/> cannot be caught. The
/// indexers guard their recursion with <see cref="System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack"/>
/// and give up on a construct that is too deep for the stack they have; a large stack makes that limit one that real
/// code does not reach. Thread pool threads keep the default stack, so work an indexer runs in parallel relies on its
/// guards alone.
/// </remarks>
public static class LargeStackThread
{
    /// <summary>The stack size of <see cref="Run{T}"/>: 256 MB, reserved address space that is committed only as used.</summary>
    public const int DefaultStackSize = 256 * 1024 * 1024;

    /// <summary>Runs <paramref name="work"/> on a new thread with a stack of <paramref name="stackSize"/> bytes and waits for it.</summary>
    public static Task<T> Run<T>(Func<Task<T>> work, int stackSize = DefaultStackSize)
    {
        ArgumentNullException.ThrowIfNull(work);
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(
            () =>
            {
                try
                {
                    // The synchronous part of the work, up to its first await that does not complete at once, runs on
                    // this thread; for the indexers that is the parsing and indexing.
                    done.SetResult(work().GetAwaiter().GetResult());
                }
#pragma warning disable CA1031 // the exception is handed to the caller
                catch (Exception e)
#pragma warning restore CA1031
                {
                    done.SetException(e);
                }
            },
            stackSize)
        {
            Name = "scip-indexer",
            IsBackground = true,
        };
        thread.Start();
        return done.Task;
    }
}
