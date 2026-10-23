using System.Text;
using static HuffmanTextCompression.Tests.TestHelpers;

namespace HuffmanTextCompression.Tests;

public class RoundTripTests
{
    // Encodes and decodes the text, and checks the decoded text and the compression info
    private static void AssertRoundTrip(string text)
    {
        (byte[] data, CompressionInfo encodeInfo) = Encode(text);
        (string decodedText, CompressionInfo decodeInfo) = Decode(data);

        Assert.Equal(text, decodedText);
        Assert.Equal(encodeInfo, decodeInfo);
        Assert.Equal(text.Length, encodeInfo.CharCount);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), encodeInfo.UncompressedSize);
        Assert.Equal(data.Length, encodeInfo.CompressedSize);
    }

    // Returns a text of the given length, with characters picked by the given function (seeded, so reproducible)
    private static string RandomText(int seed, int length, Func<Random, char> nextChar)
    {
        Random random = new(seed);
        StringBuilder text = new(length);

        for (int i = 0; i < length; i++)
        {
            text.Append(nextChar(random));
        }

        return text.ToString();
    }

    public static TheoryData<string, string> GeneratedTexts => new()
    {
        { "ASCII", RandomText(1, 10_000, random => (char)random.Next(' ', '~' + 1)) },
        { "all 16-bit chars", RandomText(2, 10_000, random => (char)random.Next(char.MaxValue + 1)) },
        { "emoji (surrogate pairs)", string.Concat(Enumerable.Range(0, 1_000).Select(i => char.ConvertFromUtf32(0x1F600 + i % 80))) },
        { "skewed frequencies", RandomText(3, 10_000, random => (char)('a' + Math.Min(25, (int)-Math.Log2(1 - random.NextDouble())))) },
        { "two distinct chars", RandomText(4, 10_000, random => random.Next(10) == 0 ? 'b' : 'a') },
        { "long (3M chars)", RandomText(5, 3_000_000, random => (char)random.Next('a', 'z' + 1)) },
    };

    [Theory]
    [MemberData(nameof(GeneratedTexts))]
    public void GeneratedText(string description, string text)
    {
        // The description only labels the test case
        _ = description;
        AssertRoundTrip(text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("\0")]
    [InlineData("￿")]
    [InlineData("ab")]
    [InlineData("breathe, breathe in the air")]
    public void ShortText(string text)
    {
        AssertRoundTrip(text);
    }

    // Lengths around byte and word boundaries of the encoded text
    [Theory]
    [MemberData(nameof(Lengths))]
    public void SingleCharText(int length)
    {
        AssertRoundTrip(new string('a', length));
    }

    [Theory]
    [MemberData(nameof(Lengths))]
    public void TwoCharText(int length)
    {
        AssertRoundTrip(RandomText(length, length, random => random.Next(2) == 0 ? 'a' : 'b'));
    }

    public static TheoryData<int> Lengths => [.. Enumerable.Range(1, 40), 100_000];

    // Every possible char once: produces the largest possible tree, with all codes exactly 16 bits long
    [Fact]
    public void AllChars()
    {
        string text = new([.. Enumerable.Range(0, char.MaxValue + 1).Select(i => (char)i)]);

        AssertRoundTrip(text);
        Assert.Equal(147_456, Encode(text).Info.EncodedTreeSize);
    }

    // With Fibonacci frequencies (1, 1, 2, 3, 5, ...), the Huffman tree is a chain, so n distinct chars produce a code
    // of n - 1 bits
    private static string FibonacciText(int distinctChars)
    {
        StringBuilder text = new();

        for (int i = 0, a = 1, b = 1; i < distinctChars; i++, (a, b) = (b, a + b))
        {
            text.Append((char)('A' + i), a);
        }

        return text.ToString();
    }

    [Fact]
    public void LongestSupportedCode()
    {
        AssertRoundTrip(FibonacciText(17));
    }

    [Fact]
    public void TooLongCode()
    {
        using StringReader reader = new(FibonacciText(18));
        using MemoryStream stream = new();

        Assert.Throws<NotSupportedException>(() => HuffmanCompressor.Encode(reader, stream));
        Assert.Equal(0, stream.Length);
    }
}
