using System;

namespace HuffmanTextCompression
{
  class HuffmanTree
  {
    public char Character;
    public readonly int Frequency;
    public HuffmanTree Left;
    public HuffmanTree Right;

    public HuffmanTree()
    {
      Character = '\0';
      Frequency = -1;
    }

    public HuffmanTree(char character)
    {
      Character = character;
      Frequency = -1;
    }

    public HuffmanTree(char character, int frequency)
    {
      Character = character;
      Frequency = frequency;
    }

    public HuffmanTree(int frequency, HuffmanTree left, HuffmanTree right)
    {
      Character = '\0';
      Frequency = frequency;
      Left = left;
      Right = right;
    }

    public bool IsLeaf()
    {
      // in a HuffmanTree, Right == null if and only if Left == null
      return Left == null;
    }

    public static HuffmanTree GenerateHuffmanTree(string text)
    {
      if (text.Length == 0)
      {
        throw new ArgumentException("Empty text.");
      }

      char[] chars = text.ToCharArray();
      int[] frequencies = new int[0xFFFF + 1];

      for (int i = 0; i < chars.Length; i++)
      {
        frequencies[chars[i]]++;
      }

      HuffmanTreeList list = new HuffmanTreeList();

      for (int i = 0; i <= 0xFFFF; i++)
      {
        if (frequencies[i] != 0)
        {
          list.Add(new HuffmanTree((char)i, frequencies[i]));
        }
      }

      while (!list.HasExactlyOneTree())
      {
        HuffmanTree top1 = list.RemoveTopTree();
        HuffmanTree top2 = list.RemoveTopTree();

        list.Add(new HuffmanTree(top1.Frequency + top2.Frequency, top1, top2));
      }

      return list.RemoveTopTree();
    }
  }
}
