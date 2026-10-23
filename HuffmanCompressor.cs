using System.Text;

namespace HuffmanTextCompression;

static class HuffmanCompressor
{
    public const int FileFormatVersion = 0;

    // A tree has at most 65,536 leaves (17 bits each) and 65,535 internal nodes (1 bit each),
    // so its encoding takes at most 147,456 bytes
    private const int MaxTreeLengthInBytes = 147456;

    // Encodes `text` and writes it to `filename`; returns the compression info
    public static CompressionInfo Encode(string text, string filename)
    {
        if (text.Length == 0)
        {
            // Create an empty file
            File.WriteAllBytes(filename, []);
            return new CompressionInfo(0, 0, 0, 0);
        }

        // 0. Generate the tree and the code table

        HuffmanTree tree = HuffmanTree.GenerateHuffmanTree(text);
        HuffmanCodeTable codeTable = new(tree);

        // 1. Encode

        // 1.1. Encode the tree

        ushort[] treeBuffer = new ushort[codeTable.NumberOfCodes * 2];
        int treeBitPosition = 0;

        EncodeTree(tree, treeBuffer, ref treeBitPosition);

        int treeLengthInBytes = treeBitPosition / 8;
        byte bitsOfLastByteOfTree = (byte)(treeBitPosition % 8);
        if (bitsOfLastByteOfTree == 0)
        {
            bitsOfLastByteOfTree = 8;
        }
        else
        {
            treeLengthInBytes++;
        }

        // 1.2. Encode the text

        ushort[] encodedTextBuffer = new ushort[text.Length];
        long encodedTextBitPosition = 0;

        foreach (char c in text)
        {
            AppendCode(encodedTextBuffer, codeTable.GetHuffmanCode(c), ref encodedTextBitPosition);
        }

        int encodedTextLengthInBytes = (int)(encodedTextBitPosition / 8);
        byte bitsOfLastByteOfEncodedText = (byte)(encodedTextBitPosition % 8);
        if (bitsOfLastByteOfEncodedText == 0)
        {
            bitsOfLastByteOfEncodedText = 8;
        }
        else
        {
            encodedTextLengthInBytes++;
        }

        // 2. Write

        using BinaryWriter writer = new(File.Open(filename, FileMode.Create));

        // 2.1. Write the metadata

        // The first 6 bits of the file are for FileFormatVersion,
        // the next 18 bits for treeLengthInBytes,
        // the next 4 bits for bitsOfLastByteOfTree,
        // and the next 4 bits for bitsOfLastByteOfEncodedText
        // (4 bytes for the metadata in total)
        writer.Write((byte)((FileFormatVersion << 2) | (treeLengthInBytes >> 16)));
        writer.Write((byte)(treeLengthInBytes >> 8));
        writer.Write((byte)treeLengthInBytes);
        writer.Write((byte)((bitsOfLastByteOfTree << 4) | bitsOfLastByteOfEncodedText));

        // 2.2. Write the encoded tree
        WriteBuffer(writer, treeBuffer, treeBitPosition);

        // 2.3. Write the encoded text
        WriteBuffer(writer, encodedTextBuffer, encodedTextBitPosition);

        return new CompressionInfo(text.Length, 4, treeLengthInBytes, encodedTextLengthInBytes);
    }

    private static void EncodeTree(HuffmanTree tree, ushort[] buffer, ref int bitPosition)
    {
        if (tree.IsLeaf)
        {
            // Put a 1 bit
            buffer[bitPosition / 16] |= (ushort)(0x8000 >> (bitPosition % 16));
            bitPosition++;

            // Append the character code
            AppendCode(buffer, (ushort)tree.Character, ref bitPosition);

            return;
        }

        // If it's not a leaf, put a 0 bit
        bitPosition++;

        EncodeTree(tree.Left, buffer, ref bitPosition);
        EncodeTree(tree.Right, buffer, ref bitPosition);
    }

    private static void AppendCode(ushort[] buffer, HuffmanCode code, ref long bitPosition)
    {
        // Append code.Code (code.Length bits) to the buffer, starting at bitPosition

        int index = (int)(bitPosition / 16);
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

    private static void AppendCode(ushort[] buffer, ushort code, ref int bitPosition)
    {
        // Append the code (16 bits) to the buffer, starting at bitPosition

        int index = bitPosition / 16;
        int split = bitPosition % 16;

        if (split == 0)
        {
            buffer[index] = code;
        }
        else
        {
            // split > 0
            buffer[index] |= (ushort)(code >> split);
            buffer[index + 1] = (ushort)(code << (16 - split));
        }

        bitPosition += 16;
    }

    private static void WriteBuffer(BinaryWriter writer, ushort[] buffer, long bufferLengthInBits)
    {
        int fullWords = (int)(bufferLengthInBits / 16);

        for (int i = 0; i < fullWords; i++)
        {
            // 8 most significant bits
            writer.Write((byte)(buffer[i] >> 8));
            // 8 least significant bits
            writer.Write((byte)buffer[i]);
        }

        int remainingBits = (int)(bufferLengthInBits % 16);

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

    // Reads and decodes `filename`; returns the decoded text
    public static string Decode(string filename)
    {
        byte[] buffer = File.ReadAllBytes(filename);

        if (buffer.Length == 0)
        {
            // Empty file
            return "";
        }

        // 1. Read the metadata

        // First 6 bits
        int fileFormatVersion = buffer[0] >> 2;

        if (fileFormatVersion > FileFormatVersion)
        {
            throw new InvalidDataException($"File '{filename}' has an unsupported format version ({fileFormatVersion}); it may have been created by a newer version of this program, or it may be corrupted.");
        }

        if (buffer.Length < 8)
        {
            // 4 bytes for the metadata,
            // at least 3 bytes for the tree (a 1 bit and 16 bits for the character code),
            // and at least 1 byte for the text
            throw InvalidFileException(filename);
        }

        int treeLengthInBytes = ((buffer[0] & 0x03) << 16) | (buffer[1] << 8) | buffer[2];
        int bitsOfLastByteOfTree = buffer[3] >> 4;
        int bitsOfLastByteOfText = buffer[3] & 0x0F;

        if (bitsOfLastByteOfTree < 1 || bitsOfLastByteOfTree > 8 || bitsOfLastByteOfText < 1 || bitsOfLastByteOfText > 8 || treeLengthInBytes < 3 || treeLengthInBytes > MaxTreeLengthInBytes || buffer.Length < 5 + treeLengthInBytes)
        {
            throw InvalidFileException(filename);
        }

        // 2. Decode the tree

        HuffmanTree tree = new();
        // The first 32 bits are for the metadata
        int treeBitPosition = 32;

        try
        {
            DecodeTree(tree, buffer, ref treeBitPosition, (3 + treeLengthInBytes) * 8 + bitsOfLastByteOfTree, 0);
        }
        catch (InvalidDataException e)
        {
            throw InvalidFileException(filename, e);
        }

        // 3. Decode the text

        StringBuilder decodedText = new();

        // If the text consists entirely of a single character
        if (tree.IsLeaf)
        {
            for (int i = 4 + treeLengthInBytes; i < buffer.Length; i++)
            {
                // The Huffman code for the only character must be 0
                if (buffer[i] != 0)
                {
                    throw InvalidFileException(filename);
                }
            }

            // The length of the Huffman code is 1,
            // so the number of characters equals the number of bits of the encoded text
            int numOfChars = (buffer.Length - treeLengthInBytes - 5) * 8 + bitsOfLastByteOfText;

            return new string(tree.Character, numOfChars);
        }

        HuffmanTree currentNode = tree;

        for (int i = 4 + treeLengthInBytes; i < buffer.Length - 1; i++)
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

        return decodedText.ToString();
    }

    private static InvalidDataException InvalidFileException(string filename, Exception? innerException = null)
    {
        return new InvalidDataException($"File '{filename}' is corrupted or is not a valid compressed file.", innerException);
    }

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

    private static char ReadChar(byte[] buffer, int bitPosition)
    {
        // Read the character stored in 16 bits, starting at bitPosition

        int index = bitPosition / 8;
        int split = bitPosition % 8;

        if (split == 0)
        {
            return (char)((buffer[index] << 8) | buffer[index + 1]);
        }

        return (char)((buffer[index] << (8 + split)) | (buffer[index + 1] << split) | (buffer[index + 2] >> (8 - split)));
    }
}

readonly record struct CompressionInfo(int TotalChars, int MetadataSize, int TreeSize, int TextSize)
{
    public int TotalSize => MetadataSize + TreeSize + TextSize;

    public override string ToString()
    {
        return $"Characters: {TotalChars:n0}, compressed size: {TotalSize:n0} bytes (metadata: {MetadataSize:n0}, tree: {TreeSize:n0}, text: {TextSize:n0})";
    }
}
