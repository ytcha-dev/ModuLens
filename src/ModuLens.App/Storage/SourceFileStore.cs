using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ModuLens.App.Storage;

/// <summary>
/// Loads and safely replaces source files while retaining their detected encoding.
/// </summary>
public sealed class SourceFileStore
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

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
        var detected = DetectEncoding(bytes);
        var text = detected.Encoding.GetString(
            bytes,
            detected.PreambleLength,
            bytes.Length - detected.PreambleLength);

        return new SourceFileSnapshot(
            fullPath,
            text,
            detected.Encoding,
            bytes[..detected.PreambleLength],
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

        var encodedText = snapshot.Encoding.GetBytes(updatedText);
        var outputBytes = new byte[snapshot.Preamble.Length + encodedText.Length];
        snapshot.Preamble.CopyTo(outputBytes, 0);
        encodedText.CopyTo(outputBytes, snapshot.Preamble.Length);

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

    private static DetectedEncoding DetectEncoding(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
        {
            return new DetectedEncoding(new UTF32Encoding(true, true, true), 4);
        }

        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
        {
            return new DetectedEncoding(new UTF32Encoding(false, true, true), 4);
        }

        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return new DetectedEncoding(new UTF8Encoding(true, true), 3);
        }

        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            return new DetectedEncoding(new UnicodeEncoding(true, true, true), 2);
        }

        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            return new DetectedEncoding(new UnicodeEncoding(false, true, true), 2);
        }

        return new DetectedEncoding(Utf8WithoutBom, 0);
    }

    private sealed record DetectedEncoding(Encoding Encoding, int PreambleLength);
}
