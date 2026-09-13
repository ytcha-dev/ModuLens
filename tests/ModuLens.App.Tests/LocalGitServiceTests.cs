using System.Text;
using ModuLens.App.Git;
using ModuLens.Core.Git;

namespace ModuLens.App.Tests;

public sealed class LocalGitServiceTests
{
    [Fact]
    public async Task GetHeadVersionAsync_ReadsWorkingTreeFilteredHeadContentWithArgumentVectors()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "ModuLens Git Fixture");
        var filePath = Path.Combine(repositoryRoot, "src", "sample file.js");
        var headText = "/**\n * ===\n * Head\n * ===\n */\nhead();\n";
        var runner = new FakeGitProcessRunner(
            Success(repositoryRoot + "\n"),
            Success("abc123\n"),
            Success(string.Empty),
            new GitProcessResult(
                0,
                [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(headText)],
                string.Empty));
        var service = new LocalGitService(runner);

        var baseline = await service.GetHeadVersionAsync(filePath);

        Assert.Equal(GitFileBaselineKind.Available, baseline.Kind);
        Assert.Equal("abc123", baseline.Revision);
        Assert.Equal(headText, baseline.Text);
        Assert.Equal(4, runner.Calls.Count);
        Assert.Equal(
            ["-C", Path.GetDirectoryName(filePath)!, "rev-parse", "--path-format=absolute", "--show-toplevel"],
            runner.Calls[0].Arguments);
        Assert.Equal(
            ["-C", repositoryRoot, "cat-file", "-e", "HEAD:src/sample file.js"],
            runner.Calls[2].Arguments);
        Assert.Equal(
            ["-C", repositoryRoot, "cat-file", "--filters", "--path=src/sample file.js", "HEAD:src/sample file.js"],
            runner.Calls[3].Arguments);
        Assert.All(runner.Calls, call => Assert.Equal("git", call.Executable));
    }

    [Fact]
    public async Task GetHeadVersionAsync_OutsideRepositoryReturnsNotInRepository()
    {
        var runner = new FakeGitProcessRunner(new GitProcessResult(128, [], "not a repository"));

        var baseline = await new LocalGitService(runner).GetHeadVersionAsync(
            Path.Combine(Path.GetTempPath(), "plain", "file.js"));

        Assert.Equal(GitFileBaselineKind.NotInRepository, baseline.Kind);
        Assert.Single(runner.Calls);
    }

    [Fact]
    public async Task GetHeadVersionAsync_WithoutHeadReturnsNotPresentAtHead()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "new-repository");
        var runner = new FakeGitProcessRunner(
            Success(repositoryRoot + "\n"),
            new GitProcessResult(128, [], "unknown revision"));

        var baseline = await new LocalGitService(runner).GetHeadVersionAsync(
            Path.Combine(repositoryRoot, "new.js"));

        Assert.Equal(GitFileBaselineKind.NotPresentAtHead, baseline.Kind);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task GetHeadVersionAsync_FileMissingFromHeadReturnsNotPresentAtHead()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "tracked-repository");
        var runner = new FakeGitProcessRunner(
            Success(repositoryRoot + "\n"),
            Success("abc123\n"),
            new GitProcessResult(128, [], "missing"));

        var baseline = await new LocalGitService(runner).GetHeadVersionAsync(
            Path.Combine(repositoryRoot, "untracked.js"));

        Assert.Equal(GitFileBaselineKind.NotPresentAtHead, baseline.Kind);
        Assert.Equal(3, runner.Calls.Count);
    }

    private static GitProcessResult Success(string standardOutput) =>
        new(0, Encoding.UTF8.GetBytes(standardOutput), string.Empty);

    private sealed class FakeGitProcessRunner(params GitProcessResult[] results) : IGitProcessRunner
    {
        private readonly Queue<GitProcessResult> results = new(results);

        public List<ProcessCall> Calls { get; } = [];

        public Task<GitProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(new ProcessCall(executable, arguments.ToArray()));
            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed record ProcessCall(
        string Executable,
        IReadOnlyList<string> Arguments);
}
