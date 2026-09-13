namespace ModuLens.Core.Git;

/// <summary>Provides Git file versions without exposing process details to Core.</summary>
public interface IGitService
{
    /// <summary>Gets the source file version stored at HEAD, when one exists.</summary>
    /// <param name="filePath">The absolute or caller-resolved working file path.</param>
    /// <param name="cancellationToken">A token that may cancel Git execution.</param>
    Task<GitFileBaseline> GetHeadVersionAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
