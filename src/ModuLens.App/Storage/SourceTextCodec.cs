using System.Text;

namespace ModuLens.App.Storage;

internal static class SourceTextCodec
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

    public static DecodedSourceText Decode(ReadOnlySpan<byte> bytes)
    {
        var detected = DetectEncoding(bytes);
        var text = detected.Encoding.GetString(bytes[detected.PreambleLength..]);
        return new DecodedSourceText(
            text,
            detected.Encoding,
            bytes[..detected.PreambleLength].ToArray());
    }

    public static byte[] Encode(string text, Encoding encoding, ReadOnlySpan<byte> preamble)
    {
        var encodedText = encoding.GetBytes(text);
        var outputBytes = new byte[preamble.Length + encodedText.Length];
        preamble.CopyTo(outputBytes);
        encodedText.CopyTo(outputBytes, preamble.Length);
        return outputBytes;
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

    internal sealed record DecodedSourceText(
        string Text,
        Encoding Encoding,
        byte[] Preamble);

    private sealed record DetectedEncoding(Encoding Encoding, int PreambleLength);
}
