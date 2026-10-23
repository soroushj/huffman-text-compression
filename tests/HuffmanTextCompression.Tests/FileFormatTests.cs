using static HuffmanTextCompression.Tests.TestHelpers;

namespace HuffmanTextCompression.Tests;

// Checks the committed .huff files in data/, so that any change to the file format is caught (a change in both Encode
// and Decode would still pass the round-trip tests)
public class FileFormatTests
{
    public static TheoryData<string> FileNames => ["breathe.txt", "single-char.txt", "unicode.txt", "license.txt"];

    [Theory]
    [MemberData(nameof(FileNames))]
    public void EncodeProducesCommittedFile(string fileName)
    {
        string text = File.ReadAllText(DataPath(fileName));
        byte[] expectedData = File.ReadAllBytes(DataPath(fileName + ".huff"));

        Assert.Equal(expectedData, Encode(text).Data);
    }

    [Theory]
    [MemberData(nameof(FileNames))]
    public void DecodeReadsCommittedFile(string fileName)
    {
        string expectedText = File.ReadAllText(DataPath(fileName));
        byte[] data = File.ReadAllBytes(DataPath(fileName + ".huff"));

        Assert.Equal(expectedText, Decode(data).Text);
    }
}
