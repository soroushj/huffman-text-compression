using System.Text;
using HuffmanTextCompression;

const string usage = """
    Usage:
      HuffmanTextCompression compress <input> <output>
      HuffmanTextCompression decompress <input> <output>
    """;

if (args.Length != 3 || (args[0] != "compress" && args[0] != "decompress"))
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
        long inputSize = new FileInfo(input).Length;
        long outputSize = new FileInfo(output).Length;

        Console.WriteLine(info);
        Console.WriteLine($"{inputSize:n0} -> {outputSize:n0} byte(s) ({(inputSize == 0 ? 0 : 100.0 * outputSize / inputSize):0.#}% of input)");
    }
    else
    {
        string text = HuffmanCompressor.Decode(input);
        File.WriteAllText(output, text);

        Console.WriteLine($"{text.Length:n0} char(s) total");
        Console.WriteLine($"{new FileInfo(input).Length:n0} -> {new FileInfo(output).Length:n0} byte(s)");
    }
}
catch (EncoderFallbackException)
{
    // A corrupted file can decode to text that is not valid Unicode
    Console.Error.WriteLine($"Error: File '{input}' is invalid or corrupted.");
    return 1;
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or NotSupportedException)
{
    Console.Error.WriteLine($"Error: {e.Message}");
    return 1;
}

return 0;
