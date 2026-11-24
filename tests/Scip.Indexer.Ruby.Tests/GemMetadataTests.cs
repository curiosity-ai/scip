namespace Scip.Indexer.Ruby.Tests;

/// <summary>Port of the <c>GemMetadataInference</c> unit test in upstream <c>test/scip_test_runner.cc</c>.</summary>
public class GemMetadataTests
{
    private static (GemMetadata Metadata, List<GemMetadataError> Errors) Infer(string fileName, string content)
    {
        var dir = Directory.CreateTempSubdirectory("gemmeta").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, fileName), content);
            return GemMetadataReader.ReadFromDirectory(dir);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task GemfileLockPathSpec()
    {
        var (m, errors) = Infer("Gemfile.lock", "\nPATH\n  remote: .\n    specs:\n      sciptest (0.2)\n            ");
        await Assert.That(m).IsEqualTo(new GemMetadata("sciptest", "0.2"));
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task Gemspec()
    {
        var (m, errors) = Infer("sciptest.gemspec", "\n  spec.name = \"sciptest\"\n  spec.version = \"0.2\"\n            ");
        await Assert.That(m).IsEqualTo(new GemMetadata("sciptest", "0.2"));
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task UnparsableGemfileLockFallsBackToDirectoryName()
    {
        var (m, errors) = Infer("Gemfile.lock", "");
        await Assert.That(m.Version).IsEqualTo("latest");
        await Assert.That(errors).Contains(GemMetadataError.FailedToParseGemfileLock);
    }

    [Test]
    public async Task GemspecWithNonLiteralNameUsesFileBaseName()
    {
        var (m, errors) = Infer("sciptest.gemspec", "\n  s.name = NOT_A_LITERAL_OOPS\n  s.version = \"0.1\"\n            ");
        await Assert.That(m).IsEqualTo(new GemMetadata("sciptest", "0.1"));
        await Assert.That(errors).IsEquivalentTo(new[] { GemMetadataError.FailedToParseName });
    }

    [Test]
    public async Task GemspecWithNonLiteralVersionDefaultsToLatest()
    {
        var (m, errors) = Infer("sciptest.gemspec", "\n  s.name = \"sciptest\"\n  s.version = NOT_A_LITERAL_OOPS\n            ");
        await Assert.That(m).IsEqualTo(new GemMetadata("sciptest", "latest"));
        await Assert.That(errors).IsEquivalentTo(new[] { GemMetadataError.FailedToParseVersion });
    }

    [Test]
    public async Task TryParseRequiresNameAtVersion()
    {
        await Assert.That(GemMetadata.TryParse("a@1")).IsEqualTo(new GemMetadata("a", "1"));
        await Assert.That(GemMetadata.TryParse("a")).IsNull();
        await Assert.That(GemMetadata.TryParse("a@")).IsNull();
        await Assert.That(GemMetadata.TryParse("a@b@c")).IsNull();
    }
}
