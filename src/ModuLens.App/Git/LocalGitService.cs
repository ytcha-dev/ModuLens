using System.IO;
using System.Text;
using ModuLens.App.Storage;
using ModuLens.Core.Git;

namespace ModuLens.App.Git;

/// <summary>Reads a source file baseline from the local Git executable.</summary>
public sealed class LocalGitService : IGitService
{
    private readonly IGitProcessRunner processRunner;

    /// <summary>Initializes the service with the production process runner.</summary>
    public LocalGitService()
        : this(new ProcessGitRunner())
    {
    }

    /// <summary>Initializes the service with an explicit process runner.</summary>
    public LocalGitService(IGitProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        this.processRunner = processRunner;
    }

    /// <inheritdoc />
    public async Task<GitFileBaseline> GetHeadVersionAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        var sourceDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new GitCommandException("The source file has no parent directory.");
        var rootResult = await RunGitAsync(
            ["-C", sourceDirectory, "rev-parse", "--path-format=absolute", "--show-toplevel"],
            cancellationToken);
        if (rootResult.ExitCode != 0)
        {
            return GitFileBaseline.NotInRepository();
        }

        var repositoryRoot = DecodeGitMetadata(rootResult.StandardOutput);
        if (repositoryRoot.Length == 0)
        {
            throw new GitCommandException("Git returned an empty repository root.");
        }

        repositoryRoot = Path.GetFullPath(repositoryRoot);
        var relativePath = Path.GetRelativePath(repositoryRoot, fullPath);
        if (IsOutsideRepository(relativePath))
        {
            return GitFileBaseline.NotInRepository();
        }

        var headResult = await RunGitAsync(
            ["-C", repositoryRoot, "rev-parse", "--verify", "HEAD"],
            cancellationToken);
        if (headResult.ExitCode != 0)
        {
            return GitFileBaseline.NotPresentAtHead();
        }

        var revision = DecodeGitMetadata(headResult.StandardOutput);
        if (revision.Length == 0)
        {
            throw new GitCommandException("Git returned an empty HEAD revision.");
        }

        var gitPath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
        var objectName = $"HEAD:{gitPath}";
        var existsResult = await RunGitAsync(
            ["-C", repositoryRoot, "cat-file", "-e", objectName],
            cancellationToken);
        if (existsResult.ExitCode != 0)
        {
            return GitFileBaseline.NotPresentAtHead();
        }

        var showResult = await RunGitAsync(
            ["-C", repositoryRoot, "show", "--no-ext-diff", "--no-textconv", objectName],
            cancellationToken);
        if (showResult.ExitCode != 0)
        {
            throw CreateCommandException("Unable to read the file from HEAD", showResult);
        }

        var headText = SourceTextCodec.Decode(showResult.StandardOutput).Text;
        return GitFileBaseline.Available(revision, headText);
    }

    private Task<GitProcessResult> RunGitAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        processRunner.RunAsync("git", arguments, cancellationToken);

    private static bool IsOutsideRepository(string relativePath) =>
        Path.IsPathRooted(relativePath) ||
        relativePath.Equals("..", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

    private static string DecodeGitMetadata(byte[] bytes) =>
        Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');

    private static GitCommandException CreateCommandException(
        string operation,
        GitProcessResult result)
    {
        var detail = result.StandardError.Trim();
        return new GitCommandException(
            detail.Length == 0
                ? $"{operation}. Git exited with code {result.ExitCode}."
                : $"{operation}. {detail}");
    }
}
