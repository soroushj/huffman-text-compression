using System.Text;
using HuffmanTextCompression;

// Known limitation: the input of compress is read as UTF-8 text and the output of decompress is written as UTF-8
// without BOM, so a BOM or invalid UTF-8 bytes in the original text file are not preserved by a round-trip

const string Usage = """
    Usage:
      huff compress <input> <output>    Compress a UTF-8 text file
      huff decompress <input> <output>  Decompress a compressed file
    """;

if (args.Length != 3 || (args[0] != "compress" && args[0] != "decompress") || args[1] == "" || args[2] == "")
{
    Console.Error.WriteLine(Usage);
    return 2;
}

string input = args[1];
string output = args[2];

try
{
    if (args[0] == "compress")
    {
        // Compress to memory first, so no output file is created if compression fails
        using MemoryStream compressed = new();
        CompressionInfo info;
        using (StreamReader text = File.OpenText(input))
        {
            info = HuffmanCompressor.Encode(text, compressed);
        }

        File.WriteAllBytes(output, compressed.ToArray());

        Console.WriteLine($"Compressed '{input}' to '{output}'");
        Console.WriteLine(info);
    }
    else
    {
        // Decompress to memory first, so no output file is created if decompression fails
        using StringWriter text = new();
        CompressionInfo info;
        using (FileStream compressed = File.OpenRead(input))
        {
            info = HuffmanCompressor.Decode(compressed, text);
        }

        File.WriteAllText(output, text.ToString());

        Console.WriteLine($"Decompressed '{input}' to '{output}'");
        Console.WriteLine(info);
    }
}
catch (EncoderFallbackException)
{
    // A corrupted file can decode to text that is not valid Unicode
    Console.Error.WriteLine("Error: The compressed data is invalid or corrupted.");
    return 1;
}
catch (Exception e)
    when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
{
    Console.Error.WriteLine($"Error: {e.Message}");
    return 1;
}

return 0;
