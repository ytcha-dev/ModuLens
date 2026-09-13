using System.Text;

namespace ModuLens.App.Storage;

/// <summary>Holds the decoded text and byte-level save metadata for one load.</summary>
public sealed class SourceFileSnapshot
{
    internal SourceFileSnapshot(
        string filePath,
        string text,
        Encoding encoding,
        byte[] preamble,
        byte[] contentHash)
    {
        FilePath = filePath;
        Text = text;
        Encoding = encoding;
        Preamble = preamble;
        ContentHash = contentHash;
    }

    /// <summary>Gets the normalized absolute source path.</summary>
    public string FilePath { get; }

    /// <summary>Gets the exact decoded source text, excluding a byte-order mark.</summary>
    public string Text { get; }

    internal Encoding Encoding { get; }

    internal byte[] Preamble { get; }

    internal byte[] ContentHash { get; }
}
