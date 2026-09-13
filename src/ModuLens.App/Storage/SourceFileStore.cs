using System.IO;
using System.Security.Cryptography;

namespace ModuLens.App.Storage;

/// <summary>
/// Loads and safely replaces source files while retaining their detected encoding.
/// </summary>
public sealed class SourceFileStore
{
    /// <summary>Loads a source file and captures the bytes needed for conflict detection.</summary>
    /// <param name="filePath">The source file to load.</param>
    /// <param name="cancellationToken">A token that may cancel the read.</param>
    /// <returns>The decoded source and its save metadata.</returns>
    public async Task<SourceFileSnapshot> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        var decoded = SourceTextCodec.Decode(bytes);

        return new SourceFileSnapshot(
            fullPath,
            decoded.Text,
            decoded.Encoding,
            decoded.Preamble,
            SHA256.HashData(bytes));
    }

    /// <summary>
    /// Writes updated text through a same-directory temporary file after verifying
    /// that the source bytes still match the loaded snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot produced by the latest load or save.</param>
    /// <param name="updatedText">The complete updated source text.</param>
    /// <param name="cancellationToken">A token that may cancel the write.</param>
    /// <returns>A new snapshot representing the successfully saved bytes.</returns>
    /// <exception cref="SourceFileChangedException">
    /// Thrown when another process changed the source after it was loaded.
    /// </exception>
    public async Task<SourceFileSnapshot> SaveAsync(
        SourceFileSnapshot snapshot,
        string updatedText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(updatedText);

        var currentBytes = await File.ReadAllBytesAsync(snapshot.FilePath, cancellationToken);
        var currentHash = SHA256.HashData(currentBytes);
        if (!CryptographicOperations.FixedTimeEquals(currentHash, snapshot.ContentHash))
        {
            throw new SourceFileChangedException(snapshot.FilePath);
        }

        var outputBytes = SourceTextCodec.Encode(
            updatedText,
            snapshot.Encoding,
            snapshot.Preamble);

        var directory = Path.GetDirectoryName(snapshot.FilePath)
            ?? throw new IOException("The source file has no parent directory.");
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(snapshot.FilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, outputBytes, cancellationToken);
            File.Move(temporaryPath, snapshot.FilePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return new SourceFileSnapshot(
            snapshot.FilePath,
            updatedText,
            snapshot.Encoding,
            snapshot.Preamble,
            SHA256.HashData(outputBytes));
    }

}
