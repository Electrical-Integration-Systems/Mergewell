using Mergewell.Core.Services;

namespace Mergewell.Tests;

public sealed class TraversalServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"MergewellTests-{Guid.NewGuid():N}");

    public TraversalServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void GetInputFiles_UsesDirectoryFirstDepthFirstOrder()
    {
        Directory.CreateDirectory(Path.Combine(_root, "B"));
        Directory.CreateDirectory(Path.Combine(_root, "A", "Nested"));
        File.WriteAllText(Path.Combine(_root, "root.pdf"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "A", "a.docx"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "A", "Nested", "nested.pdf"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "B", "b.rtf"), string.Empty);

        var relativePaths = new TraversalService().GetInputFiles(_root)
            .Select(path => Path.GetRelativePath(_root, path))
            .ToArray();

        Assert.Equal([Path.Combine("A", "Nested", "nested.pdf"), Path.Combine("A", "a.docx"), Path.Combine("B", "b.rtf"), "root.pdf"], relativePaths);
    }

    [Fact]
    public void GetInputFiles_IgnoresUnsupportedAndWordLockFiles()
    {
        File.WriteAllText(Path.Combine(_root, "keep.pdf"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "~$lock.docx"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "ignore.txt"), string.Empty);

        var files = new TraversalService().GetInputFiles(_root);

        Assert.Single(files);
        Assert.EndsWith("keep.pdf", files[0]);
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }
}