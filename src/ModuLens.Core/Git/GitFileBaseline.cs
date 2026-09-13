namespace ModuLens.Core.Git;

/// <summary>Describes whether HEAD contains a baseline for one source file.</summary>
public sealed class GitFileBaseline
{
    private GitFileBaseline(
        GitFileBaselineKind kind,
        string? revision,
        string? text)
    {
        Kind = kind;
        Revision = revision;
        Text = text;
    }

    /// <summary>Gets the baseline availability state.</summary>
    public GitFileBaselineKind Kind { get; }

    /// <summary>Gets the resolved HEAD commit when a baseline is available.</summary>
    public string? Revision { get; }

    /// <summary>Gets the exact decoded HEAD source when available.</summary>
    public string? Text { get; }

    /// <summary>Creates a baseline containing a file version from HEAD.</summary>
    public static GitFileBaseline Available(string revision, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        ArgumentNullException.ThrowIfNull(text);
        return new GitFileBaseline(GitFileBaselineKind.Available, revision, text);
    }

    /// <summary>Creates a result for a repository whose HEAD lacks the file.</summary>
    public static GitFileBaseline NotPresentAtHead() =>
        new(GitFileBaselineKind.NotPresentAtHead, null, null);

    /// <summary>Creates a result for a file outside a Git work tree.</summary>
    public static GitFileBaseline NotInRepository() =>
        new(GitFileBaselineKind.NotInRepository, null, null);
}

/// <summary>Identifies the available Git baseline state.</summary>
public enum GitFileBaselineKind
{
    /// <summary>The file has a readable version at HEAD.</summary>
    Available,

    /// <summary>The repository has no HEAD commit or the file is absent at HEAD.</summary>
    NotPresentAtHead,

    /// <summary>The source file is not inside a Git work tree.</summary>
    NotInRepository,
}
