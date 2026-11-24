using System.IO;
using System.Text;

namespace LiteExplorer.Services;

/// <summary>
/// Reads the start of a text file, including old Hebrew files saved as Windows-1255.
/// </summary>
public static class TextPreviewReader
{
    private const int MaxBytes = 64 * 1024;

    private static readonly Encoding Hebrew = CreateHebrew();

    public static async Task<string> ReadAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        var buffer = new byte[MaxBytes];
        int count = await stream.ReadAtLeastAsync(buffer, MaxBytes, throwOnEndOfStream: false, token);
        return Decode(buffer.AsSpan(0, count));
    }

    internal static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Encoding.UTF8.Preamble))
            return Encoding.UTF8.GetString(bytes[3..]);
        if (bytes.StartsWith(Encoding.Unicode.Preamble))
            return Encoding.Unicode.GetString(bytes[2..]);
        if (bytes.StartsWith(Encoding.BigEndianUnicode.Preamble))
            return Encoding.BigEndianUnicode.GetString(bytes[2..]);

        try
        {
            // Not flushed, because the limit may cut the last character in half.
            var decoder = new UTF8Encoding(false, throwOnInvalidBytes: true).GetDecoder();
            var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
            int count = decoder.GetChars(bytes, chars, flush: bytes.Length < MaxBytes);
            return new string(chars, 0, count);
        }
        catch (DecoderFallbackException)
        {
            return Hebrew.GetString(bytes);
        }
    }

    private static Encoding CreateHebrew()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1255);
    }
}
