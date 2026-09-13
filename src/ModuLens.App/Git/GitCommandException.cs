using System.IO;

namespace ModuLens.App.Git;

/// <summary>Indicates that a required local Git command failed.</summary>
public sealed class GitCommandException : IOException
{
    /// <summary>Initializes a Git command failure.</summary>
    public GitCommandException(string message)
        : base(message)
    {
    }
}
