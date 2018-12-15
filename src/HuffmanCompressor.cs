using System;
using System.IO;

namespace HuffmanTextCompression
{
  static class HuffmanCompressor
  {
    public static readonly int FileFormatVersion = 0;

    // encodes `text` and writes to `filename`, returns compression info
    public static CompressionInfo Encode(string text, string filename)
    {
      BinaryWriter writer;

      try
      {
        writer = new BinaryWriter(File.Open(filename, FileMode.Create));
      }
      catch
      {
        throw;
      }

      if (text.Length == 0)
      {
        // create an empty file
        writer.Close();
        return new CompressionInfo(0, 0, 0, 0);
      }

      // 0. generate the tree and the code table

      HuffmanTree tree = HuffmanTree.GenerateHuffmanTree(text);
      HuffmanCodeTable codeTable = new HuffmanCodeTable(tree);

      // 1. encode

      // 1.1. encode the tree

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

      // 1.2. encode the text

      char[] textCharArray = text.ToCharArray();
      ushort[] encodedTextBuffer = new ushort[textCharArray.Length];
      long encodedTextBitPosition = 0;

      for (int i = 0; i < textCharArray.Length; i++)
      {
        AppendCode(encodedTextBuffer, codeTable.GetHuffmanCode(textCharArray[i]), ref encodedTextBitPosition);
      }

      textCharArray = null;

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

      // 2. write

      try
      {
        // 2.1. write the metadata

        // first 6 bits of the file is for FileFormatVersion,
        // next 18 bits for treeLengthInBytes,
        // next 4 bits for bitsOfLastByteOfTree,
        // next 4 bits for bitsOfLastByteOfEncodedText
        // (4 bytes for metadata in total)
        writer.Write((byte)((FileFormatVersion << 2) | (treeLengthInBytes >> 16)));
        writer.Write((byte)(treeLengthInBytes >> 8));
        writer.Write((byte)(treeLengthInBytes));
        writer.Write((byte)((bitsOfLastByteOfTree << 4) | bitsOfLastByteOfEncodedText));

        // 2.2. write the encoded tree
        WriteBuffer(writer, treeBuffer, treeBitPosition);

        // 2.3. write the encoded text
        WriteBuffer(writer, encodedTextBuffer, encodedTextBitPosition);
      }
      catch
      {
        throw;
      }
      finally
      {
        writer.Close();
      }

      return new CompressionInfo(text.Length, 4, treeLengthInBytes, encodedTextLengthInBytes);
    }

    private static void EncodeTree(HuffmanTree tree, ushort[] buffer, ref int bitPosition)
    {
      if (tree.IsLeaf())
      {
        // put a 1 bit
        buffer[bitPosition / 16] |= (ushort)(0x8000 >> (int)(bitPosition % 16));
        bitPosition++;

        // append the char code
        AppendCode(buffer, (ushort)tree.Character, ref bitPosition);

        return;
      }

      // if it's not a leaf, put a 0 bit
      bitPosition++;

      EncodeTree(tree.Left, buffer, ref bitPosition);
      EncodeTree(tree.Right, buffer, ref bitPosition);
    }

    private static void AppendCode(ushort[] buffer, HuffmanCode code, ref long bitPosition)
    {
      // append code.Code (code.Length bits) to the buffer, starting at bitPosition

      int index = (int)(bitPosition / 16);
      int split = (int)(bitPosition % 16) - (16 - code.Length);

      if (split > 0)
      {
        buffer[index] |= (ushort)(code.Code >> split);
        buffer[index + 1] = (ushort)(code.Code << 16 - split);
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
      // append the code (16 bits) to the buffer, starting at bitPosition

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
        buffer[index + 1] = (ushort)(code << 16 - split);
      }

      bitPosition += 16;
    }

    private static void WriteBuffer(BinaryWriter writer, ushort[] buffer, long bufferLengthInBits)
    {
      int i;
      int temp = (int)(bufferLengthInBits / 16);

      for (i = 0; i < temp; i++)
      {
        // 8 more-significant bits
        writer.Write((byte)(buffer[i] >> 8));
        // 8 less-significant bits
        writer.Write((byte)buffer[i]);
      }

      temp = (int)(bufferLengthInBits % 16);

      if (temp > 8)
      {
        // last two bytes
        // 8 more-significant bits
        writer.Write((byte)(buffer[i] >> 8));
        // 8 less-significant bits
        writer.Write((byte)buffer[i]);
      }
      else if (temp > 0)
      {
        // last byte
        // 8 less-significant bits
        writer.Write((byte)(buffer[i] >> 8));
      }
    }

    // reads and decodes `filename`, returns the decoded text
    public static string Decode(string filename)
    {
      byte[] buffer;

      try
      {
        buffer = File.ReadAllBytes(filename);
      }
      catch
      {
        throw;
      }

      if (buffer.Length == 0)
      {
        // empty file
        return "";
      }

      // 1. read metadata

      // first 6 bits
      int fileFormatVersion = buffer[0] & 0xFC;

      if (fileFormatVersion > FileFormatVersion)
      {
        throw new FormatException("File '" + filename + "' has a newer format (file format version " + fileFormatVersion.ToString("00") + "), or is invalid or corrupted.");
      }

      if (buffer.Length < 8)
      {
        // 4 bytes for metadata,
        // at least 3 bytes for tree (a 1 bit and 16 bits for char code),
        // and at least 1 byte for the text
        throw new FormatException("File '" + filename + "' is invalid or corrupted.");
      }

      int treeLengthInBytes = ((int)(buffer[0] & 0x03) << 16) | ((int)buffer[1] << 8) |  ((int)buffer[2]);
      int bitsOfLastByteOfTree = buffer[3] >> 4;
      int bitsOfLastByteOfText = buffer[3] & 0x0F;

      if (bitsOfLastByteOfTree < 1 || bitsOfLastByteOfTree > 8 || bitsOfLastByteOfText < 1 || bitsOfLastByteOfText > 8 || treeLengthInBytes < 3 || treeLengthInBytes > 147456 || buffer.Length < 5 + treeLengthInBytes)
      {
        throw new FormatException("File '" + filename + "' is invalid or corrupted.");
      }

      // 2. decode the tree

      HuffmanTree tree = new HuffmanTree();
      // the first 32 bits are for metadata
      int treeBitPosition = 32;

      try
      {
        DecodeTree(tree, buffer, ref treeBitPosition, (3 + treeLengthInBytes) * 8 + bitsOfLastByteOfTree);
      }
      catch (FormatException)
      {
        throw new FormatException("File '" + filename + "' is invalid or corrupted.");
      }

      // 3. decode the text

      string decodedText = "";

      // if the text entirely consists of a single character
      if (tree.IsLeaf())
      {
        for (int i = 4 + treeLengthInBytes; i < buffer.Length; i++)
        {
          // the huffman code for the only character must be 0
          if (buffer[i] != 0)
          {
            throw new FormatException("File '" + filename + "' is invalid or corrupted.");
          }
        }

        // length of the huffman code is 1,
        // so the number of characters equals the number of bits of the encoded text
        int numOfChars = (buffer.Length - treeLengthInBytes - 5) * 8 + bitsOfLastByteOfText;

        for (int i = 0; i < numOfChars; i++)
        {
          decodedText += tree.Character;
        }

        return decodedText;
      }

      HuffmanTree currentNode = tree;

      for (int i = 4 + treeLengthInBytes; i < buffer.Length - 1; i++)
      {
        for (byte mask = 0x80; mask != 0; mask >>= 1)
        {
          if ((buffer[i] & mask) != 0)
          {
            // reached bit 1, go right
            currentNode = currentNode.Right;
          }
          else
          {
            // reached bit 0, go left
            currentNode = currentNode.Left;
          }

          if (currentNode == null)
          {
            throw new FormatException("File '" + filename + "' is invalid or corrupted.");
          }

          if (currentNode.IsLeaf())
          {
            // reached a leaf, read a character
            decodedText += currentNode.Character;
            // go to the root
            currentNode = tree;
          }
        }
      }

      // only some bits of the last byte (bitsOfLastByteOfEncodedText) are real data
      for (byte mask = 0x80, count = 0; count < bitsOfLastByteOfText; mask >>= 1, count++)
      {
        if ((buffer[buffer.Length - 1] & mask) != 0)
        {
          // reached bit 1, go right
          currentNode = currentNode.Right;
        }
        else
        {
          // reached bit 0, go left
          currentNode = currentNode.Left;
        }

        if (currentNode == null)
        {
          throw new FormatException("File '" + filename + "' is invalid or corrupted.");
        }

        if (currentNode.IsLeaf())
        {
          // reached a leaf, read a character
          decodedText += currentNode.Character;
          // go to the root
          currentNode = tree;
        }
      }

      return decodedText;
    }

    private static void DecodeTree(HuffmanTree tree, byte[] buffer, ref int bitPosition, int endPosition)
    {
      // if the bit at bitPosition is 1
      if ((buffer[bitPosition / 8] & (byte)(0x80 >> (bitPosition % 8))) != 0)
      {
        // read the character stored in 16 bits next to the bit at bitPosition
        tree.Character = ReadChar(buffer, ++bitPosition);
        bitPosition += 16;

        if (bitPosition > endPosition)
        {
          // invalid or corrupted file
          throw new FormatException();
        }

        return;
      }

      bitPosition++;

      if (bitPosition >= endPosition)
      {
        // invalid or corrupted file
        throw new FormatException();
      }

      tree.Left = new HuffmanTree();
      DecodeTree(tree.Left, buffer, ref bitPosition, endPosition);

      tree.Right = new HuffmanTree();
      DecodeTree(tree.Right, buffer, ref bitPosition, endPosition);
    }

    private static char ReadChar(byte[] buffer, int bitPosition)
    {
      // read the character stored in 16 bits, starting at bitPosition

      int index = bitPosition / 8;
      int split = bitPosition % 8;

      if (split == 0)
      {
        return (char)(((ushort)buffer[index] << 8) | buffer[index + 1]);
      }

      return (char)(((ushort)buffer[index] << 8 + split) | ((ushort)buffer[index + 1] << split) | (buffer[index + 2] >> 8 - split));
    }
  }

  struct CompressionInfo
  {
    public readonly int TotalChars;
    public readonly int MetadataSize;
    public readonly int TreeSize;
    public readonly int TextSize;

    public CompressionInfo(int totalChars, int metadataSize, int treeSize, int textSize)
    {
      TotalChars = totalChars;
      MetadataSize = metadataSize;
      TreeSize = treeSize;
      TextSize = textSize;
    }

    public string ToString()
    {
      long compressedFileSize = MetadataSize + TreeSize + TextSize;

      return
        TotalChars.ToString("n0") + " char(s) total" + Environment.NewLine +
        compressedFileSize.ToString("n0") + " byte(s) total" + Environment.NewLine +
        MetadataSize.ToString("n0") + " byte(s) for metadata" + Environment.NewLine +
        TreeSize.ToString("n0") + " byte(s) for tree" + Environment.NewLine +
        TextSize.ToString("n0") + " byte(s) for text";
    }
  }
}
