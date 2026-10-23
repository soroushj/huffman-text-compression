using System.Text;
using HuffmanTextCompression;

// Known limitation: the input of compress is read as UTF-8 text and the output of decompress is written as
// UTF-8 without BOM, so a BOM or invalid UTF-8 bytes in the original text file are not preserved by a round-trip

const string usage = """
    Usage:
      HuffmanTextCompression compress <input> <output>    Compress a UTF-8 text file
      HuffmanTextCompression decompress <input> <output>  Decompress a compressed file
    """;

if (args.Length != 3 || (args[0] != "compress" && args[0] != "decompress") || args[1] == "" || args[2] == "")
{
    Console.Error.WriteLine(usage);
    return 2;
}

string input = args[1];
string output = args[2];

try
{
    if (args[0] == "compress")
    {
        string text = File.ReadAllText(input);
        CompressionInfo info = HuffmanCompressor.Encode(text, output);

        Console.WriteLine($"Compressed '{input}' to '{output}'");
        PrintSizes(info.TotalChars, new FileInfo(input).Length, info.TotalSize,
            $" (metadata: {info.MetadataSize:n0}, tree: {info.TreeSize:n0}, text: {info.TextSize:n0})");
    }
    else
    {
        string text = HuffmanCompressor.Decode(input);
        File.WriteAllText(output, text);

        Console.WriteLine($"Decompressed '{input}' to '{output}'");
        PrintSizes(text.Length, new FileInfo(output).Length, new FileInfo(input).Length);
    }
}
catch (EncoderFallbackException)
{
    // A corrupted file can decode to text that is not valid Unicode
    Console.Error.WriteLine($"Error: File '{input}' is corrupted or is not a valid compressed file.");
    return 1;
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
{
    Console.Error.WriteLine($"Error: {e.Message}");
    return 1;
}

return 0;

static void PrintSizes(int characters, long uncompressedSize, long compressedSize, string compressedSizeDetails = "")
{
    string ratio = compressedSize == 0 ? "n/a" : $"{(double)uncompressedSize / compressedSize:0.00}";

    Console.WriteLine($"  Characters:        {characters:n0}");
    Console.WriteLine($"  Uncompressed size: {FormatBytes(uncompressedSize)}");
    Console.WriteLine($"  Compressed size:   {FormatBytes(compressedSize)}{compressedSizeDetails}");
    Console.WriteLine($"  Compression ratio: {ratio}");
}

static string FormatBytes(long bytes)
{
    return bytes == 1 ? "1 byte" : $"{bytes:n0} bytes";
}
