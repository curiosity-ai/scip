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
        "abstract_bodies","abstract_interface","args","arrays","attached_class_recovery","binary_hover","blocks_lambdas_procs",
        "cattr","classes","constructor_navigation","def_delegator","delegate","field_inheritance","fields_and_attrs","for",
        "freeze_constants","functions","gem_metadata","globals","hashes","hoverdocs","implicit_super_arg","indexed_assignment",
        "inheritance","keyword_argument_errors","keyword_parameter_returns","legacy_single_argument_union","literal_receiver",
        "loops_and_conditionals","mattr","metaprog","method_inheritance","method_redefinitions","missing_dsl_instance_methods",
        "module_function","nested_constants","nilable_shape_union_intersection","non_existent","opus","rbs_errors",
        "rbs_keyword_arguments","rbs_signatures","requires","requires_ancestor","rescue","retained_receiver_types",
        "return_operands","safe_navigation","safe_navigation_hover","shape_union_intersection","singleton","singleton_class",
        "super_call","t_helpers","tuple_comparison_blocks","tuple_shape_merge","type_docs","typed_false","typed_ignore",
        "typed_strong","unresolved_constant_scopes","upstream_anonymous_forwarding","upstream_block_locals",
        "upstream_destructuring","upstream_implicit_it","upstream_local_scopes","upstream_punned_kwargs","upstream_wrapped_calls",
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
