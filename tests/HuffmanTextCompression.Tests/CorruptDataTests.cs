using static HuffmanTextCompression.Tests.TestHelpers;

namespace HuffmanTextCompression.Tests;

// Decoding invalid data must either succeed (if the data happens to be valid) or throw InvalidDataException, never
// anything else
public class CorruptDataTests
{
    private static readonly byte[] ValidData = Encode("breathe, breathe in the air").Data;

    private static void AssertDecodesOrThrowsInvalidData(byte[] data)
    {
        try
        {
            Decode(data);
        }
        catch (InvalidDataException)
        {
        }
    }

    [Fact]
    public void TruncatedData()
    {
        for (int length = 1; length < ValidData.Length; length++)
        {
            AssertDecodesOrThrowsInvalidData(ValidData[..length]);
        }
    }

    [Fact]
    public void RandomlyModifiedData()
    {
        Random random = new(1);

        for (int i = 0; i < 10_000; i++)
        {
            byte[] data = (byte[])ValidData.Clone();

            for (int j = random.Next(1, 4); j > 0; j--)
            {
                data[random.Next(data.Length)] = (byte)random.Next(256);
            }

            AssertDecodesOrThrowsInvalidData(data);
        }
    }

    [Fact]
    public void RandomData()
    {
        Random random = new(2);

        for (int i = 0; i < 10_000; i++)
        {
            byte[] data = new byte[random.Next(1, 64)];
            random.NextBytes(data);

            AssertDecodesOrThrowsInvalidData(data);
        }
    }

    [Fact]
    public void UnsupportedFormatVersion()
    {
        byte[] data = (byte[])ValidData.Clone();
        data[0] |= 1 << 2;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Decode(data));
        Assert.Contains("unsupported format version", exception.Message);
    }

    // A tree of only 0 bits is a chain of internal nodes, as deep as the data allows (this used to overflow the stack)
    [Fact]
    public void DeepTree()
    {
        int treeSize = 100_000;
        byte[] data = new byte[4 + treeSize + 1];
        data[0] = (byte)(treeSize >> 16);
        data[1] = (byte)(treeSize >> 8);
        data[2] = (byte)treeSize;
        data[3] = 0x88;

        Assert.Throws<InvalidDataException>(() => Decode(data));
    }
}
