using System.Text;

namespace HuffmanTextCompression;

// The public entry point: compresses text from a TextReader to a Stream and decompresses it back to a TextWriter. The
// file format is described in README.md.
//
// Encoding works like this:
// 1. HuffmanTree.Build counts the character frequencies and builds the Huffman tree, using HuffmanTreeList as a
//    priority queue.
// 2. HuffmanCodeTable walks the tree to find the code of each character.
// 3. The tree itself is stored in the file (see EncodeTree), followed by the code of each character of the text.
//    Bits are packed most significant bit first, into 16-bit words that are then written as bytes.
//
// Decoding reads the tree back from the file (see DecodeTree), then walks it from the root, bit by bit, emitting a
// character at each leaf. Characters are UTF-16 code units (C# chars), so there are at most 65,536 of them.
public static class HuffmanCompressor
{
    public const int FileFormatVersion = 0;

    // The metadata consists of FileFormatVersion (6 bits), the tree size (18 bits), bitsOfLastByteOfTree (4 bits), and
    // bitsOfLastByteOfText (4 bits), in this order
    private const int MetadataSize = 4;

    // The smallest tree is a single leaf: a 1 bit followed by a 16-bit character (17 bits, so 3 bytes)
    private const int MinTreeSize = 3;

    // A tree has at most 65,536 leaves (17 bits each) and 65,535 internal nodes (1 bit each), so its encoding takes at
    // most 147,456 bytes
    private const int MaxTreeSize = 147456;

    // A non-empty text takes at least 1 bit (so 1 byte)
    private const int MinTextSize = 1;

    // Reads input to the end and writes the encoded text to output (without closing either); returns the compression
    // info
    public static CompressionInfo Encode(TextReader input, Stream output)
    {
        // The whole text is needed up front, to count the character frequencies
        string text = input.ReadToEnd();

        if (text.Length == 0)
        {
            // An empty text is encoded as no data at all
            return new CompressionInfo(0, 0, 0, 0, 0);
        }

        // 0. Generate the tree and the code table

        HuffmanTree tree = HuffmanTree.Build(text);
        HuffmanCodeTable codeTable = new(tree);

        // 1. Encode

        // 1.1. Encode the tree

        // The tree takes 18 bits per leaf minus 1 bit (see EncodeTree), so 2 words per leaf are enough
        ushort[] treeBuffer = new ushort[codeTable.Count * 2];
        int treeBitPosition = 0;

        EncodeTree(tree, treeBuffer, ref treeBitPosition);

        int treeSize = treeBitPosition / 8;
        byte bitsOfLastByteOfTree = (byte)(treeBitPosition % 8);
        if (bitsOfLastByteOfTree == 0)
        {
            bitsOfLastByteOfTree = 8;
        }
        else
        {
            treeSize++;
        }

        // 1.2. Encode the text

        // Each code is at most 16 bits, so 1 word per character is enough
        ushort[] textBuffer = new ushort[text.Length];
        long textBitPosition = 0;

        foreach (char c in text)
        {
            AppendCode(textBuffer, codeTable.GetCode(c), ref textBitPosition);
        }

        int textSize = (int)(textBitPosition / 8);
        byte bitsOfLastByteOfText = (byte)(textBitPosition % 8);
        if (bitsOfLastByteOfText == 0)
        {
            bitsOfLastByteOfText = 8;
        }
        else
        {
            textSize++;
        }

        // 2. Write

        using BinaryWriter writer = new(output, Encoding.UTF8, leaveOpen: true);

        // 2.1. Write the metadata

        // See MetadataSize for the layout
        writer.Write((byte)((FileFormatVersion << 2) | (treeSize >> 16)));
        writer.Write((byte)(treeSize >> 8));
        writer.Write((byte)treeSize);
        writer.Write((byte)((bitsOfLastByteOfTree << 4) | bitsOfLastByteOfText));

        // 2.2. Write the encoded tree

        WriteBuffer(writer, treeBuffer, treeBitPosition);

        // 2.3. Write the encoded text

        WriteBuffer(writer, textBuffer, textBitPosition);

        return new CompressionInfo(text.Length, Encoding.UTF8.GetByteCount(text), MetadataSize, treeSize, textSize);
    }

    // Stores the tree in pre-order: an internal node is a 0 bit followed by its left and right subtrees, and a leaf is
    // a 1 bit followed by its 16-bit character
    private static void EncodeTree(HuffmanTree tree, ushort[] buffer, ref int bitPosition)
    {
        if (tree.IsLeaf)
        {
            // Put a 1 bit
            buffer[bitPosition / 16] |= (ushort)(0x8000 >> (bitPosition % 16));
            bitPosition++;

            // Append the character (16 bits)
            AppendChar(buffer, tree.Character, ref bitPosition);

            return;
        }

        // If it's not a leaf, put a 0 bit
        bitPosition++;

        EncodeTree(tree.Left, buffer, ref bitPosition);
        EncodeTree(tree.Right, buffer, ref bitPosition);
    }

    // Appends code.Code (code.Length bits) to the buffer, starting at bitPosition
    private static void AppendCode(ushort[] buffer, HuffmanCode code, ref long bitPosition)
    {
        int index = (int)(bitPosition / 16);
        // The number of bits that don't fit in the current word and spill into the next one (if positive)
        int split = (int)(bitPosition % 16) - (16 - code.Length);

        if (split > 0)
        {
            buffer[index] |= (ushort)(code.Code >> split);
            buffer[index + 1] = (ushort)(code.Code << (16 - split));
        }
        else if (split == 0)
        {
            buffer[index] |= code.Code;
        }
        else
        {
            // split < 0
            buffer[index] |= (ushort)(code.Code << -split);
        }

        bitPosition += code.Length;
    }

    // Appends the character (16 bits) to the buffer, starting at bitPosition
    private static void AppendChar(ushort[] buffer, char character, ref int bitPosition)
    {
        int index = bitPosition / 16;
        int split = bitPosition % 16;

        if (split == 0)
        {
            buffer[index] = character;
        }
        else
        {
            // split > 0
            buffer[index] |= (ushort)(character >> split);
            buffer[index + 1] = (ushort)(character << (16 - split));
        }

        bitPosition += 16;
    }

    // Writes the first bitLength bits of the buffer as whole bytes (the last byte is padded with 0 bits)
    private static void WriteBuffer(BinaryWriter writer, ushort[] buffer, long bitLength)
    {
        int fullWords = (int)(bitLength / 16);

        for (int i = 0; i < fullWords; i++)
        {
            // 8 most significant bits
            writer.Write((byte)(buffer[i] >> 8));
            // 8 least significant bits
            writer.Write((byte)buffer[i]);
        }

        int remainingBits = (int)(bitLength % 16);

        if (remainingBits > 8)
        {
            // Last two bytes
            // 8 most significant bits
            writer.Write((byte)(buffer[fullWords] >> 8));
            // 8 least significant bits
            writer.Write((byte)buffer[fullWords]);
        }
        else if (remainingBits > 0)
        {
            // Last byte (8 most significant bits)
            writer.Write((byte)(buffer[fullWords] >> 8));
        }
    }

    // Reads input to the end and writes the decoded text to output (without closing either); returns the compression
    // info
    public static CompressionInfo Decode(Stream input, TextWriter output)
    {
        using MemoryStream memory = new();
        input.CopyTo(memory);
        byte[] buffer = memory.ToArray();

        if (buffer.Length == 0)
        {
            // Empty text
            return new CompressionInfo(0, 0, 0, 0, 0);
        }

        // 1. Read the metadata

        // See MetadataSize for the layout
        int fileFormatVersion = buffer[0] >> 2;

        if (fileFormatVersion > FileFormatVersion)
        {
            throw new InvalidDataException($"The compressed data has an unsupported format version ({fileFormatVersion}); it may have been created by a newer version of this program, or it may be corrupted.");
        }

        if (buffer.Length < MetadataSize + MinTreeSize + MinTextSize)
        {
            throw InvalidCompressedDataException();
        }

        int treeSize = ((buffer[0] & 0x03) << 16) | (buffer[1] << 8) | buffer[2];
        int bitsOfLastByteOfTree = buffer[3] >> 4;
        int bitsOfLastByteOfText = buffer[3] & 0x0F;

        if (bitsOfLastByteOfTree < 1 || bitsOfLastByteOfTree > 8 || bitsOfLastByteOfText < 1 || bitsOfLastByteOfText > 8 || treeSize < MinTreeSize || treeSize > MaxTreeSize || buffer.Length < MetadataSize + treeSize + MinTextSize)
        {
            throw InvalidCompressedDataException();
        }

        // 2. Decode the tree

        HuffmanTree tree = new();
        // The tree starts right after the metadata
        int treeBitPosition = MetadataSize * 8;

        try
        {
            // The tree ends after the first bitsOfLastByteOfTree bits of its last byte
            DecodeTree(tree, buffer, ref treeBitPosition, (MetadataSize + treeSize - 1) * 8 + bitsOfLastByteOfTree, 0);
        }
        catch (InvalidDataException e)
        {
            throw InvalidCompressedDataException(e);
        }

        // 3. Decode the text

        // The text starts right after the tree
        int textStart = MetadataSize + treeSize;

        // If the text consists entirely of a single character
        if (tree.IsLeaf)
        {
            for (int i = textStart; i < buffer.Length; i++)
            {
                // The Huffman code for the only character must be 0
                if (buffer[i] != 0)
                {
                    throw InvalidCompressedDataException();
                }
            }

            // The length of the Huffman code is 1, so the number of characters equals the number of bits of the encoded
            // text
            int charCount = (buffer.Length - textStart - 1) * 8 + bitsOfLastByteOfText;

            string singleCharText = new(tree.Character, charCount);
            output.Write(singleCharText);
            return new CompressionInfo(singleCharText.Length, Encoding.UTF8.GetByteCount(singleCharText), MetadataSize, treeSize, buffer.Length - textStart);
        }

        // The whole text is collected first, to compute its UTF-8 size
        StringBuilder decodedText = new();
        HuffmanTree currentNode = tree;

        for (int i = textStart; i < buffer.Length - 1; i++)
        {
            for (byte mask = 0x80; mask != 0; mask >>= 1)
            {
                if ((buffer[i] & mask) != 0)
                {
                    // Reached a 1 bit; go right
                    currentNode = currentNode.Right;
                }
                else
                {
                    // Reached a 0 bit; go left
                    currentNode = currentNode.Left;
                }

                if (currentNode.IsLeaf)
                {
                    // Reached a leaf; read a character
                    decodedText.Append(currentNode.Character);
                    // Go back to the root
                    currentNode = tree;
                }
            }
        }

        // Only some bits of the last byte (bitsOfLastByteOfText) are real data
        for (byte mask = 0x80, count = 0; count < bitsOfLastByteOfText; mask >>= 1, count++)
        {
            if ((buffer[^1] & mask) != 0)
            {
                // Reached a 1 bit; go right
                currentNode = currentNode.Right;
            }
            else
            {
                // Reached a 0 bit; go left
                currentNode = currentNode.Left;
            }

            if (currentNode.IsLeaf)
            {
                // Reached a leaf; read a character
                decodedText.Append(currentNode.Character);
                // Go back to the root
                currentNode = tree;
            }
        }

        string text = decodedText.ToString();
        output.Write(text);
        return new CompressionInfo(text.Length, Encoding.UTF8.GetByteCount(text), MetadataSize, treeSize, buffer.Length - textStart);
    }

    private static InvalidDataException InvalidCompressedDataException(Exception? innerException = null)
    {
        return new InvalidDataException("The compressed data is invalid or corrupted.", innerException);
    }

    // Rebuilds the tree stored by EncodeTree, starting at bitPosition and ending before endPosition
    private static void DecodeTree(HuffmanTree tree, byte[] buffer, ref int bitPosition, int endPosition, int depth)
    {
        if (depth > HuffmanCode.MaxLength)
        {
            // The encoder never produces such a deep tree
            throw new InvalidDataException($"The encoded tree is deeper than {HuffmanCode.MaxLength} levels.");
        }

        // If the bit at bitPosition is 1
        if ((buffer[bitPosition / 8] & (byte)(0x80 >> (bitPosition % 8))) != 0)
        {
            bitPosition += 17;

            if (bitPosition > endPosition)
            {
                throw new InvalidDataException("The encoded tree ends in the middle of a character code.");
            }

            // Read the character stored in the 16 bits after the 1 bit
            tree.Character = ReadChar(buffer, bitPosition - 16);
            return;
        }

        bitPosition++;

        if (bitPosition >= endPosition)
        {
            throw new InvalidDataException("The encoded tree ends before all of its nodes are read.");
        }

        tree.Left = new HuffmanTree();
        DecodeTree(tree.Left, buffer, ref bitPosition, endPosition, depth + 1);

        tree.Right = new HuffmanTree();
        DecodeTree(tree.Right, buffer, ref bitPosition, endPosition, depth + 1);
    }

    // Reads the character stored in 16 bits, starting at bitPosition
    private static char ReadChar(byte[] buffer, int bitPosition)
    {
        int index = bitPosition / 8;
        int split = bitPosition % 8;

        if (split == 0)
        {
            return (char)((buffer[index] << 8) | buffer[index + 1]);
        }

        return (char)((buffer[index] << (8 + split)) | (buffer[index + 1] << split) | (buffer[index + 2] >> (8 - split)));
    }
}

// All sizes are in bytes. UncompressedSize is the size of the text in UTF-8 (without BOM), which is the baseline for
// CompressionRatio.
public readonly record struct CompressionInfo(int CharCount, int UncompressedSize, int MetadataSize, int EncodedTreeSize, int EncodedTextSize)
{
    public int CompressedSize => MetadataSize + EncodedTreeSize + EncodedTextSize;

    // NaN for an empty text (0 / 0)
    public double CompressionRatio => (double)UncompressedSize / CompressedSize;

    public override string ToString()
    {
        string ratio = double.IsNaN(CompressionRatio) ? "n/a" : $"{CompressionRatio:0.00}";

        return string.Join(Environment.NewLine,
            $"Characters:        {CharCount:n0}",
            $"Uncompressed size: {FormatSize(UncompressedSize)}",
            $"Compressed size:   {FormatSize(CompressedSize)} (metadata: {MetadataSize:n0}, tree: {EncodedTreeSize:n0}, text: {EncodedTextSize:n0})",
            $"Compression ratio: {ratio}");
    }

    private static string FormatSize(int bytes)
    {
        return bytes == 1 ? "1 byte" : $"{bytes:n0} bytes";
    }
}
