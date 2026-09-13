namespace ModuLens.App.Git;

/// <summary>Runs Git without exposing shell command construction.</summary>
public interface IGitProcessRunner
{
    /// <summary>Runs one executable with an explicit argument vector.</summary>
    Task<GitProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}

/// <summary>Contains one completed Git process result.</summary>
public sealed record GitProcessResult(
    int ExitCode,
    byte[] StandardOutput,
    string StandardError);
