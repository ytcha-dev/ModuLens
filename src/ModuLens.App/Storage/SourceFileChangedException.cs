using System.IO;

namespace ModuLens.App.Storage;

/// <summary>
/// Indicates that a source file no longer matches the bytes originally loaded.
/// </summary>
public sealed class SourceFileChangedException : IOException
{
    /// <summary>Initializes an external-change error for a source path.</summary>
    /// <param name="filePath">The source file changed outside ModuLens.</param>
    public SourceFileChangedException(string filePath)
        : base($"The source file changed outside ModuLens: {filePath}")
    {
        FilePath = filePath;
    }

    /// <summary>Gets the source file that failed the conflict check.</summary>
    public string FilePath { get; }
}
