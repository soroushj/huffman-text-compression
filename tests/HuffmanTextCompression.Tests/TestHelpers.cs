namespace HuffmanTextCompression.Tests;

static class TestHelpers
{
    public static (byte[] Data, CompressionInfo Info) Encode(string text)
    {
        using StringReader reader = new(text);
        using MemoryStream stream = new();
        CompressionInfo info = HuffmanCompressor.Encode(reader, stream);
        return (stream.ToArray(), info);
    }

    public static (string Text, CompressionInfo Info) Decode(byte[] data)
    {
        using MemoryStream stream = new(data);
        using StringWriter writer = new();
        CompressionInfo info = HuffmanCompressor.Decode(stream, writer);
        return (writer.ToString(), info);
    }

    // Returns the path of a committed test data file (copied next to the test assembly at build time)
    public static string DataPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "data", fileName);
    }
}
