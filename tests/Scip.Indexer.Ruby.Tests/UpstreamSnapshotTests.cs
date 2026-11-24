namespace Scip.Indexer.Ruby.Tests;

/// <summary>
/// Runs the upstream scip-ruby snapshot fixtures (<c>test/scip/testdata/*.snapshot.rb</c>, copied under
/// <c>Fixtures/snapshots</c>) against the native indexer. See <see cref="SnapshotRunner"/> for what is compared.
/// </summary>
public class UpstreamSnapshotTests
{
    /// <summary>Fixtures whose definitions and in-file references match upstream exactly.</summary>
    public static IEnumerable<string> PassingFixtures() =>
    [
        "abstract_bodies","abstract_interface","arrays","binary_hover","blocks_lambdas_procs","for","freeze_constants","functions",
        "gem_metadata","globals","hashes","implicit_super_arg","legacy_single_argument_union","literal_receiver","loops_and_conditionals",
        "metaprog","method_inheritance","non_existent","opus","rbs_errors","requires","requires_ancestor","rescue","retained_receiver_types",
        "return_operands","safe_navigation_hover","singleton","t_helpers","tuple_comparison_blocks","type_docs","typed_ignore",
        "upstream_anonymous_forwarding","upstream_block_locals","upstream_destructuring","upstream_implicit_it","upstream_local_scopes",
        "yield_block",
    ];

    [Test]
    [MethodDataSource(nameof(PassingFixtures))]
    public async Task FixtureMatchesUpstream(string name)
    {
        var result = SnapshotRunner.Run(name);
        await Assert.That(string.Join('\n', result.Problems)).IsEqualTo("");
    }

    /// <summary>
    /// Guards overall coverage across every upstream fixture. Misses are expected for things that need Sorbet's type
    /// inference, DSL rewriters, RBI payloads or generics (documented in README.md); the floor only stops regressions.
    /// </summary>
    [Test]
    public async Task OverallDefinitionRecallStaysAboveFloor()
    {
        int defs = 0, missing = 0, fixtures = 0;
        foreach (var name in SnapshotRunner.FixtureNames())
        {
            var r = SnapshotRunner.Run(name);
            defs += r.ExpectedDefinitions;
            missing += r.MissingDefinitions;
            fixtures++;
        }

        await Assert.That(fixtures).IsGreaterThan(100);
        await Assert.That((defs - missing) / (double)defs).IsGreaterThan(0.7);
    }
}
