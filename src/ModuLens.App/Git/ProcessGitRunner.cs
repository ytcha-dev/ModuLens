using System.Diagnostics;
using System.IO;
using System.Text;

namespace ModuLens.App.Git;

/// <summary>Runs local Git directly without a command shell.</summary>
public sealed class ProcessGitRunner : IGitProcessRunner
{
    /// <inheritdoc />
    public async Task<GitProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(executable)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new GitCommandException($"Unable to start {executable}.");
        }

        using var standardOutput = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(
            standardOutput,
            cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new GitProcessResult(
            process.ExitCode,
            standardOutput.ToArray(),
            await errorTask);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and the kill request.
        }
    }
}
